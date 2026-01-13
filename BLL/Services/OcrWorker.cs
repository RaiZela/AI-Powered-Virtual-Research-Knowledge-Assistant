using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;
namespace BLL.Services;

public sealed class OcrWorker : BackgroundService
{
    private readonly IOcrJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OcrWorker> _logger;

    public OcrWorker(
           IOcrJobQueue queue,
           IServiceScopeFactory scopeFactory,
           ILogger<OcrWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OCR Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            OcrJob job;

            try
            {
                job = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOcrJobProcessor>();
                await processor.ProcessAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR job failed for {DocumentId}", job.DocumentId);
            }
        }

        _logger.LogInformation("OCR Worker stopped.");
    }
}
