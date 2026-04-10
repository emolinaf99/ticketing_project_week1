namespace FairQueueService.Application.DTOs;

/// Settings de la cola inyectados vía IOptions<QueueSettings>.
/// Permite que Application no dependa de IConfiguration directamente.
public sealed class QueueSettings
{
    public const string SectionName = "QueueSettings";
    public int TurnDurationSeconds { get; init; } = 180;
}
