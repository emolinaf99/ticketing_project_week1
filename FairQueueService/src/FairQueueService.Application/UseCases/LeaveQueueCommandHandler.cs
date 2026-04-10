using FairQueueService.Application.Interfaces;
using FairQueueService.Domain.Entities;
using FairQueueService.Domain.Ports;

namespace FairQueueService.Application.UseCases;

public sealed class LeaveQueueCommandHandler : ILeaveQueueUseCase
{
    private readonly IQueueRepository _repo;
    private readonly IAdvanceQueueUseCase _advance;
    private readonly IQueuePositionNotifier _notifier;

    public LeaveQueueCommandHandler(
        IQueueRepository repo,
        IAdvanceQueueUseCase advance,
        IQueuePositionNotifier notifier)
    {
        _repo = repo;
        _advance = advance;
        _notifier = notifier;
    }

    public async Task HandleAsync(long ticketId, string userId, CancellationToken ct = default)
    {
        var entry = await _repo.FindByUserAndTicketAsync(userId, ticketId, ct);
        if (entry is null || entry.Status is QueueStatus.Completed or QueueStatus.Cancelled or QueueStatus.TimedOut)
            return;

        var wasActive = entry.Status == QueueStatus.Active;
        entry.Cancel();
        await _repo.UpdateAsync(entry, ct);

        // Si era el activo, avanzar la cola al siguiente
        if (wasActive)
            await _advance.HandleAsync(ticketId, ct);
    }
}
