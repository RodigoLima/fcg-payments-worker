namespace FCGPagamentos.Worker.Models;

public record GamePurchaseRequestedEvent(
    Guid PaymentId,
    Guid UserId,
    Guid GameId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    Guid CorrelationId
);
