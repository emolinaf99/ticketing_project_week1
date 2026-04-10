namespace FairQueueService.Domain.Ports;

public interface ITicketReservedPublisher
{
    Task PublishAsync(long ticketId, long eventId, string userId, string email,
                      int turnDurationSeconds, CancellationToken ct = default);
}
