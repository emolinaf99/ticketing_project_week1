namespace FairQueueService.Application.DTOs;

public record EnterQueueCommand(long EventId, long TicketId, string UserId, string Email);

public record EnterQueueResponse(
    long TicketId,
    int Position,
    int TotalInQueue,
    int EstimatedWaitSeconds,
    string Status,          // "waiting" | "active"
    DateTime? TurnExpiresAt // solo cuando status="active"
);
