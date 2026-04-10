namespace CrudService.Domain.Entities;

using NpgsqlTypes;

/// <summary>
/// Estados posibles de un ticket
/// </summary>
public enum TicketStatus
{
    [PgName("available")]
    Available,
    [PgName("reserved")]
    Reserved,
    [PgName("paid")]
    Paid,
    [PgName("released")]
    Released,
    [PgName("cancelled")]
    Cancelled
}

/// <summary>
/// Representa un ticket en el sistema
/// </summary>
public class Ticket
{
    /// <summary>
    /// ID único del ticket
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// ID del evento al que pertenece
    /// </summary>
    public long EventId { get; set; }

    /// <summary>
    /// Estado actual del ticket
    /// </summary>
    public TicketStatus Status { get; set; } = TicketStatus.Available;

    /// <summary>
    /// Fecha y hora de la reserva
    /// </summary>
    public DateTime? ReservedAt { get; set; }

    /// <summary>
    /// Fecha y hora de expiración de la reserva
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Fecha y hora del pago
    /// </summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>
    /// ID de la orden asociada
    /// </summary>
    public string? OrderId { get; set; }

    /// <summary>
    /// Usuario que reservó el ticket
    /// </summary>
    public string? ReservedBy { get; set; }

    /// <summary>
    /// Versión del ticket (para control de concurrencia)
    /// </summary>
    /// <summary>
    /// Precio del ticket en centavos (ej. 5000 = $50.00 USD)
    /// </summary>
    public int PriceCents { get; set; } = 0;

    public int Version { get; set; } = 0;

    /// <summary>
    /// Evento asociado
    /// </summary>
    public Event Event { get; set; } = null!;

    /// <summary>
    /// Pagos asociados al ticket
    /// </summary>
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    /// <summary>
    /// Historial de cambios del ticket
    /// </summary>
    public ICollection<TicketHistory> History { get; set; } = new List<TicketHistory>();
}
