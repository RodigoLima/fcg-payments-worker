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
        _logger.LogInformation("=== INÍCIO PUBLICAÇÃO GAME PURCHASE COMPLETED ===");
        _logger.LogInformation("Evento recebido: PaymentId={PaymentId}, UserId={UserId}, GameId={GameId}, Amount={Amount}", 
            completedEvent.PaymentId, completedEvent.UserId, completedEvent.GameId, completedEvent.Amount);
        
        await PublishToQueueAsync("game-purchase-completed", completedEvent, cancellationToken);
        
        _logger.LogInformation("=== FIM PUBLICAÇÃO GAME PURCHASE COMPLETED ===");
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

            _logger.LogDebug("JSON serializado: {EventJson}", eventJson);

            // Codificar JSON em base64
            var eventJsonBytes = System.Text.Encoding.UTF8.GetBytes(eventJson);
            var eventJsonBase64 = Convert.ToBase64String(eventJsonBytes);

            _logger.LogInformation("JSON codificado em Base64: {Base64Length} caracteres", eventJsonBase64.Length);
            _logger.LogDebug("Conteúdo Base64: {Base64Content}", eventJsonBase64);

            // Verificar se é realmente Base64 válido
            try
            {
                var decodedBytes = Convert.FromBase64String(eventJsonBase64);
                var decodedJson = System.Text.Encoding.UTF8.GetString(decodedBytes);
                _logger.LogInformation("✓ Verificação Base64: Decodificação bem-sucedida, {DecodedLength} caracteres", decodedJson.Length);
            }
            catch (Exception decodeEx)
            {
                _logger.LogError(decodeEx, "✗ Erro na verificação Base64");
            }

            // Obter client da fila
            var queueClient = _queueClientFactory.GetQueueClient(queueName);
            
            // Publicar na fila (agora com conteúdo codificado em base64)
            await queueClient.SendMessageAsync(eventJsonBase64, cancellationToken);
            
            _logger.LogInformation("✓ Evento publicado com sucesso na fila {QueueName} (codificado em base64)", queueName);
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
