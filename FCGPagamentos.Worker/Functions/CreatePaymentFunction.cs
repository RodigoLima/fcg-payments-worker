using System.Text.Json;
using FCGPagamentos.Worker.Models;
using FCGPagamentos.Worker.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FCGPagamentos.Worker.Functions;

public class CreatePaymentFunction
{
    private readonly IPaymentService _paymentService;
    private readonly IObservabilityService _observabilityService;
    private readonly ILogger<CreatePaymentFunction> _logger;

    public CreatePaymentFunction(
        IPaymentService paymentService,
        IObservabilityService observabilityService,
        ILogger<CreatePaymentFunction> logger)
    {
        _paymentService = paymentService;
        _observabilityService = observabilityService;
        _logger = logger;
    }

    [Function("CreatePaymentFunction")]
    public async Task Run(
        [QueueTrigger("game-purchase-requested", Connection = "AzureWebJobsStorage")] string message,
        CancellationToken cancellationToken)
    {
        GamePurchaseRequestedEvent? purchaseEvent = null;
        
        try
        {
            _logger.LogInformation("Iniciando criação de pagamento");
            
            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogError("Mensagem da fila está vazia ou nula");
                return;
            }

            // Deserializar evento
            try
            {
                purchaseEvent = JsonSerializer.Deserialize<GamePurchaseRequestedEvent>(message);
                if (purchaseEvent == null)
                {
                    _logger.LogError("Falha ao deserializar evento de compra. Mensagem: {Message}", message);
                    return;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erro ao deserializar evento de compra. Mensagem: {Message}", message);
                return;
            }

            // Log detalhado do evento recebido
            _logger.LogDebug("Evento deserializado: PaymentId={PaymentId}, CorrelationId={CorrelationId}, UserId={UserId}, GameId={GameId}, Amount={Amount}, Currency={Currency}, PaymentMethod={PaymentMethod}", 
                purchaseEvent.PaymentId, purchaseEvent.CorrelationId, purchaseEvent.UserId, purchaseEvent.GameId, purchaseEvent.Amount, purchaseEvent.Currency, purchaseEvent.PaymentMethod);

            // Configurar correlation ID para traces distribuídos
            _observabilityService.SetCorrelationId(purchaseEvent.CorrelationId);

            _logger.LogInformation("Criando pagamento {PaymentId} (CorrelationId: {CorrelationId}) para usuário {UserId} no jogo {GameId}", 
                purchaseEvent.PaymentId, purchaseEvent.CorrelationId, purchaseEvent.UserId, purchaseEvent.GameId);

            // Criar pagamento (API vai publicar PaymentCreated + PaymentQueued)
            var success = await _paymentService.CreatePaymentAsync(purchaseEvent, cancellationToken);
            
            if (success)
            {
                _logger.LogInformation("Pagamento {PaymentId} criado com sucesso", purchaseEvent.PaymentId);
            }
            else
            {
                _logger.LogWarning("Falha na criação do pagamento {PaymentId}", purchaseEvent.PaymentId);
            }
        }
        catch (Exception ex)
        {
            var paymentId = purchaseEvent?.PaymentId.ToString() ?? "unknown";
            var correlationId = purchaseEvent?.CorrelationId.ToString() ?? "unknown";
            
            _logger.LogError(ex, "Erro inesperado ao criar pagamento. PaymentId: {PaymentId}, CorrelationId: {CorrelationId}, Mensagem: {Message}", 
                paymentId, correlationId, message);
        }
    }
}
