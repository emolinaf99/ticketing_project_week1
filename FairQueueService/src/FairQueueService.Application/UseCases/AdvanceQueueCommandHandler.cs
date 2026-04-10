using FairQueueService.Application.DTOs;
using FairQueueService.Application.Interfaces;
using FairQueueService.Domain.Entities;
using FairQueueService.Domain.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FairQueueService.Application.UseCases;

public sealed class AdvanceQueueCommandHandler : IAdvanceQueueUseCase
{
    private readonly IQueueRepository _repo;
    private readonly ITicketReservedPublisher _publisher;
    private readonly IQueuePositionNotifier _notifier;
    private readonly ILogger<AdvanceQueueCommandHandler> _logger;
    private readonly int _turnDurationSeconds;

    public AdvanceQueueCommandHandler(
        IQueueRepository repo,
        ITicketReservedPublisher publisher,
        IQueuePositionNotifier notifier,
        ILogger<AdvanceQueueCommandHandler> logger,
        IOptions<QueueSettings> settings)
    {
        _repo = repo;
        _publisher = publisher;
        _notifier = notifier;
        _logger = logger;
        _turnDurationSeconds = settings.Value.TurnDurationSeconds;
    }

    public async Task HandleAsync(long ticketId, CancellationToken ct = default)
    {
        // 1. Obtener el siguiente en cola (status=waiting, menor posición)
        var next = await _repo.GetNextWaitingAsync(ticketId, ct);
        if (next is null)
        {
            _logger.LogInformation("No more users waiting for ticket {TicketId}.", ticketId);
            return;
        }

        // 2. Activar turno
        next.ActivateTurn(_turnDurationSeconds);
        await _repo.UpdateAsync(next, ct);

        // 3. Disparar flujo de reserva existente vía RabbitMQ
        await _publisher.PublishAsync(ticketId, next.EventId, next.UserId, next.Email,
                                      _turnDurationSeconds, ct);

        // 4. Notificar SSE al nuevo usuario activo
        var activeUpdate = new QueueStatusUpdate(ticketId, next.UserId, next.Position,
            1, 0, "active", next.TurnExpiresAt);
        _notifier.NotifyOne(ticketId, next.UserId, activeUpdate);

        // 5. Notificar SSE a todos los que siguen esperando (posición bajó en 1)
        var waiting = await _repo.GetAllWaitingAsync(ticketId, ct);
        var updates = waiting.Select((e, i) => new QueueStatusUpdate(
            ticketId, e.UserId,
            e.Position,         // la posición en DB ya es la correcta
            waiting.Count + 1,  // +1 por el que acaba de activarse
            (e.Position - 1) * (_turnDurationSeconds / 2),
            "waiting", null));

        _notifier.NotifyAll(ticketId, updates);

        _logger.LogInformation("Turn activated for user {UserId} on ticket {TicketId}.",
                               next.UserId, ticketId);
    }
}
