namespace Shared;

public sealed record OcrJob(
    string DocumentId,
    string BlobName,
    string OriginalFileName,
    string ContentType,
    string? Source,
    string? Language
);

