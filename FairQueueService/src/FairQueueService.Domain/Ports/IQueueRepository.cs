using FairQueueService.Domain.Entities;

namespace FairQueueService.Domain.Ports;

public interface IQueueRepository
{
    Task<QueueEntry?> FindByUserAndTicketAsync(string userId, long ticketId, CancellationToken ct = default);
    Task<int> CountActivePositionsAsync(long ticketId, CancellationToken ct = default);
    Task AddAsync(QueueEntry entry, CancellationToken ct = default);
    Task UpdateAsync(QueueEntry entry, CancellationToken ct = default);
    Task<QueueEntry?> GetNextWaitingAsync(long ticketId, CancellationToken ct = default);
    Task<List<QueueEntry>> GetAllWaitingAsync(long ticketId, CancellationToken ct = default);
    Task<QueueEntry?> GetActiveAsync(long ticketId, CancellationToken ct = default);
    Task<List<QueueEntry>> GetExpiredActiveEntriesAsync(CancellationToken ct = default);
}
