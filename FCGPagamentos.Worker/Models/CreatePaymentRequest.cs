namespace FCGPagamentos.Worker.Models;

public record CreatePaymentRequest(
    Guid Id,
    Guid UserId,
    Guid GameId,
    decimal Amount,
    string Currency,
    string PaymentMethod
);
