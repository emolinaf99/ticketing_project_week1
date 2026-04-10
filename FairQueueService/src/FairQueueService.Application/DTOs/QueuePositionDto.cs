namespace FairQueueService.Application.DTOs;

public record QueuePositionDto(
    long TicketId,
    string UserId,
    int Position,
    int TotalInQueue,
    int EstimatedWaitSeconds,
    string Status,           // "waiting" | "active" | "completed" | "cancelled" | "timed_out"
    DateTime? TurnExpiresAt  // solo cuando status="active"
);
