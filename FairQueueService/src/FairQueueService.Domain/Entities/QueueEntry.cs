namespace FairQueueService.Domain.Entities;

public sealed class QueueEntry
{
    public long Id { get; private set; }
    public long EventId { get; private set; }
    public long TicketId { get; private set; }
    public string UserId { get; private set; } = default!;   // claim 'sub' del JWT
    public string Email { get; private set; } = default!;   // claim 'email' del JWT
    public int Position { get; private set; }                // 1-indexed
    public QueueStatus Status { get; private set; }
    public DateTime EnteredAt { get; private set; }
    public DateTime? TurnStartsAt { get; private set; }
    public DateTime? TurnExpiresAt { get; private set; }

    private QueueEntry() { }

    public static QueueEntry Create(long eventId, long ticketId,
                                    string userId, string email, int position)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId is required.");
        if (string.IsNullOrWhiteSpace(email))  throw new ArgumentException("email is required.");
        if (position < 1) throw new ArgumentOutOfRangeException(nameof(position), "Position must be >= 1.");

        return new QueueEntry
        {
            EventId   = eventId,
            TicketId  = ticketId,
            UserId    = userId.Trim(),
            Email     = email.Trim().ToLowerInvariant(),
            Position  = position,
            Status    = QueueStatus.Waiting,
            EnteredAt = DateTime.UtcNow
        };
    }

    public void ActivateTurn(int turnDurationSeconds)
    {
        Status         = QueueStatus.Active;
        TurnStartsAt   = DateTime.UtcNow;
        TurnExpiresAt  = DateTime.UtcNow.AddSeconds(turnDurationSeconds);
    }

    public void Complete() => Status = QueueStatus.Completed;
    public void Cancel()   => Status = QueueStatus.Cancelled;
    public void TimedOut() => Status = QueueStatus.TimedOut;

    public bool IsTurnExpired() =>
        Status == QueueStatus.Active &&
        TurnExpiresAt.HasValue &&
        TurnExpiresAt.Value <= DateTime.UtcNow;
}
