using Azure;
using Azure.AI.TextAnalytics;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BLL.Services;
using Microsoft.AspNetCore.Http.Features;
using Shared.NER;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var keyVaultUri = builder.Configuration["KeyVault:Uri"];

if (!string.IsNullOrEmpty(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}

var storageCs = builder.Configuration["Storage:ConnectionString"]
    ?? throw new InvalidOperationException("Missing Storage:ConnectionString");

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["Language:Endpoint"]!;
    var key = config["Language:Key"]!;

    return new TextAnalyticsClient(new Uri(endpoint), new AzureKeyCredential(key));
});


builder.Services.AddSingleton(new BlobServiceClient(storageCs));
builder.Services.AddHttpClient();

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 200 * 1024 * 1024; // 200 MB
});

builder.Services.AddScoped<IOcrService, OcrService>();
builder.Services.AddScoped<ILanguageDetectionService, AzureLanguageDetectionService>();
builder.Services.AddScoped<INerService, NerService>();
builder.Services.AddScoped<IKeyPhraseService,KeyPhraseService>();
builder.Services.AddScoped<IPiiService,PiiService>();
builder.Services.AddScoped<IOcrPipeline, OcrPipeline>();
builder.Services.AddScoped<IOcrResultStore, BlobOcrResultStore>();
builder.Services.AddScoped<IDocumentsService, DocumentsService>();
builder.Services.AddSingleton<IOcrJobQueue, OcrJobQueue>();
builder.Services.AddScoped<IOcrJobProcessor, OcrJobProcessor>();
builder.Services.AddHostedService<OcrWorker>();
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["Search:Endpoint"] ?? throw new InvalidOperationException("Missing Search:Endpoint");
    var key = config["Search:AdminKey"] ?? throw new InvalidOperationException("Missing Search:AdminKey");
    var indexName = config["Search:IndexName"] ?? "idx-ocr-results";

    return new SearchClient(new Uri(endpoint), indexName, new AzureKeyCredential(key));
});

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    var endpoint = config["Search:Endpoint"] ?? throw new InvalidOperationException("Missing Search:Endpoint");
    var key = config["Search:AdminKey"] ?? throw new InvalidOperationException("Missing Search:AdminKey"); // or QueryKey later
    var indexName = config["Search:IndexName"] ?? "idx-ocr-results";

    return new SearchClient(new Uri(endpoint), indexName, new AzureKeyCredential(key));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


app.UseSwagger();
app.UseSwaggerUI();



app.MapPost("/documents", async (
    IFormFile file,
    string? source,
    string? language,
    BlobServiceClient blobServiceClient,
    IConfiguration config,
    IDocumentsService documentService) =>
{
    if (file is null || file.Length == 0)
        return Results.BadRequest("No file uploaded.");
    await using var stream = file.OpenReadStream();
    var result = await documentService.UploadDocument(stream, source, language, file.FileName, file.ContentType);

    if (result.StatusCode == 200)
        return Results.Ok(result.Result);
    else
        return Results.BadRequest(result.Message);
})
    .Accepts<IFormFile>("multipart/form-data")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .DisableAntiforgery();


app.MapPost("/language/detect", async (
    string text,
    ILanguageDetectionService detector) =>
{
    if (string.IsNullOrWhiteSpace(text))
        return Results.BadRequest("Text is required.");

    var language = await detector.DetectAsync(text);
    return Results.Ok(new { language });
})
.Accepts<string>("application/json")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.MapPost("/ner", async (string request, INerService ner) =>
{
    if (string.IsNullOrWhiteSpace(request))
        return Results.BadRequest("Text is required.");

    var entities = await ner.RecognizeEntity(request);
    return Results.Ok(entities);
})
.Produces<List<EntityOutput>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);


app.MapPost("/ner/batch", async (List<string> request, INerService ner) =>
{
    if (request is null || request.Count == 0)
        return Results.BadRequest("Documents are required.");

    var results = await ner.RecognizeBatchEntity(request);
    return Results.Ok(results);
})
.Produces<List<EntityOutputs>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);
app.MapPost("/keyphrases", async (string request, IKeyPhraseService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request))
        return Results.BadRequest("Text is required.");

    var keyPhrases = await svc.ExtractAsync(request, ct);
    return Results.Ok(new { keyPhrases });
})
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.MapPost("/keyphrases/batch", async (List<string> request, IKeyPhraseService svc, CancellationToken ct) =>
{
    if (request is null || request.Count == 0)
        return Results.BadRequest("Documents are required.");

    var results = await svc.ExtractBatchAsync(request, ct);

    return Results.Ok(new { results });
})
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.MapPost("/pii/redact", async (TextDocumentInput request, IPiiService pii, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
        return Results.BadRequest("Text is required.");

    var result = await pii.RedactAsync(request.Text, ct);
    return Results.Ok(result);
})
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.MapGet("/search", async (
    string q,
    int top,
    string? language,
    SearchClient searchClient,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest("Query 'q' is required.");

    top = (top <= 0 || top > 50) ? 10 : top;

    var options = new SearchOptions
    {
        Size = top,
        QueryType = SearchQueryType.Full,
        IncludeTotalCount = true
    };

    if (!string.IsNullOrWhiteSpace(language))
        options.Filter = $"detectedLanguage eq '{language}'";

    options.Select.Add("documentId");
    options.Select.Add("originalFileName");
    options.Select.Add("language");
    options.Select.Add("textNormalized");

    var response = await searchClient.SearchAsync<SearchDocument>(q, options, ct);

    var results = new List<object>();
    await foreach (var r in response.Value.GetResultsAsync())
    {
        results.Add(new
        {
            score = r.Score,
            doc = r.Document
        });
    }

    return Results.Ok(new
    {
        count = response.Value.TotalCount,
        results
    });
});


app.MapGet("/search/semantic", async (
    string q,
    SearchClient searchClient,
    IConfiguration config,
    int top,
    string? filter,
    string? language,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest("Query 'q' is required.");

    top = (top <= 0 || top > 50) ? 5 : top; 

    var semanticConfig = config["Search:SemanticConfig"] ?? "sem-default";

    var options = new SearchOptions
    {
        Size = top,
        IncludeTotalCount = true,
        QueryType = SearchQueryType.Semantic, 
        Filter = string.IsNullOrWhiteSpace(filter) ? null : filter,
        SemanticSearch = new SemanticSearchOptions
        {
            SemanticConfigurationName = semanticConfig,
            QueryCaption = new QueryCaption(QueryCaptionType.Extractive),
            QueryAnswer = new QueryAnswer(QueryAnswerType.Extractive)
        }
    };

    options.Select.Add("documentId");
    options.Select.Add("originalFileName");
    options.Select.Add("language");
    options.Select.Add("textNormalized");

    var resp = await searchClient.SearchAsync<SearchDocument>(q, options, ct);

    var items = new List<object>();
    await foreach (var r in resp.Value.GetResultsAsync())
    {
        items.Add(new
        {
            score = r.Score,
            rerankerScore = r.SemanticSearch?.RerankerScore,
            doc = r.Document,
            captions = r.SemanticSearch?.Captions?.Select(c => new { c.Text, c.Highlights }),
        });
    }

    var answers = resp.Value.SemanticSearch?.Answers?.Select(a => new { a.Text, a.Score });

    return Results.Ok(new
    {
        count = resp.Value.TotalCount,
        answers,
        results = items
    });
});
app.Run();

