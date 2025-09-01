using System.Text.Json;
using FCGPagamentos.Worker.Models;
using FCGPagamentos.Worker.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FCGPagamentos.Worker.Functions;

public class ProcessPaymentFunction
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<ProcessPaymentFunction> _logger;

    public ProcessPaymentFunction(
        IPaymentService paymentService,
        ILogger<ProcessPaymentFunction> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [Function("ProcessPaymentFunction")]
    public async Task Run(
        [QueueTrigger("payments-requests", Connection = "AzureWebJobsStorage")] string message,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Iniciando processamento da mensagem da fila");
            
            var paymentMessage = JsonSerializer.Deserialize<PaymentRequestedMessage>(message);
            if (paymentMessage == null)
            {
                _logger.LogError("Falha ao deserializar mensagem da fila: {Message}", message);
                return;
            }

            _logger.LogInformation("Processando pagamento {PaymentId} para usuário {UserId}", 
                paymentMessage.PaymentId, paymentMessage.UserId);

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
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Erro ao deserializar mensagem da fila: {Message}", message);
            // TODO: Implementar dead letter queue para mensagens inválidas
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao processar mensagem da fila: {Message}", message);
            // TODO: Implementar dead letter queue para mensagens com erro
        }
    }
}
