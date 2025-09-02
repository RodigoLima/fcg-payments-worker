using FCGPagamentos.Worker.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace FCGPagamentos.Worker.Services;

public class PaymentsApiClient : IPaymentsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaymentsApiClient> _logger;
    private readonly IObservabilityService _observabilityService;
    private readonly string _baseUrl;
    private readonly string _internalToken;

    public PaymentsApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PaymentsApiClient> logger,
        IObservabilityService observabilityService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _observabilityService = observabilityService;
        _baseUrl = configuration["PaymentsApi:BaseUrl"] ?? throw new InvalidOperationException("PaymentsApi:BaseUrl not configured");
        _internalToken = configuration["PaymentsApi:InternalToken"] ?? throw new InvalidOperationException("PaymentsApi:InternalToken not configured");
        
        _httpClient.DefaultRequestHeaders.Add("x-internal-token", _internalToken);
    }

    public async Task<Payment?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var endpoint = $"/internal/payments/{paymentId}";
        
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}{endpoint}", cancellationToken);
            stopwatch.Stop();
            
            if (response.IsSuccessStatusCode)
            {
                _observabilityService.TrackApiDependency("GET", endpoint, stopwatch.Elapsed, true);
                return await response.Content.ReadFromJsonAsync<Payment>(cancellationToken: cancellationToken);
            }
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _observabilityService.TrackApiDependency("GET", endpoint, stopwatch.Elapsed, true);
                return null;
            }
            
            _observabilityService.TrackApiDependency("GET", endpoint, stopwatch.Elapsed, false);
            _logger.LogWarning("Falha ao buscar pagamento {PaymentId}. Status: {StatusCode}", paymentId, response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _observabilityService.TrackApiDependency("GET", endpoint, stopwatch.Elapsed, false);
            _logger.LogError(ex, "Erro ao buscar pagamento {PaymentId}", paymentId);
            throw;
        }
    }

    public async Task<bool> MarkProcessingAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await CallStatusEndpointAsync(paymentId, "mark-processing", cancellationToken);
    }

    public async Task<bool> MarkApprovedAsync(Guid paymentId, string providerResponse, CancellationToken cancellationToken = default)
    {
        var content = JsonContent.Create(new { providerResponse });
        return await CallStatusEndpointAsync(paymentId, "mark-approved", cancellationToken, content);
    }

    public async Task<bool> MarkDeclinedAsync(Guid paymentId, string providerResponse, string reason, CancellationToken cancellationToken = default)
    {
        var content = JsonContent.Create(new { providerResponse, reason });
        return await CallStatusEndpointAsync(paymentId, "mark-declined", cancellationToken, content);
    }

    public async Task<bool> MarkFailedAsync(Guid paymentId, string reason, CancellationToken cancellationToken = default)
    {
        var content = JsonContent.Create(new { reason });
        return await CallStatusEndpointAsync(paymentId, "mark-failed", cancellationToken, content);
    }

    private async Task<bool> CallStatusEndpointAsync(Guid paymentId, string endpoint, CancellationToken cancellationToken, HttpContent? content = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var fullEndpoint = $"/internal/payments/{paymentId}/{endpoint}";
        
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}{fullEndpoint}", content, cancellationToken);
            stopwatch.Stop();
            
            if (response.IsSuccessStatusCode)
            {
                _observabilityService.TrackApiDependency("POST", fullEndpoint, stopwatch.Elapsed, true);
                _logger.LogDebug("Status do pagamento {PaymentId} atualizado via {Endpoint}", paymentId, endpoint);
                return true;
            }
            
            _observabilityService.TrackApiDependency("POST", fullEndpoint, stopwatch.Elapsed, false);
            _logger.LogWarning("Falha ao atualizar status do pagamento {PaymentId} via {Endpoint}. Status: {StatusCode}", 
                paymentId, endpoint, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _observabilityService.TrackApiDependency("POST", fullEndpoint, stopwatch.Elapsed, false);
            _logger.LogError(ex, "Erro ao atualizar status do pagamento {PaymentId} via {Endpoint}", paymentId, endpoint);
            throw;
        }
    }
}
