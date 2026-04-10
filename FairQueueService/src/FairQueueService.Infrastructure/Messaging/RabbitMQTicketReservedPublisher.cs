using System.Text;
using System.Text.Json;
using FairQueueService.Domain.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FairQueueService.Infrastructure.Messaging;

public sealed class RabbitMQTicketReservedPublisher : ITicketReservedPublisher, IDisposable
{
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<RabbitMQTicketReservedPublisher> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMQTicketReservedPublisher(
        IOptions<RabbitMQSettings> options,
        ILogger<RabbitMQTicketReservedPublisher> logger)
    {
        _settings = options.Value;
        _logger = logger;
        Connect();
    }

    private void Connect()
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.Host,
            Port = _settings.Port,
            UserName = _settings.Username,
            Password = _settings.Password,
            VirtualHost = _settings.VirtualHost
        };
        _connection = factory.CreateConnection("FairQueueService-Publisher");
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(_settings.ExchangeName, ExchangeType.Topic, durable: true);
    }

    public Task PublishAsync(long ticketId, long eventId, string userId, string email,
                             int turnDurationSeconds, CancellationToken ct = default)
    {
        var payload = new
        {
            ticketId,
            eventId,
            orderId = $"FQ-{userId[..Math.Min(8, userId.Length)]}",
            reservedBy = email,
            reservationDurationSeconds = turnDurationSeconds,
            publishedAt = DateTime.UtcNow
        };

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        var props = _channel!.CreateBasicProperties();
        props.DeliveryMode = 2; // persistent
        props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        _channel.BasicPublish(
            exchange: _settings.ExchangeName,
            routingKey: _settings.TicketReservedRoutingKey,
            basicProperties: props,
            body: body);

        _logger.LogInformation("Published ticket.reserved for ticket {TicketId} user {UserId}.", ticketId, userId);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}
