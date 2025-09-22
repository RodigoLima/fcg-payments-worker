using Azure.Storage.Queues;

namespace FCGPagamentos.Worker.Services;

public class QueueClientFactory : IQueueClientFactory
{
    private readonly string _connectionString;
    private readonly Dictionary<string, QueueClient> _clients = new();

    public QueueClientFactory(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public QueueClient GetQueueClient(string queueName)
    {
        if (string.IsNullOrEmpty(queueName))
            throw new ArgumentException("Queue name cannot be null or empty", nameof(queueName));

        // Cache dos clients para reutilização
        if (!_clients.ContainsKey(queueName))
        {
            var client = new QueueClient(_connectionString, queueName);
            _clients[queueName] = client;
        }

        return _clients[queueName];
    }
}
