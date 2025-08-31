using System.Net.Http.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

public class ProcessPaymentFunction
{
    private readonly HttpClient _http;
    private readonly ILogger _log;
    private readonly string _apiBase;
    private readonly string _internalToken;

    public ProcessPaymentFunction(IHttpClientFactory f, ILoggerFactory lf, IConfiguration cfg)
    {
        _http = f.CreateClient();
        _log = lf.CreateLogger<ProcessPaymentFunction>();
        _apiBase = cfg["PaymentsApi:BaseUrl"]!;
        _internalToken = cfg["PaymentsApi:InternalToken"]!;
    }

    [Function("ProcessPaymentFunction")]
    public async Task Run([QueueTrigger("payments-requests", Connection = "AzureWebJobsStorage")] string message)
    {
        _log.LogInformation("Processing message: {message}", message);
        var payload = System.Text.Json.JsonSerializer.Deserialize<PaymentRequestedMessage>(message)!;

        // TODO: lógica real. Aqui simulamos sucesso.
        var callback = $"{_apiBase}/internal/payments/{payload.PaymentId}/mark-processed";
        var req = new HttpRequestMessage(HttpMethod.Post, callback);
        req.Headers.Add("x-internal-token", _internalToken);
        req.Content = JsonContent.Create(new { success = true });

        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        _log.LogInformation("Payment {id} marked processed.", payload.PaymentId);
    }
}

public record PaymentRequestedMessage(Guid PaymentId, Guid UserId, Guid GameId, decimal Amount, string Currency);
