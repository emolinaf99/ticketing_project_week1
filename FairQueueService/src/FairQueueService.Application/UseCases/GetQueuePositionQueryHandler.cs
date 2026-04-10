using FairQueueService.Application.DTOs;
using FairQueueService.Application.Interfaces;
using FairQueueService.Domain.Ports;
using Microsoft.Extensions.Options;

namespace FairQueueService.Application.UseCases;

public sealed class GetQueuePositionQueryHandler : IGetQueuePositionUseCase
{
    private readonly IQueueRepository _repo;
    private readonly int _turnDurationSeconds;

    public GetQueuePositionQueryHandler(IQueueRepository repo, IOptions<QueueSettings> settings)
    {
        _repo = repo;
        _turnDurationSeconds = settings.Value.TurnDurationSeconds;
    }

    public async Task<QueuePositionDto?> HandleAsync(long ticketId, string userId, CancellationToken ct = default)
    {
        var entry = await _repo.FindByUserAndTicketAsync(userId, ticketId, ct);
        if (entry is null) return null;

        var total = await _repo.CountActivePositionsAsync(ticketId, ct);
        var wait  = (entry.Position - 1) * (_turnDurationSeconds / 2);

        return new QueuePositionDto(
            ticketId,
            userId,
            entry.Position,
            total,
            wait,
            entry.Status.ToString().ToLowerInvariant(),
            entry.TurnExpiresAt);
    }
}
