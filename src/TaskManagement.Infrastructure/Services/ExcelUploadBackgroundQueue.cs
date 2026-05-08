using System.Threading.Channels;

namespace TaskManagement.Infrastructure.Services;

public static class ExcelUploadBackgroundQueue
{
    private static readonly Channel<long> _channel = Channel.CreateUnbounded<long>(
        new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    public static void Enqueue(long uploadId)
    {
        // Unbounded channel; TryWrite should always succeed.
        _channel.Writer.TryWrite(uploadId);
    }

    public static ValueTask<long> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}

