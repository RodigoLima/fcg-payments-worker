using FCGPagamentos.Worker.Models;
using Microsoft.Extensions.Logging;

namespace FCGPagamentos.Worker.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IObservabilityService _observabilityService;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPaymentRepository paymentRepository,
        IEventPublisher eventPublisher,
        IObservabilityService observabilityService,
        ILogger<PaymentService> logger)
    {
        _paymentRepository = paymentRepository;
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

            // 1. Carregar payment do PostgreSQL
            var payment = await _paymentRepository.GetByIdAsync(message.PaymentId, cancellationToken);
            if (payment == null)
            {
                _logger.LogError("Pagamento {PaymentId} não encontrado no banco de dados", message.PaymentId);
                await _eventPublisher.PublishPaymentFailedAsync(message.PaymentId, message.CorrelationId, "Pagamento não encontrado", cancellationToken);
                return false;
            }

            // 2. Gravar evento PaymentProcessing
            await _eventPublisher.PublishPaymentProcessingAsync(message.PaymentId, message.CorrelationId, cancellationToken);

            // 3. Simular provedor (valor par = aprovado, ímpar = recusado)
            var isApproved = SimulateProviderDecision(payment.Amount);
            var providerResponse = isApproved ? "approved" : "declined";
            var reason = isApproved ? "Pagamento aprovado pelo provedor" : "Pagamento recusado pelo provedor";

            _logger.LogInformation("Simulação do provedor para pagamento {PaymentId}: {Result} (valor: {Amount})", 
                message.PaymentId, providerResponse, payment.Amount);

            // 4. Gravar evento PaymentApproved ou PaymentDeclined
            if (isApproved)
            {
                await _eventPublisher.PublishPaymentApprovedAsync(message.PaymentId, message.CorrelationId, providerResponse, cancellationToken);
                await _paymentRepository.UpdateStatusAsync(message.PaymentId, "APPROVED", providerResponse, null, cancellationToken);
            }
            else
            {
                await _eventPublisher.PublishPaymentDeclinedAsync(message.PaymentId, message.CorrelationId, reason, cancellationToken);
                await _paymentRepository.UpdateStatusAsync(message.PaymentId, "DECLINED", providerResponse, reason, cancellationToken);
            }

            _logger.LogInformation("Pagamento {PaymentId} processado com sucesso: {Status}", message.PaymentId, providerResponse);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao processar pagamento {PaymentId} (CorrelationId: {CorrelationId})", 
                message.PaymentId, message.CorrelationId);
            
            // 5. Gravar evento PaymentFailed em caso de erro
            await _eventPublisher.PublishPaymentFailedAsync(message.PaymentId, message.CorrelationId, ex.Message, cancellationToken);
            await _paymentRepository.UpdateStatusAsync(message.PaymentId, "FAILED", null, ex.Message, cancellationToken);
            
            return false;
        }
    }

    private static bool SimulateProviderDecision(decimal amount)
    {
        // Regra simples: valor par = aprovado, ímpar = recusado
        return (int)(amount * 100) % 2 == 0;
    }
}
