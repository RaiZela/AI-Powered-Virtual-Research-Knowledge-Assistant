using BLL.Helper;
using Shared;

namespace BLL.Services;

public interface IOcrPipeline
{
    Task<OcrStoredResult> RunAsync(
        string documentId,
        string blobName,
        string originalFileName,
        string contentType,
        Stream fileContent,
        string? language,
        CancellationToken ct = default);
}

public sealed class OcrPipeline : IOcrPipeline
{
    private readonly IOcrService _ocr;
    private readonly IOcrResultStore _store;

    public OcrPipeline(IOcrService ocr, IOcrResultStore store)
    {
        _ocr = ocr;
        _store = store;
    }

    public async Task<OcrStoredResult> RunAsync(
        string documentId,
        string blobName,
        string originalFileName,
        string contentType,
        Stream fileContent,
        string? language,
        CancellationToken ct = default)
    {
        var textOriginal = await _ocr.ExtractTextAsync(fileContent, contentType, ct);

        // AI-2.4: normalize immediately
        var normalizedCommon = TextNormalize.NormalizeCommon(textOriginal);
        var textNormalized = language switch
        {
            "ar" => TextNormalize.NormalizeArabicForSearch(normalizedCommon),
            "he" => TextNormalize.NormalizeHebrewForSearch(normalizedCommon),
            _ => normalizedCommon
        };

        var result = new OcrStoredResult
        {
            DocumentId = documentId,
            BlobName = blobName,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            Language = language,
            TextOriginal = textOriginal,
            TextNormalized = textNormalized,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await _store.SaveAsync(result, ct);
        return result;
    }
}

