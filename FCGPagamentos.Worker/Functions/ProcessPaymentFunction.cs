using System.Text.Json;
using FCGPagamentos.Worker.Models;
using FCGPagamentos.Worker.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FCGPagamentos.Worker.Functions;

public class ProcessPaymentFunction
{
    private readonly IPaymentService _paymentService;
    private readonly IObservabilityService _observabilityService;
    private readonly ILogger<ProcessPaymentFunction> _logger;

    public ProcessPaymentFunction(
        IPaymentService paymentService,
        IObservabilityService observabilityService,
        ILogger<ProcessPaymentFunction> logger)
    {
        _paymentService = paymentService;
        _observabilityService = observabilityService;
        _logger = logger;
    }

    [Function("ProcessPaymentFunction")]
    public async Task Run(
        [QueueTrigger("payments-to-process", Connection = "AzureWebJobsStorage")] string message,
        CancellationToken cancellationToken)
    {
        PaymentRequestedMessage? paymentMessage = null;
        
        try
        {
            _logger.LogInformation("Iniciando processamento da mensagem da fila");
            
            // Validar se a mensagem não está vazia
            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogError("Mensagem da fila está vazia ou nula");
                return;
            }

            // Tentar deserializar a mensagem
            try
            {
                paymentMessage = JsonSerializer.Deserialize<PaymentRequestedMessage>(message);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erro ao deserializar mensagem da fila. Mensagem: {Message}", message);
                return;
            }

            if (paymentMessage == null)
            {
                _logger.LogError("Falha ao deserializar mensagem da fila - resultado é nulo. Mensagem: {Message}", message);
                return;
            }

            // Log detalhado da mensagem recebida para debug
            _logger.LogDebug("Mensagem deserializada: PaymentId={PaymentId}, CorrelationId={CorrelationId}, UserId={UserId}, GameId={GameId}, Amount={Amount}, Currency={Currency}", 
                paymentMessage.PaymentId, paymentMessage.CorrelationId, paymentMessage.UserId, paymentMessage.GameId, paymentMessage.Amount, paymentMessage.Currency);

            // Configurar correlation ID para traces distribuídos
            _observabilityService.SetCorrelationId(paymentMessage.CorrelationId);

            _logger.LogInformation("Processando pagamento {PaymentId} (CorrelationId: {CorrelationId}) para usuário {UserId} no jogo {GameId}", 
                paymentMessage.PaymentId, paymentMessage.CorrelationId, paymentMessage.UserId, paymentMessage.GameId);

            var success = await _paymentService.ProcessPaymentAsync(paymentMessage, cancellationToken);
            
            if (success)
            {
                _logger.LogInformation("Pagamento {PaymentId} processado com sucesso", paymentMessage.PaymentId);
            }
            else
            {
                _logger.LogWarning("Falha no processamento do pagamento {PaymentId}", paymentMessage.PaymentId);
                // TODO: Implementar dead letter queue ou retry logic
            }
        }
        catch (Exception ex)
        {
            var paymentId = paymentMessage?.PaymentId.ToString() ?? "unknown";
            var correlationId = paymentMessage?.CorrelationId.ToString() ?? "unknown";
            
            _logger.LogError(ex, "Erro inesperado ao processar mensagem da fila. PaymentId: {PaymentId}, CorrelationId: {CorrelationId}, Mensagem: {Message}", 
                paymentId, correlationId, message);
            
            // TODO: Implementar dead letter queue para mensagens com erro
        }
    }
}
