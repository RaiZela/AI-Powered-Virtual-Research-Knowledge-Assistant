using Azure;
using Azure.AI.TextAnalytics;
using BLL.Helper;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace BLL.Services;

public interface ILanguageDetectionService
{
    Task<string?> DetectAsync(string text, CancellationToken ct = default);
}


public class AzureLanguageDetectionService : ILanguageDetectionService
{
    private readonly IConfiguration _config;
    public AzureLanguageDetectionService(IConfiguration config)
    {
        _config = config;
    }

    public async Task<string?> DetectAsync(string text, CancellationToken ct = default)
    {
        if(string.IsNullOrEmpty(text)) return null;

        var endpoint = _config["Language:Endpoint"]
            ?? throw new InvalidOperationException("Missing Language:Endpoint");
        var key = _config["Language:Key"]
            ?? throw new InvalidOperationException("Missing Language:Key");

        var client = new TextAnalyticsClient(new Uri(endpoint), new AzureKeyCredential(key));

        var sample = text.Length > 5000 ? text[..5000] : text;

        var normalized = TextNormalize.RemoveDiacritics(text);
        var resp = await client.DetectLanguageAsync(sample, cancellationToken: ct);

        return resp.Value.Iso6391Name;
    }
}
