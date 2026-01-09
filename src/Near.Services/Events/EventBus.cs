using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Near.Services.Events;

public interface IEventBus<T>
{
    ValueTask PublishAsync(T item, CancellationToken cancellationToken = default);

    IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken = default);
}

public sealed class EventBus<T> : IEventBus<T>
{
    private readonly Channel<T> _channel = Channel.CreateUnbounded<T>();

    public ValueTask PublishAsync(T item, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(item, cancellationToken);
    }

    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
