using FCGPagamentos.Worker.Models;
using Microsoft.Extensions.Logging;

namespace FCGPagamentos.Worker.Services;

public class EventPublisher : IEventPublisher
{
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(ILogger<EventPublisher> logger)
    {
        _logger = logger;
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

    private async Task PublishEventAsync(string eventType, Guid paymentId, Guid correlationId, string? data, CancellationToken cancellationToken)
    {
        try
        {
            var paymentEvent = new PaymentEvent(paymentId, correlationId, eventType, DateTime.UtcNow, data);
            
            _logger.LogInformation("Publicando evento {EventType}: PaymentId={PaymentId}, CorrelationId={CorrelationId}", 
                eventType, paymentId, correlationId);

            // TODO: Implementar publicação real do evento (Service Bus, Event Grid, etc.)
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
