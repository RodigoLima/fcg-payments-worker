using FCGPagamentos.Worker.Models;
using Microsoft.Extensions.Logging;

namespace FCGPagamentos.Worker.Services;

public class PaymentService : IPaymentService
{
    private const string ApprovedStatus = "approved";
    private const string DeclinedStatus = "declined";
    private const string ApprovedReason = "Pagamento aprovado pelo provedor";
    private const string DeclinedReason = "Pagamento recusado pelo provedor";
    private const string DataInconsistencyError = "Dados inconsistentes entre mensagem e API";

    private readonly IPaymentsApiClient _apiClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly IObservabilityService _observabilityService;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPaymentsApiClient apiClient,
        IEventPublisher eventPublisher,
        IObservabilityService observabilityService,
        ILogger<PaymentService> logger)
    {
        _apiClient = apiClient;
        _eventPublisher = eventPublisher;
        _observabilityService = observabilityService;
        _logger = logger;
    }

    public async Task<bool> ProcessPaymentAsync(PaymentRequestedMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processando pagamento {PaymentId} para usuário {UserId}", message.PaymentId, message.UserId);
        
        if (!ValidateMessage(message, out var validationError))
        {
            _logger.LogError("Mensagem inválida: {Error}", validationError);
            await PublishFailureAsync(message, validationError, cancellationToken);
            return false;
        }

        _observabilityService.SetCorrelationId(message.CorrelationId);
        using var activity = _observabilityService.StartPaymentProcessingActivity(message.PaymentId, message.CorrelationId);
        
        try
        {
            // 1. Validar e normalizar dados
            if (!ValidateAndNormalizePaymentData(message, null))
            {
                await PublishFailureAsync(message, DataInconsistencyError, cancellationToken);
                return false;
            }

            // 2. Marcar como processando
            await _apiClient.MarkProcessingAsync(message.PaymentId, cancellationToken);
            await _eventPublisher.PublishPaymentProcessingAsync(message.PaymentId, message.CorrelationId, cancellationToken);

            // 3. Processar pagamento
            var (isApproved, providerResponse, reason) = ProcessPaymentDecision(message.Amount);
            
            // 4. Atualizar status e emitir evento
            await UpdatePaymentStatusAsync(message, isApproved, providerResponse, reason, cancellationToken);

            // 5. Sempre publicar GamePurchaseCompleted independente do resultado
            await PublishGamePurchaseCompletedAsync(message, isApproved, providerResponse, reason, cancellationToken);

            _logger.LogInformation("Pagamento {PaymentId} processado: {Status}", message.PaymentId, providerResponse);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar pagamento {PaymentId}", message.PaymentId);
            await _apiClient.MarkFailedAsync(message.PaymentId, ex.Message, cancellationToken);
            await PublishFailureAsync(message, ex.Message, cancellationToken);
            
            // Sempre publicar GamePurchaseCompleted mesmo em caso de falha
            await PublishGamePurchaseCompletedAsync(message, false, "failed", ex.Message, cancellationToken);
            
            return false;
        }
    }

    private static (bool isApproved, string response, string reason) ProcessPaymentDecision(decimal amount)
    {
        var isApproved = (int)(amount * 100) % 2 == 0;
        return isApproved 
            ? (true, ApprovedStatus, ApprovedReason)
            : (false, DeclinedStatus, DeclinedReason);
    }

    private static bool ValidateMessage(PaymentRequestedMessage message, out string error)
    {
        if (message == null)
        {
            error = "Mensagem é nula";
            return false;
        }

        error = message switch
        {
            { PaymentId: var id } when id == Guid.Empty => "PaymentId não pode ser vazio",
            { CorrelationId: var id } when id == Guid.Empty => "CorrelationId não pode ser vazio",
            { UserId: var id } when id == Guid.Empty => "UserId não pode ser vazio",
            { GameId: null or "" } => "GameId não pode ser vazio",
            { Amount: <= 0 } => "Amount deve ser maior que zero",
            { Currency: null or "" } => "Currency não pode ser vazio",
            _ => string.Empty
        };
        
        return string.IsNullOrEmpty(error);
    }

    private bool ValidateAndNormalizePaymentData(PaymentRequestedMessage message, Payment? payment)
    {
        // Como não estamos mais carregando da API, apenas validamos se os dados da mensagem estão consistentes
        // Esta validação pode ser expandida conforme necessário
        return true;
    }

    private async Task PublishFailureAsync(PaymentRequestedMessage message, string reason, CancellationToken cancellationToken)
    {
        await _eventPublisher.PublishPaymentFailedAsync(message.PaymentId, message.CorrelationId, reason, cancellationToken);
    }

    private async Task PublishGamePurchaseCompletedAsync(PaymentRequestedMessage message, bool isApproved, string status, string reason, CancellationToken cancellationToken)
    {
        var completedEvent = new GamePurchaseCompletedEvent(
            PaymentId: message.PaymentId,
            UserId: message.UserId,
            GameId: message.GameId,
            Amount: message.Amount,
            Currency: message.Currency,
            PaymentMethod: message.PaymentMethod,
            Status: status,
            Reason: reason,
            CorrelationId: message.CorrelationId,
            CompletedAt: DateTime.UtcNow
        );

        await _eventPublisher.PublishGamePurchaseCompletedAsync(completedEvent, cancellationToken);
    }

    private async Task UpdatePaymentStatusAsync(PaymentRequestedMessage message, bool isApproved, string providerResponse, string reason, CancellationToken cancellationToken)
    {
        if (isApproved)
        {
            await _apiClient.MarkApprovedAsync(message.PaymentId, providerResponse, cancellationToken);
            await _eventPublisher.PublishPaymentApprovedAsync(message.PaymentId, message.CorrelationId, providerResponse, cancellationToken);
        }
        else
        {
            await _apiClient.MarkDeclinedAsync(message.PaymentId, providerResponse, reason, cancellationToken);
            await _eventPublisher.PublishPaymentDeclinedAsync(message.PaymentId, message.CorrelationId, reason, cancellationToken);
        }
    }

    public async Task<bool> CreatePaymentAsync(GamePurchaseRequestedEvent purchaseEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Criando pagamento {PaymentId} para usuário {UserId}", purchaseEvent.PaymentId, purchaseEvent.UserId);
        
        _observabilityService.SetCorrelationId(purchaseEvent.CorrelationId);
        using var activity = _observabilityService.StartPaymentProcessingActivity(purchaseEvent.PaymentId, purchaseEvent.CorrelationId);
        
        try
        {
            // Validar evento
            if (!ValidatePurchaseEvent(purchaseEvent, out var validationError))
            {
                _logger.LogError("Evento de compra inválido: {Error}", validationError);
                return false;
            }

            // Criar pagamento na API (API vai publicar PaymentCreated + PaymentQueued)
            var createRequest = new CreatePaymentRequest(
                PaymentId: purchaseEvent.PaymentId,
                UserId: purchaseEvent.UserId,
                GameId: purchaseEvent.GameId,
                Amount: purchaseEvent.Amount,
                Currency: purchaseEvent.Currency,
                PaymentMethod: purchaseEvent.PaymentMethod,
                CorrelationId: purchaseEvent.CorrelationId
            );

            var payment = await _apiClient.CreatePaymentAsync(createRequest, cancellationToken);
            if (payment == null)
            {
                _logger.LogError("Falha ao criar pagamento {PaymentId} na API", purchaseEvent.PaymentId);
                return false;
            }

            _logger.LogInformation("Pagamento {PaymentId} criado com sucesso", purchaseEvent.PaymentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar pagamento {PaymentId}", purchaseEvent.PaymentId);
            return false;
        }
    }

    private static bool ValidatePurchaseEvent(GamePurchaseRequestedEvent purchaseEvent, out string error)
    {
        if (purchaseEvent == null)
        {
            error = "Evento de compra é nulo";
            return false;
        }

        error = purchaseEvent switch
        {
            { PaymentId: var id } when id == Guid.Empty => "PaymentId não pode ser vazio",
            { CorrelationId: var id } when id == Guid.Empty => "CorrelationId não pode ser vazio",
            { UserId: var id } when id == Guid.Empty => "UserId não pode ser vazio",
            { GameId: null or "" } => "GameId não pode ser vazio",
            { Amount: <= 0 } => "Amount deve ser maior que zero",
            { Currency: null or "" } => "Currency não pode ser vazio",
            { PaymentMethod: null or "" } => "PaymentMethod não pode ser vazio",
            _ => string.Empty
        };
        
        return string.IsNullOrEmpty(error);
    }
}
