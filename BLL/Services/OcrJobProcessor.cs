using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BLL.Helper;
using Microsoft.Extensions.Configuration;
using Shared;

namespace BLL.Services;

public interface IOcrJobProcessor
{
    Task ProcessAsync(OcrJob job, CancellationToken ct);
}

public sealed class OcrJobProcessor : IOcrJobProcessor
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _config;
    private readonly IOcrService _ocrService;
    private readonly IOcrResultStore _ocrResultStore;

    public OcrJobProcessor(
        BlobServiceClient blobServiceClient,
        IConfiguration config,
        IOcrService ocrService,
        IOcrResultStore ocrResultStore)
    {
        _blobServiceClient = blobServiceClient;
        _config = config;
        _ocrService = ocrService;
        _ocrResultStore = ocrResultStore;
    }

    public async Task ProcessAsync(OcrJob job, CancellationToken ct)
    {
        await MarkStatusAsync(job, "ocr-running", ct);

        var containerName = _config["Storage:ContainerName"] ?? "documents";
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(job.BlobName);

        await using var blobStream = await blob.OpenReadAsync(cancellationToken: ct);

        var textOriginal = await _ocrService.ExtractTextAsync(blobStream, job.ContentType, ct);

        var common = TextNormalize.NormalizeCommon(textOriginal);
        var textNormalized = (job.Language ?? "unknown") switch
        {
            "ar" => TextNormalize.NormalizeArabicForSearch(common),
            "he" => TextNormalize.NormalizeHebrewForSearch(common),
            _ => common
        };

        var stored = new OcrStoredResult
        {
            DocumentId = job.DocumentId,
            BlobName = job.BlobName,
            OriginalFileName = job.OriginalFileName,
            ContentType = job.ContentType,
            Language = job.Language,
            TextOriginal = textOriginal,
            TextNormalized = textNormalized,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await _ocrResultStore.SaveAsync(stored, ct);

        await MarkStatusAsync(job, "ocr-done", ct);
    }

    private async Task MarkStatusAsync(OcrJob job, string status, CancellationToken ct)
    {
        var containerName = _config["Storage:ContainerName"] ?? "documents";
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(job.BlobName);

        BlobProperties props = await blob.GetPropertiesAsync(cancellationToken: ct);

        var md = new Dictionary<string, string>(props.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            ["processingstatus"] = status,
            ["processingupdatedat"] = DateTimeOffset.UtcNow.ToString("O")
        };

        await blob.SetMetadataAsync(md, cancellationToken: ct);
    }
}
