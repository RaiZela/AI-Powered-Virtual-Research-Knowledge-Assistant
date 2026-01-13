using System;
using System.Collections.Generic;
using System.Text;

namespace BLL.Services;

using Shared;
using System.Threading.Channels;

public interface IOcrJobQueue
{
    ValueTask EnqueueAsync(OcrJob job, CancellationToken ct = default);
    ValueTask<OcrJob> DequeueAsync(CancellationToken ct);
}

public sealed class OcrJobQueue : IOcrJobQueue
{
    private readonly Channel<OcrJob> _channel = Channel.CreateUnbounded<OcrJob>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ValueTask EnqueueAsync(OcrJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);

    public ValueTask<OcrJob> DequeueAsync(CancellationToken ct)
        => _channel.Reader.ReadAsync(ct);
}
