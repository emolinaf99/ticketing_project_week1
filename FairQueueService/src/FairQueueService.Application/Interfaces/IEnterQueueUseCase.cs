using FairQueueService.Application.DTOs;

namespace FairQueueService.Application.Interfaces;

public interface IEnterQueueUseCase
{
    Task<EnterQueueResponse> HandleAsync(EnterQueueCommand command, CancellationToken ct = default);
}
