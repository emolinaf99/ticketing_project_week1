using FairQueueService.Application.DTOs;
using FairQueueService.Application.Interfaces;
using FairQueueService.Domain.Entities;
using FairQueueService.Domain.Ports;
using Microsoft.Extensions.Options;

namespace FairQueueService.Application.UseCases;

public sealed class EnterQueueCommandHandler : IEnterQueueUseCase
{
    private readonly IQueueRepository _repo;
    private readonly ITicketReservedPublisher _publisher;
    private readonly IQueuePositionNotifier _notifier;
    private readonly int _turnDurationSeconds;

    public EnterQueueCommandHandler(
        IQueueRepository repo,
        ITicketReservedPublisher publisher,
        IQueuePositionNotifier notifier,
        IOptions<QueueSettings> settings)
    {
        _repo = repo;
        _publisher = publisher;
        _notifier = notifier;
        _turnDurationSeconds = settings.Value.TurnDurationSeconds;
    }

    public async Task<EnterQueueResponse> HandleAsync(EnterQueueCommand cmd, CancellationToken ct = default)
    {
        // Idempotencia: si ya está en cola, retornar posición actual
        var existing = await _repo.FindByUserAndTicketAsync(cmd.UserId, cmd.TicketId, ct);
        if (existing is not null && existing.Status is QueueStatus.Waiting or QueueStatus.Active)
        {
            var total = await _repo.CountActivePositionsAsync(cmd.TicketId, ct);
            return new EnterQueueResponse(
                cmd.TicketId,
                existing.Position,
                total,
                CalculateWait(existing.Position, total),
                existing.Status.ToString().ToLowerInvariant(),
                existing.TurnExpiresAt);
        }

        // Calcular siguiente posición
        var currentCount = await _repo.CountActivePositionsAsync(cmd.TicketId, ct);
        var newPosition = currentCount + 1;

        var entry = QueueEntry.Create(cmd.EventId, cmd.TicketId, cmd.UserId, cmd.Email, newPosition);

        // Si es el primero: activar turno inmediatamente
        if (newPosition == 1)
        {
            entry.ActivateTurn(_turnDurationSeconds);
            await _repo.AddAsync(entry, ct);

            // Disparar el flujo de reserva existente vía RabbitMQ
            await _publisher.PublishAsync(cmd.TicketId, cmd.EventId, cmd.UserId, cmd.Email,
                                          _turnDurationSeconds, ct);

            return new EnterQueueResponse(cmd.TicketId, 1, 1, 0, "active", entry.TurnExpiresAt);
        }

        await _repo.AddAsync(entry, ct);
        return new EnterQueueResponse(
            cmd.TicketId,
            newPosition,
            newPosition,
            CalculateWait(newPosition, newPosition),
            "waiting",
            null);
    }

    // Estimación simple: cada turno dura turnDurationSeconds / 2 en promedio
    private int CalculateWait(int position, int total) =>
        (position - 1) * (_turnDurationSeconds / 2);
}
