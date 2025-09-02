using FCGPagamentos.Worker.Models;

namespace FCGPagamentos.Worker.Services;

public interface IPaymentsApiClient
{
    Task<Payment?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<bool> MarkProcessingAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<bool> MarkApprovedAsync(Guid paymentId, string providerResponse, CancellationToken cancellationToken = default);
    Task<bool> MarkDeclinedAsync(Guid paymentId, string providerResponse, string reason, CancellationToken cancellationToken = default);
    Task<bool> MarkFailedAsync(Guid paymentId, string reason, CancellationToken cancellationToken = default);
}
