namespace FCGPagamentos.Worker.Models;

public record CreatePaymentRequest(
    Guid PaymentId,
    Guid UserId,
    string GameId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    Guid CorrelationId
);
