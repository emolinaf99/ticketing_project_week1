using Microsoft.AspNetCore.Mvc;
using Producer.Api.Models;
using Producer.Application.DTOs.RequestPayment;
using Producer.Application.Interfaces;

namespace Producer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IRequestPaymentUseCase _handler;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IRequestPaymentUseCase handler,
        ILogger<PaymentsController> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessPayment(
        [FromBody] ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { message = "La solicitud no puede estar vacía" });

        if (request.TicketId <= 0)
            return BadRequest(new { message = "TicketId debe ser mayor a 0" });

        if (request.EventId <= 0)
            return BadRequest(new { message = "EventId debe ser mayor a 0" });

        if (request.AmountCents < 0)
            return BadRequest(new { message = "AmountCents no puede ser negativo" });

        if (string.IsNullOrWhiteSpace(request.PaymentBy))
            return BadRequest(new { message = "PaymentBy es requerido" });

        if (string.IsNullOrWhiteSpace(request.PaymentMethodId))
            return BadRequest(new { message = "PaymentMethodId es requerido" });

        try
        {
            var command = new RequestPaymentCommand(
                request.TicketId,
                request.EventId,
                request.AmountCents,
                request.Currency,
                request.PaymentBy,
                request.PaymentMethodId,
                request.TransactionRef);

            var response = await _handler.HandleAsync(command, cancellationToken);

            _logger.LogInformation(
                "Payment request published. TicketId={TicketId}, EventId={EventId}",
                request.TicketId,
                request.EventId);

            return Accepted(new
            {
                message = response.Message,
                ticketId = response.TicketId,
                eventId = request.EventId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error publishing payment request. TicketId={TicketId}",
                request.TicketId);

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Error al encolar la solicitud de pago"
            });
        }
    }
}
