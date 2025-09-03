using FCGPagamentos.Worker.Models;

namespace FCGPagamentos.Worker.Services;

public interface IPaymentService
{
    Task<bool> ProcessPaymentAsync(PaymentRequestedMessage message, CancellationToken cancellationToken = default);
    Task<bool> CreatePaymentAsync(GamePurchaseRequestedEvent purchaseEvent, CancellationToken cancellationToken = default);
}
