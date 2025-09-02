using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace FCGPagamentos.Worker.Services;

public class ObservabilityService : IObservabilityService
{
    private readonly TelemetryClient _telemetryClient;
    private static readonly ActivitySource _activitySource = new("FCGPagamentos.Worker.PaymentProcessing");

    public ObservabilityService(TelemetryClient telemetryClient)
    {
        _telemetryClient = telemetryClient;
    }

    public Activity? StartPaymentProcessingActivity(Guid paymentId, Guid correlationId)
    {
        var activity = _activitySource.StartActivity("ProcessPayment");
        
        if (activity != null)
        {
            activity.SetTag("payment.id", paymentId.ToString());
            activity.SetTag("payment.correlation_id", correlationId.ToString());
            activity.SetTag("service.name", "FCGPagamentos.Worker");
            activity.SetTag("service.version", "1.0.0");
        }

        return activity;
    }

    public void TrackApiDependency(string operation, string endpoint, TimeSpan duration, bool success)
    {
        var dependency = new DependencyTelemetry
        {
            Type = "HTTP",
            Name = $"Payments API {operation}",
            Data = endpoint,
            Duration = duration,
            Success = success,
            Target = "payments-api"
        };

        _telemetryClient.TrackDependency(dependency);
    }

    public void SetCorrelationId(Guid correlationId)
    {
        _telemetryClient.Context.Operation.Id = correlationId.ToString();
    }
}
