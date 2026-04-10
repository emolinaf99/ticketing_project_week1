namespace FairQueueService.Application.DTOs;

/// Payload enviado por SSE al cliente cuando su posición cambia.
public record QueueStatusUpdate(
    long TicketId,
    string UserId,
    int Position,
    int TotalInQueue,
    int EstimatedWaitSeconds,
    string Status,           // "waiting" | "active" | "timed_out" | "completed" | "cancelled"
    DateTime? TurnExpiresAt
);
