using FCGPagamentos.Worker.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Azure.Storage.Queues;

namespace FCGPagamentos.Worker.Services;

public class EventPublisher : IEventPublisher
{
    private readonly ILogger<EventPublisher> _logger;
    private readonly IQueueClientFactory _queueClientFactory;

    public EventPublisher(ILogger<EventPublisher> logger, IQueueClientFactory queueClientFactory)
    {
        _logger = logger;
        _queueClientFactory = queueClientFactory;
    }

    public async Task PublishPaymentProcessingAsync(Guid paymentId, Guid correlationId, CancellationToken cancellationToken = default)
    {
        await PublishEventAsync("PaymentProcessing", paymentId, correlationId, null, cancellationToken);
    }

    public async Task PublishPaymentApprovedAsync(Guid paymentId, Guid correlationId, string providerResponse, CancellationToken cancellationToken = default)
    {
        await PublishEventAsync("PaymentApproved", paymentId, correlationId, providerResponse, cancellationToken);
    }

    public async Task PublishPaymentDeclinedAsync(Guid paymentId, Guid correlationId, string reason, CancellationToken cancellationToken = default)
    {
        await PublishEventAsync("PaymentDeclined", paymentId, correlationId, reason, cancellationToken);
    }

    public async Task PublishPaymentFailedAsync(Guid paymentId, Guid correlationId, string reason, CancellationToken cancellationToken = default)
    {
        await PublishEventAsync("PaymentFailed", paymentId, correlationId, reason, cancellationToken);
    }

    public async Task PublishGamePurchaseCompletedAsync(GamePurchaseCompletedEvent completedEvent, CancellationToken cancellationToken = default)
    {
        await PublishToQueueAsync("game-purchase-completed", completedEvent, cancellationToken);
    }

    // Método genérico para publicar em qualquer fila
    public async Task PublishToQueueAsync<T>(string queueName, T eventData, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Publicando evento na fila {QueueName}", queueName);

            // Serializar evento para JSON
            var eventJson = JsonSerializer.Serialize(eventData, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false // Compacto para filas
            });

            // Obter client da fila
            var queueClient = _queueClientFactory.GetQueueClient(queueName);
            
            // Publicar na fila
            await queueClient.SendMessageAsync(eventJson, cancellationToken);
            
            _logger.LogInformation("Evento publicado com sucesso na fila {QueueName}", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar evento na fila {QueueName}", queueName);
            throw;
        }
    }

    private async Task PublishEventAsync(string eventType, Guid paymentId, Guid correlationId, string? data, CancellationToken cancellationToken)
    {
        try
        {
            var paymentEvent = new PaymentEvent(paymentId, correlationId, eventType, DateTime.UtcNow, data);
            
            _logger.LogInformation("Publicando evento {EventType}: PaymentId={PaymentId}, CorrelationId={CorrelationId}", 
                eventType, paymentId, correlationId);

            // Por enquanto, apenas logamos o evento
            _logger.LogInformation("Evento publicado: {Event}", paymentEvent);
            
            await Task.CompletedTask; // Simular operação assíncrona
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar evento {EventType} para PaymentId={PaymentId}", eventType, paymentId);
            throw;
        }
    }
}
