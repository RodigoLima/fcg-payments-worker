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
        // Configurar correlation ID para traces distribuídos
        _observabilityService.SetCorrelationId(message.CorrelationId);
        
        // Iniciar activity para tracing distribuído
        using var activity = _observabilityService.StartPaymentProcessingActivity(message.PaymentId, message.CorrelationId);
        
        try
        {
            _logger.LogInformation("Iniciando processamento do pagamento {PaymentId} (CorrelationId: {CorrelationId})", 
                message.PaymentId, message.CorrelationId);

            // 1. Carregar payment via API
            var payment = await _apiClient.GetPaymentAsync(message.PaymentId, cancellationToken);
            if (payment == null)
            {
                _logger.LogError("Pagamento {PaymentId} não encontrado na API", message.PaymentId);
                await _eventPublisher.PublishPaymentFailedAsync(message.PaymentId, message.CorrelationId, "Pagamento não encontrado", cancellationToken);
                return false;
            }

            // 2. Marcar como processando via API
            await _apiClient.MarkProcessingAsync(message.PaymentId, cancellationToken);
            await _eventPublisher.PublishPaymentProcessingAsync(message.PaymentId, message.CorrelationId, cancellationToken);

            // 3. Simular provedor (valor par = aprovado, ímpar = recusado)
            var isApproved = SimulateProviderDecision(payment.Amount);
            var providerResponse = isApproved ? "approved" : "declined";
            var reason = isApproved ? "Pagamento aprovado pelo provedor" : "Pagamento recusado pelo provedor";

            _logger.LogInformation("Simulação do provedor para pagamento {PaymentId}: {Result} (valor: {Amount})", 
                message.PaymentId, providerResponse, payment.Amount);

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

            _logger.LogInformation("Pagamento {PaymentId} processado com sucesso: {Status}", message.PaymentId, providerResponse);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao processar pagamento {PaymentId} (CorrelationId: {CorrelationId})", 
                message.PaymentId, message.CorrelationId);
            
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
}
