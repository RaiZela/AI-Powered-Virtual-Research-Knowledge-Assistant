using System;
using System.Collections.Generic;
using System.Text;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Shared;
namespace BLL.Services;

public interface IDocumentsService
{
    public Task<ApiReturn<FileUploadBlobResult>> UploadDocument(
        Stream file,
        string? source,
        string? language,
        string? fileName,
        string? contentType);
}
public class DocumentsService : IDocumentsService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _config;
    private readonly IOcrJobQueue _jobQueue;
    public DocumentsService(BlobServiceClient blobServiceClient, IConfiguration config, IOcrJobQueue ocrJobQueue)
    {
        _blobServiceClient = blobServiceClient;
        _config = config;
        _jobQueue = ocrJobQueue;
    }
    public async Task<ApiReturn<FileUploadBlobResult>> UploadDocument(
        Stream file,
        string? source,
        string? language,
        string? fileName,
        string? contentType)
    {

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".txt"
        };

        var allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/jpeg",
            "image/png",
            "text/plain"
        };

        var extension = Path.GetExtension(fileName);

        if (!allowedExtensions.Contains(extension))
        {
            return new ApiReturn<FileUploadBlobResult>
            {
                Message = "Unsupported file extension.",
                StatusCode = 400
            };
        }

        if (!allowedContentTypes.Contains(contentType))
        {
            return new ApiReturn<FileUploadBlobResult>
            {
                Message = "Unsupported file type.",
                StatusCode = 400
            };
        }
        const long maxSize = 200 * 1024 * 1024;

        if (file.Length > maxSize)
        {
            return new ApiReturn<FileUploadBlobResult>
            {
                Message = "File too large.",
                StatusCode = 400
            };
        }


        var containerName = _config["Storage:ContainerName"] ?? "documents";
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync();

        //unique blob name
        var blobName = $"{Guid.NewGuid()}{extension}";

        var blobClient = container.GetBlobClient(blobName);

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["originalname"] = fileName,
            ["contenttype"] = contentType,
            ["uploadedat"] = DateTimeOffset.UtcNow.ToString("O"),
            ["source"] = source ?? "unknown",   
            ["language"] = language ?? "unknown"
        };

        if (file.CanSeek) file.Position = 0;

        await blobClient.UploadAsync(file, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            Metadata = metadata
        });
        await _jobQueue.EnqueueAsync(new OcrJob(
    DocumentId: blobName,          
    BlobName: blobName,
    OriginalFileName: fileName!,
    ContentType: contentType!,
    Source: source,
    Language: language
));

        return new ApiReturn<FileUploadBlobResult>
        {
            Message = "",
            StatusCode = 200,
            Result = new FileUploadBlobResult
            {
                Id = blobName,
                OriginalName = fileName,
                Size = file.Length,
                ContainerName = containerName

            }
        };

    }
}
