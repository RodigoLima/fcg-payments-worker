using System.Net.Http.Json;
using FCGPagamentos.Worker.Configuration;
using FCGPagamentos.Worker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FCGPagamentos.Worker.Services;

public class PaymentService : IPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaymentService> _logger;
    private readonly PaymentsApiOptions _options;

    public PaymentService(
        IHttpClientFactory httpClientFactory,
        ILogger<PaymentService> logger,
        IOptions<PaymentsApiOptions> options)
    {
        _httpClient = httpClientFactory.CreateClient("PaymentsApi");
        _logger = logger;
        _options = options.Value;
        
        // Configurar timeout
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<bool> ProcessPaymentAsync(PaymentRequestedMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Iniciando processamento do pagamento {PaymentId} para usuário {UserId}", 
                message.PaymentId, message.UserId);

            // TODO: Implementar lógica real de processamento de pagamento
            // Aqui simulamos o processamento
            
            var success = await MarkPaymentAsProcessedAsync(message.PaymentId, cancellationToken);
            
            if (success)
            {
                _logger.LogInformation("Pagamento {PaymentId} processado com sucesso", message.PaymentId);
                return true;
            }
            
            _logger.LogWarning("Falha ao marcar pagamento {PaymentId} como processado", message.PaymentId);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro de comunicação ao processar pagamento {PaymentId}", message.PaymentId);
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout ao processar pagamento {PaymentId}", message.PaymentId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao processar pagamento {PaymentId}", message.PaymentId);
            return false;
        }
    }

    private async Task<bool> MarkPaymentAsProcessedAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var callbackUrl = $"{_options.BaseUrl}/internal/payments/{paymentId}/mark-processed";
        
        var request = new HttpRequestMessage(HttpMethod.Post, callbackUrl);
        request.Headers.Add("x-internal-token", _options.InternalToken);
        request.Content = JsonContent.Create(new { success = true });

        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Pagamento {PaymentId} marcado como processado com sucesso", paymentId);
            return true;
        }

        _logger.LogWarning("Falha ao marcar pagamento {PaymentId} como processado. Status: {StatusCode}", 
            paymentId, response.StatusCode);
        return false;
    }
}
