

using System.Threading.Channels;

namespace TradingBackend.Api.Fix;

public interface IOutgoingFixMessageQueue
{
    ValueTask EnqueueAsync(
        IncomingFixMessage message,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<IncomingFixMessage> ReadAllAsync(
        CancellationToken cancellationToken = default);
}

public class OutgoingFixMessageQueue : IOutgoingFixMessageQueue
{
    private readonly Channel<IncomingFixMessage> _queue;

    public OutgoingFixMessageQueue()
    {
        _queue = Channel.CreateBounded<IncomingFixMessage>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
    }

    public ValueTask EnqueueAsync(
        IncomingFixMessage message,
        CancellationToken cancellationToken = default)
    {
        return _queue.Writer.WriteAsync(message, cancellationToken);
    }

    public IAsyncEnumerable<IncomingFixMessage> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _queue.Reader.ReadAllAsync(cancellationToken);
    }
}
