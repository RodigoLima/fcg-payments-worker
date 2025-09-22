namespace FCGPagamentos.Worker.Models;

public record GamePurchaseRequestedEvent(
    Guid UserId,
    Guid GameId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    Guid CorrelationId
);
