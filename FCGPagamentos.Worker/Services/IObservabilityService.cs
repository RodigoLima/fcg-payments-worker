using System.Diagnostics;

namespace FCGPagamentos.Worker.Services;

public interface IObservabilityService
{
    Activity? StartPaymentProcessingActivity(Guid paymentId, Guid correlationId);
    void TrackPostgresDependency(string operation, string query, TimeSpan duration, bool success);
    void SetCorrelationId(Guid correlationId);
}
