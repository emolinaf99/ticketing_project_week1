using CrudService.Application.Dtos;
using CrudService.Domain.Entities;
using CrudService.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace CrudService.Application.Services;

/// <summary>
/// Servicio para gestionar tickets
/// </summary>
public interface ITicketService
{
    Task<IEnumerable<TicketDto>> GetTicketsByEventAsync(long eventId);
    Task<TicketDto?> GetTicketByIdAsync(long id);
    Task<IEnumerable<TicketDto>> CreateTicketsAsync(long eventId, int quantity, int priceCents = 0);
    Task<TicketDto> UpdateTicketStatusAsync(long id, string newStatus, string? reason = null);
    Task<TicketDto> ReleaseTicketAsync(long id, string? reason = null);
    Task<IEnumerable<TicketDto>> GetExpiredTicketsAsync();
}

/// <summary>
/// Implementación del servicio de tickets
/// </summary>
public class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketHistoryRepository _historyRepository;
    private readonly ILogger<TicketService> _logger;

    public TicketService(
        ITicketRepository ticketRepository,
        ITicketHistoryRepository historyRepository,
        ILogger<TicketService> logger)
    {
        _ticketRepository = ticketRepository;
        _historyRepository = historyRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<TicketDto>> GetTicketsByEventAsync(long eventId)
    {
        var tickets = await _ticketRepository.GetByEventIdAsync(eventId);
        return tickets.Select(MapToDto);
    }

    public async Task<TicketDto?> GetTicketByIdAsync(long id)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id);
        return ticket == null ? null : MapToDto(ticket);
    }

    public async Task<IEnumerable<TicketDto>> CreateTicketsAsync(long eventId, int quantity, int priceCents = 0)
    {
        var tickets = new List<Ticket>();

        for (int i = 0; i < quantity; i++)
        {
            var ticket = new Ticket
            {
                EventId = eventId,
                Status = TicketStatus.Available,
                PriceCents = priceCents
            };

            var created = await _ticketRepository.AddAsync(ticket);
            tickets.Add(created);
        }

        _logger.LogInformation("Se crearon {Quantity} tickets para evento {EventId}", quantity, eventId);
        return tickets.Select(MapToDto);
    }

    public async Task<TicketDto> UpdateTicketStatusAsync(long id, string newStatus, string? reason = null)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id);
        if (ticket == null)
            throw new KeyNotFoundException($"Ticket {id} no encontrado");

        var oldStatus = ticket.Status;

        // Convertir string a enum
        if (!Enum.TryParse<TicketStatus>(newStatus, ignoreCase: true, out var status))
            throw new ArgumentException($"Estado inválido: {newStatus}");

        ticket.Status = status;
        ticket.Version++;

        // Registrar en historial
        var history = new TicketHistory
        {
            TicketId = ticket.Id,
            OldStatus = oldStatus,
            NewStatus = status,
            Reason = reason
        };

        await _historyRepository.AddAsync(history);
        var updated = await _ticketRepository.UpdateAsync(ticket);

        _logger.LogInformation(
            "Ticket {TicketId} cambió de {OldStatus} a {NewStatus}",
            id, oldStatus, status);

        return MapToDto(updated);
    }

    public async Task<TicketDto> ReleaseTicketAsync(long id, string? reason = null)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id);
        if (ticket == null)
            throw new KeyNotFoundException($"Ticket {id} no encontrado");

        var oldStatus = ticket.Status;
        ticket.Status = TicketStatus.Available;
        ticket.ReservedAt = null;
        ticket.ExpiresAt = null;
        ticket.ReservedBy = null;
        ticket.OrderId = null;
        ticket.Version++;

        var history = new TicketHistory
        {
            TicketId = ticket.Id,
            OldStatus = oldStatus,
            NewStatus = TicketStatus.Available,
            Reason = reason ?? "Ticket liberado"
        };

        await _historyRepository.AddAsync(history);
        var updated = await _ticketRepository.UpdateAsync(ticket);

        _logger.LogInformation("Ticket {TicketId} liberado. Razón: {Reason}", id, reason ?? "No especificada");
        return MapToDto(updated);
    }

    public async Task<IEnumerable<TicketDto>> GetExpiredTicketsAsync()
    {
        var expiredTickets = await _ticketRepository.GetExpiredAsync(DateTime.UtcNow);
        return expiredTickets.Select(MapToDto);
    }

    private TicketDto MapToDto(Ticket ticket)
    {
        return new TicketDto
        {
            Id = ticket.Id,
            EventId = ticket.EventId,
            Status = ticket.Status.ToString(),
            ReservedAt = ticket.ReservedAt,
            ExpiresAt = ticket.ExpiresAt,
            PaidAt = ticket.PaidAt,
            OrderId = ticket.OrderId,
            ReservedBy = ticket.ReservedBy,
            PriceCents = ticket.PriceCents,
            Version = ticket.Version
        };
    }
}
