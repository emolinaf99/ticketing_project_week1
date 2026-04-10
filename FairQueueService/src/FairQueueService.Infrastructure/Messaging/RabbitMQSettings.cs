namespace FairQueueService.Infrastructure.Messaging;

public sealed class RabbitMQSettings
{
    public const string SectionName = "RabbitMQ";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string Username { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";
    public string ExchangeName { get; init; } = "tickets";
    public string TicketReservedRoutingKey { get; init; } = "ticket.reserved";
    public string ReleasedQueueName { get; init; } = "q.ticket.released.queue";
}
