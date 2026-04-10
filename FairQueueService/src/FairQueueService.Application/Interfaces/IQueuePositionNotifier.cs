using FairQueueService.Application.DTOs;

namespace FairQueueService.Application.Interfaces;

public interface IQueuePositionNotifier
{
    /// Notifica a TODOS los usuarios en cola de un ticket sus nuevas posiciones.
    void NotifyAll(long ticketId, IEnumerable<QueueStatusUpdate> updates);

    /// Notifica solo al usuario cuya entrada expiró o fue cancelada.
    void NotifyOne(long ticketId, string userId, QueueStatusUpdate update);
}
