using System.Diagnostics;

namespace FCGPagamentos.Worker.Services;

public interface IObservabilityService
{
    Activity? StartPaymentProcessingActivity(Guid paymentId, Guid correlationId);
    void TrackApiDependency(string operation, string endpoint, TimeSpan duration, bool success);
    void SetCorrelationId(Guid correlationId);
}
