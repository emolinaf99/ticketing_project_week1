using System.Threading.Channels;
using FairQueueService.Application.DTOs;

namespace FairQueueService.Application.Interfaces;

public interface IQueuePositionSubscriber
{
    ChannelReader<QueueStatusUpdate> Subscribe(long ticketId, string userId);
    void Unsubscribe(long ticketId, string userId);
}
