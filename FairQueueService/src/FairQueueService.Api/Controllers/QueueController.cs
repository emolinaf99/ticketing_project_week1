using System.Security.Claims;
using System.Text.Json;
using FairQueueService.Application.DTOs;
using FairQueueService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairQueueService.Api.Controllers;

[ApiController]
[Route("api/queue")]
[Authorize]
public sealed class QueueController : ControllerBase
{
    private readonly IEnterQueueUseCase _enter;
    private readonly ILeaveQueueUseCase _leave;
    private readonly IGetQueuePositionUseCase _getPosition;
    private readonly IQueuePositionSubscriber _subscriber;

    public QueueController(
        IEnterQueueUseCase enter,
        ILeaveQueueUseCase leave,
        IGetQueuePositionUseCase getPosition,
        IQueuePositionSubscriber subscriber)
    {
        _enter = enter;
        _leave = leave;
        _getPosition = getPosition;
        _subscriber = subscriber;
    }

    /// <summary>Entrar a la cola de un ticket.</summary>
    [HttpPost("enter")]
    [ProducesResponseType(typeof(EnterQueueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Enter([FromBody] EnterQueueRequest request, CancellationToken ct)
    {
        var (userId, email) = ExtractClaims();
        var cmd = new EnterQueueCommand(request.EventId, request.TicketId, userId, email);
        var result = await _enter.HandleAsync(cmd, ct);
        return Ok(result);
    }

    /// <summary>Consultar posición actual en la cola.</summary>
    [HttpGet("{ticketId:long}/position")]
    [ProducesResponseType(typeof(QueuePositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPosition(long ticketId, CancellationToken ct)
    {
        var (userId, _) = ExtractClaims();
        var result = await _getPosition.HandleAsync(ticketId, userId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Salir de la cola voluntariamente.</summary>
    [HttpDelete("{ticketId:long}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Leave(long ticketId, CancellationToken ct)
    {
        var (userId, _) = ExtractClaims();
        await _leave.HandleAsync(ticketId, userId, ct);
        return NoContent();
    }

    /// <summary>SSE stream de posición en cola — conexión persistente.</summary>
    [HttpGet("{ticketId:long}/stream")]
    public async Task Stream(long ticketId, CancellationToken clientDisconnected)
    {
        var (userId, _) = ExtractClaims();

        Response.Headers["Content-Type"]      = "text/event-stream";
        Response.Headers["Cache-Control"]     = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        // Timeout de 5 minutos — suficiente para una cola razonable
        using var timeout  = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(clientDisconnected, timeout.Token);

        var reader = _subscriber.Subscribe(ticketId, userId);
        try
        {
            await foreach (var update in reader.ReadAllAsync(combined.Token))
            {
                var json = JsonSerializer.Serialize(update, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await Response.WriteAsync($"data: {json}\n\n", combined.Token);
                await Response.Body.FlushAsync(combined.Token);

                // Cerrar stream si el estado es final
                if (update.Status is "completed" or "cancelled" or "timed_out")
                    break;
            }
        }
        catch (OperationCanceledException) { /* desconexión o timeout — normal */ }
        finally
        {
            _subscriber.Unsubscribe(ticketId, userId);
        }
    }

    private (string userId, string email) ExtractClaims()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? throw new InvalidOperationException("Missing sub claim.");
        var email  = User.FindFirstValue(ClaimTypes.Email)
                  ?? User.FindFirstValue("email")
                  ?? string.Empty;
        return (userId, email);
    }
}

public sealed record EnterQueueRequest(long EventId, long TicketId);
