using FCGPagamentos.Worker.Models;
using Microsoft.Extensions.Logging;

namespace FCGPagamentos.Worker.Services;

public class PaymentService : IPaymentService
{
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
        // Validar mensagem

        _logger.LogInformation("Mensagem recebida: {Message}", message);
        if (!ValidateMessage(message, out var validationError))
        {
            _logger.LogError("Mensagem inválida recebida: {Error}", validationError);
            await _eventPublisher.PublishPaymentFailedAsync(message.PaymentId, message.CorrelationId, validationError, cancellationToken);
            return false;
        }

        // Configurar correlation ID para traces distribuídos
        _observabilityService.SetCorrelationId(message.CorrelationId);
        
        // Iniciar activity para tracing distribuído
        using var activity = _observabilityService.StartPaymentProcessingActivity(message.PaymentId, message.CorrelationId);
        
        try
        {
            _logger.LogInformation("Iniciando processamento do pagamento {PaymentId} (CorrelationId: {CorrelationId}) para usuário {UserId} no jogo {GameId}", 
                message.PaymentId, message.CorrelationId, message.UserId, message.GameId);

            // 1. Carregar payment via API
            var payment = await _apiClient.GetPaymentAsync(message.PaymentId, cancellationToken);
            if (payment == null)
            {
                _logger.LogError("Pagamento {PaymentId} não encontrado na API", message.PaymentId);
                await _eventPublisher.PublishPaymentFailedAsync(message.PaymentId, message.CorrelationId, "Pagamento não encontrado", cancellationToken);
                return false;
            }

            // Log detalhado dos dados para debug
            _logger.LogDebug("Dados do pagamento {PaymentId}: Mensagem - UserId={MessageUserId}, GameId={MessageGameId}. API - UserId={ApiUserId}, GameId={ApiGameId}", 
                message.PaymentId, message.UserId, message.GameId, payment.UserId, payment.GameId);

            // Validar consistência dos dados (mais flexível)
            var hasUserIdMismatch = payment.UserId != Guid.Empty && payment.UserId != message.UserId;
            var hasGameIdMismatch = payment.GameId != Guid.Empty && payment.GameId != message.GameId;
            
            if (hasUserIdMismatch || hasGameIdMismatch)
            {
                _logger.LogError("Inconsistência nos dados do pagamento {PaymentId}. Mensagem: UserId={MessageUserId}, GameId={MessageGameId}. API: UserId={ApiUserId}, GameId={ApiGameId}", 
                    message.PaymentId, message.UserId, message.GameId, payment.UserId, payment.GameId);
                await _eventPublisher.PublishPaymentFailedAsync(message.PaymentId, message.CorrelationId, "Dados inconsistentes entre mensagem e API", cancellationToken);
                return false;
            }

            // Se a API retornou GUIDs zerados, usar os dados da mensagem (cenário comum em desenvolvimento/teste)
            if (payment.UserId == Guid.Empty || payment.GameId == Guid.Empty)
            {
                _logger.LogWarning("API retornou GUIDs zerados para pagamento {PaymentId}. Usando dados da mensagem: UserId={MessageUserId}, GameId={MessageGameId}", 
                    message.PaymentId, message.UserId, message.GameId);
                
                // Atualizar os dados do payment com os da mensagem para continuar o processamento
                payment.UserId = message.UserId;
                payment.GameId = message.GameId;
            }

            // 2. Marcar como processando via API
            await _apiClient.MarkProcessingAsync(message.PaymentId, cancellationToken);
            await _eventPublisher.PublishPaymentProcessingAsync(message.PaymentId, message.CorrelationId, cancellationToken);

            // 3. Simular provedor (valor par = aprovado, ímpar = recusado)
            var isApproved = SimulateProviderDecision(payment.Amount);
            var providerResponse = isApproved ? "approved" : "declined";
            var reason = isApproved ? "Pagamento aprovado pelo provedor" : "Pagamento recusado pelo provedor";

            _logger.LogInformation("Simulação do provedor para pagamento {PaymentId}: {Result} (valor: {Amount}) para usuário {UserId} no jogo {GameId}", 
                message.PaymentId, providerResponse, payment.Amount, payment.UserId, payment.GameId);

            // 4. Atualizar status via API e emitir evento
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

            _logger.LogInformation("Pagamento {PaymentId} processado com sucesso: {Status} para usuário {UserId} no jogo {GameId}", 
                message.PaymentId, providerResponse, payment.UserId, payment.GameId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao processar pagamento {PaymentId} (CorrelationId: {CorrelationId}) para usuário {UserId} no jogo {GameId}", 
                message.PaymentId, message.CorrelationId, message.UserId, message.GameId);
            
            // 5. Marcar como falhou via API e emitir evento
            await _apiClient.MarkFailedAsync(message.PaymentId, ex.Message, cancellationToken);
            await _eventPublisher.PublishPaymentFailedAsync(message.PaymentId, message.CorrelationId, ex.Message, cancellationToken);
            
            return false;
        }
    }

    private static bool SimulateProviderDecision(decimal amount)
    {
        // Regra simples: valor par = aprovado, ímpar = recusado
        return (int)(amount * 100) % 2 == 0;
    }

    private static bool ValidateMessage(PaymentRequestedMessage message, out string error)
    {
        error = string.Empty;

        if (message == null)
        {
            error = "Mensagem é nula";
            return false;
        }

        if (message.PaymentId == Guid.Empty)
        {
            error = "PaymentId não pode ser vazio";
            return false;
        }

        if (message.CorrelationId == Guid.Empty)
        {
            error = "CorrelationId não pode ser vazio";
            return false;
        }

        if (message.UserId == Guid.Empty)
        {
            error = "UserId não pode ser vazio";
            return false;
        }

        if (message.GameId == Guid.Empty)
        {
            error = "GameId não pode ser vazio";
            return false;
        }

        if (message.Amount <= 0)
        {
            error = "Amount deve ser maior que zero";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.Currency))
        {
            error = "Currency não pode ser vazio";
            return false;
        }

        return true;
    }
}
