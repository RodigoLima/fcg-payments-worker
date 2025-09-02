using FCGPagamentos.Worker.Models;

namespace FCGPagamentos.Worker.Services;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid paymentId, string status, string? providerResponse = null, string? failureReason = null, CancellationToken cancellationToken = default);
}
