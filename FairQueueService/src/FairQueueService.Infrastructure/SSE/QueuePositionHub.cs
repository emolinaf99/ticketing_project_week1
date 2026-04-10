using System.Collections.Concurrent;
using System.Threading.Channels;
using FairQueueService.Application.DTOs;
using FairQueueService.Application.Interfaces;

namespace FairQueueService.Infrastructure.SSE;

/// Singleton in-memory hub — mismo patrón que TicketStatusHub del CrudService.
/// Keyed por (ticketId, userId) para notificaciones individuales y colectivas.
public sealed class QueuePositionHub : IQueuePositionNotifier, IQueuePositionSubscriber
{
    // ticketId → (userId → Channel)
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, Channel<QueueStatusUpdate>>>
        _subs = new();

    public ChannelReader<QueueStatusUpdate> Subscribe(long ticketId, string userId)
    {
        var ch = Channel.CreateBounded<QueueStatusUpdate>(new BoundedChannelOptions(5)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

        var ticketSubs = _subs.GetOrAdd(ticketId, _ => new ConcurrentDictionary<string, Channel<QueueStatusUpdate>>());
        ticketSubs[userId] = ch;
        return ch.Reader;
    }

    public void Unsubscribe(long ticketId, string userId)
    {
        if (_subs.TryGetValue(ticketId, out var ticketSubs))
        {
            if (ticketSubs.TryRemove(userId, out var ch))
                ch.Writer.TryComplete();
            if (ticketSubs.IsEmpty)
                _subs.TryRemove(ticketId, out _);
        }
    }

    public void NotifyAll(long ticketId, IEnumerable<QueueStatusUpdate> updates)
    {
        if (!_subs.TryGetValue(ticketId, out var ticketSubs)) return;

        foreach (var update in updates)
        {
            if (ticketSubs.TryGetValue(update.UserId, out var ch))
                ch.Writer.TryWrite(update);
        }
    }

    public void NotifyOne(long ticketId, string userId, QueueStatusUpdate update)
    {
        if (_subs.TryGetValue(ticketId, out var ticketSubs) &&
            ticketSubs.TryGetValue(userId, out var ch))
        {
            ch.Writer.TryWrite(update);
        }
    }
}
