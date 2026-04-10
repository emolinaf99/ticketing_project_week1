using System.Text;
using System.Text.Json;
using FairQueueService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FairQueueService.Infrastructure.Messaging;

/// Consume de q.ticket.released.queue (binding a ticket.status.changed).
/// Cuando detecta status="released", avanza la cola para ese ticket.
public sealed class TicketReleasedConsumer : BackgroundService
{
    private readonly RabbitMQSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TicketReleasedConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public TicketReleasedConsumer(
        IOptions<RabbitMQSettings> options,
        IServiceScopeFactory scopeFactory,
        ILogger<TicketReleasedConsumer> logger)
    {
        _settings = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ConnectAndConsume(stoppingToken);
        return Task.CompletedTask;
    }

    private void ConnectAndConsume(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 24; attempt++)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _settings.Host,
                    Port = _settings.Port,
                    UserName = _settings.Username,
                    Password = _settings.Password,
                    VirtualHost = _settings.VirtualHost,
                    DispatchConsumersAsync = true
                };
                _connection = factory.CreateConnection("FairQueueService-Consumer");
                _channel = _connection.CreateModel();
                _channel.BasicQos(0, 10, false);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.Received += OnMessageAsync;
                _channel.BasicConsume(_settings.ReleasedQueueName, autoAck: false, consumer);
                _logger.LogInformation("TicketReleasedConsumer connected to {Queue}.", _settings.ReleasedQueueName);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("RabbitMQ not ready (attempt {N}/24): {Msg}", attempt + 1, ex.Message);
                Thread.Sleep(5000);
            }
        }
        _logger.LogError("Could not connect to RabbitMQ after 24 attempts.");
    }

    private async Task OnMessageAsync(object sender, BasicDeliverEventArgs args)
    {
        try
        {
            var json = Encoding.UTF8.GetString(args.Body.ToArray());
            var payload = JsonSerializer.Deserialize<StatusChangedPayload>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload is null || !string.Equals(payload.NewStatus, "released", StringComparison.OrdinalIgnoreCase))
            {
                _channel?.BasicAck(args.DeliveryTag, false);
                return;
            }

            _logger.LogInformation("Ticket {TicketId} released — advancing fair queue.", payload.TicketId);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var advance = scope.ServiceProvider.GetRequiredService<IAdvanceQueueUseCase>();
            await advance.HandleAsync(payload.TicketId);

            _channel?.BasicAck(args.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing released ticket message.");
            _channel?.BasicNack(args.DeliveryTag, false, requeue: true);
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }

    private sealed record StatusChangedPayload(long TicketId, string NewStatus, DateTime ChangedAt);
}
