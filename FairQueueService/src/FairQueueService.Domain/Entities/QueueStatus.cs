namespace FairQueueService.Domain.Entities;

public enum QueueStatus
{
    Waiting,
    Active,
    Completed,
    Cancelled,
    TimedOut
}
