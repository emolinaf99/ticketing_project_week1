namespace FairQueueService.Application.Interfaces;

public interface ILeaveQueueUseCase
{
    Task HandleAsync(long ticketId, string userId, CancellationToken ct = default);
}
