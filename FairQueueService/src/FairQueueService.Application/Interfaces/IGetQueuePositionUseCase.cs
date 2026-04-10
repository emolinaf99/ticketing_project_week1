using FairQueueService.Application.DTOs;

namespace FairQueueService.Application.Interfaces;

public interface IGetQueuePositionUseCase
{
    Task<QueuePositionDto?> HandleAsync(long ticketId, string userId, CancellationToken ct = default);
}
