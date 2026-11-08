using System.Threading.Channels;

namespace TradingBackend.Api.Fix;

public interface IIncomingFixMessageQueue
{
    ValueTask EnqueueAsync(
        IncomingFixMessage message,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<IncomingFixMessage> ReadAllAsync(
        CancellationToken cancellationToken = default);
}

public class IncomingFixMessageQueue : IIncomingFixMessageQueue
{
    private readonly Channel<IncomingFixMessage> _queue;

    public IncomingFixMessageQueue()
    {
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _queue = Channel.CreateBounded<IncomingFixMessage>(options);
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