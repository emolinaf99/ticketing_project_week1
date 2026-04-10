using FairQueueService.Domain.Entities;
using FairQueueService.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairQueueService.Infrastructure.Persistence.Repositories;

public sealed class QueueRepository : IQueueRepository
{
    private readonly FairQueueDbContext _db;

    public QueueRepository(FairQueueDbContext db) => _db = db;

    public Task<QueueEntry?> FindByUserAndTicketAsync(string userId, long ticketId, CancellationToken ct) =>
        _db.QueueEntries
           .FirstOrDefaultAsync(e => e.UserId == userId && e.TicketId == ticketId, ct);

    public Task<int> CountActivePositionsAsync(long ticketId, CancellationToken ct) =>
        _db.QueueEntries
           .CountAsync(e => e.TicketId == ticketId &&
                            (e.Status == QueueStatus.Waiting || e.Status == QueueStatus.Active), ct);

    public async Task AddAsync(QueueEntry entry, CancellationToken ct)
    {
        _db.QueueEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(QueueEntry entry, CancellationToken ct)
    {
        _db.QueueEntries.Update(entry);
        await _db.SaveChangesAsync(ct);
    }

    public Task<QueueEntry?> GetNextWaitingAsync(long ticketId, CancellationToken ct) =>
        _db.QueueEntries
           .Where(e => e.TicketId == ticketId && e.Status == QueueStatus.Waiting)
           .OrderBy(e => e.Position)
           .FirstOrDefaultAsync(ct);

    public Task<List<QueueEntry>> GetAllWaitingAsync(long ticketId, CancellationToken ct) =>
        _db.QueueEntries
           .Where(e => e.TicketId == ticketId && e.Status == QueueStatus.Waiting)
           .OrderBy(e => e.Position)
           .ToListAsync(ct);

    public Task<QueueEntry?> GetActiveAsync(long ticketId, CancellationToken ct) =>
        _db.QueueEntries
           .FirstOrDefaultAsync(e => e.TicketId == ticketId && e.Status == QueueStatus.Active, ct);

    public Task<List<QueueEntry>> GetExpiredActiveEntriesAsync(CancellationToken ct) =>
        _db.QueueEntries
           .Where(e => e.Status == QueueStatus.Active &&
                       e.TurnExpiresAt.HasValue &&
                       e.TurnExpiresAt.Value <= DateTime.UtcNow)
           .ToListAsync(ct);
}
