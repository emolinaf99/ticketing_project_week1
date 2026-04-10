namespace FairQueueService.Application.Interfaces;

public interface IAdvanceQueueUseCase
{
    Task HandleAsync(long ticketId, CancellationToken ct = default);
}
