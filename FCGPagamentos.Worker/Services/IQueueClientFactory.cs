using Azure.Storage.Queues;

namespace FCGPagamentos.Worker.Services;

public interface IQueueClientFactory
{
    QueueClient GetQueueClient(string queueName);
}
