using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Shared;
using System.Text;
using System.Text.Json;

namespace BLL.Services;

public interface IOcrResultStore
{
    Task SaveAsync(OcrStoredResult result, CancellationToken ct = default);
}

public class BlobOcrResultStore : IOcrResultStore
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _config;

    public BlobOcrResultStore(BlobServiceClient blobServiceClient, IConfiguration config)
    {
        _blobServiceClient = blobServiceClient;
        _config = config;
    }

    public async Task SaveAsync(OcrStoredResult result, CancellationToken ct = default)
    {
        var containerName = _config["Storage:OcrResultsContainer"] ?? "ocr-results";
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);

        var blob = container.GetBlobClient($"{result.DocumentId}.json");
        using var ms = new MemoryStream(bytes);
        await blob.UploadAsync(ms, overwrite: true, cancellationToken: ct);
    }
}

