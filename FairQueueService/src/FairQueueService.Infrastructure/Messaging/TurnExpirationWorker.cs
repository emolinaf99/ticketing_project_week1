using FairQueueService.Application.DTOs;
using FairQueueService.Application.Interfaces;
using FairQueueService.Domain.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FairQueueService.Infrastructure.Messaging;

/// BackgroundService que verifica cada 10s si algún turno activo expiró
/// y avanza la cola automáticamente.
public sealed class TurnExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IQueuePositionNotifier _notifier;
    private readonly ILogger<TurnExpirationWorker> _logger;

    public TurnExpirationWorker(
        IServiceScopeFactory scopeFactory,
        IQueuePositionNotifier notifier,
        ILogger<TurnExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            await CheckExpiredTurnsAsync(stoppingToken);
        }
    }

    private async Task CheckExpiredTurnsAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo    = scope.ServiceProvider.GetRequiredService<IQueueRepository>();
            var advance = scope.ServiceProvider.GetRequiredService<IAdvanceQueueUseCase>();

            var expired = await repo.GetExpiredActiveEntriesAsync(ct);
            foreach (var entry in expired)
            {
                _logger.LogInformation("Turn expired for user {UserId} on ticket {TicketId}.",
                                       entry.UserId, entry.TicketId);

                entry.TimedOut();
                await repo.UpdateAsync(entry, ct);

                // Notificar SSE al usuario que expiró
                var timedOutUpdate = new QueueStatusUpdate(
                    entry.TicketId, entry.UserId, entry.Position, 0, 0, "timed_out", null);
                _notifier.NotifyOne(entry.TicketId, entry.UserId, timedOutUpdate);

                // Avanzar la cola
                await advance.HandleAsync(entry.TicketId, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error checking expired turns.");
        }
    }
}
