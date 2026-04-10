using CrudService.Domain.Entities;

namespace CrudService.Application.Dtos;

/// <summary>
/// DTO para respuesta de ticket
/// </summary>
public class TicketDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? ReservedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? OrderId { get; set; }
    public string? ReservedBy { get; set; }
    public int PriceCents { get; set; }
    public int Version { get; set; }
}

public class CreateTicketRequest
{
    public long EventId { get; set; }
    public int Quantity { get; set; } = 1;
    public int PriceCents { get; set; } = 0;
}

public class UpdateTicketStatusRequest
{
    public string NewStatus { get; set; } = null!;
    public string? Reason { get; set; }
}
