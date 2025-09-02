using FCGPagamentos.Worker.Models;

namespace FCGPagamentos.Worker.Services;

public interface IEventPublisher
{
    Task PublishPaymentProcessingAsync(Guid paymentId, Guid correlationId, CancellationToken cancellationToken = default);
    Task PublishPaymentApprovedAsync(Guid paymentId, Guid correlationId, string providerResponse, CancellationToken cancellationToken = default);
    Task PublishPaymentDeclinedAsync(Guid paymentId, Guid correlationId, string reason, CancellationToken cancellationToken = default);
    Task PublishPaymentFailedAsync(Guid paymentId, Guid correlationId, string reason, CancellationToken cancellationToken = default);
}

public record PaymentEvent(
    Guid PaymentId,
    Guid CorrelationId,
    string EventType,
    DateTime Timestamp,
    string? Data = null
);
