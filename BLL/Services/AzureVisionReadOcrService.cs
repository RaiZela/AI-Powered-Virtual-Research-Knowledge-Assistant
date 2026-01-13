using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BLL.Services;

public interface IAzureVisionReadOcrService
{
    Task<string> ExtractTextAsync(Stream content, string contentType, CancellationToken ct = default);
}
public class AzureVisionReadOcrService : IAzureVisionReadOcrService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    public AzureVisionReadOcrService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }
    public async Task<string> ExtractTextAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        var endpoint = _config["ComputerVision:Endpoint"]?.TrimEnd('/')
             ?? throw new InvalidOperationException("Missing ComputerVision:Endpoint");

        var key = _config["ComputerVision:Key"]
            ?? throw new InvalidOperationException("Missing ComputerVision:Key");

        var client = _httpClientFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/vision/v3.1/read/analyze");
        request.Headers.Add("Ocp-Apim-Subscription-Key", key);
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

        request.Content = streamContent;

        using var startResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (startResponse.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            var body = await startResponse.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Read start failed: {(int)startResponse.StatusCode} {startResponse.ReasonPhrase}. Body: {body}");
        }

        if (!startResponse.Headers.TryGetValues("Operation-Location", out var values))
            throw new InvalidOperationException("Missing Operation-Location header from Read API response.");

        var operationLocation = values.First();

        const int maxAttempts = 30;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1.2), ct);

            using var getReq = new HttpRequestMessage(HttpMethod.Get, operationLocation);
            getReq.Headers.Add("Ocp-Apim-Subscription-Key", key);

            using var resultResponse = await client.SendAsync(getReq, ct);
            var json = await resultResponse.Content.ReadAsStringAsync(ct);

            resultResponse.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(json);
            var status = doc.RootElement.GetProperty("status").GetString();

            if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                var sb = new StringBuilder();
                if (doc.RootElement.TryGetProperty("analyzeResult", out var analyzeResult) &&
                    analyzeResult.TryGetProperty("readResults", out var readResults))
                {
                    foreach (var page in readResults.EnumerateArray())
                    {
                        if (page.TryGetProperty("lines", out var lines))
                        {
                            foreach (var line in lines.EnumerateArray())
                            {
                                if (line.TryGetProperty("text", out var text))
                                    sb.AppendLine(text.GetString());
                            }
                        }
                    }
                }
                return sb.ToString().Trim();
            }

            if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Read OCR failed. Payload: {json}");
            }
        }

        throw new TimeoutException("Read OCR timed out waiting for operation to complete.");
    }
}
