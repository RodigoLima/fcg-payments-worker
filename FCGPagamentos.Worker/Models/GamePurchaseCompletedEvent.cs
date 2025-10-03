namespace FCGPagamentos.Worker.Models;

public record GamePurchaseCompletedEvent(
    Guid PaymentId,
    Guid UserId,
    string GameId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string Status, // "approved", "declined", "failed"
    string? Reason, // Motivo da aprovação, recusa ou falha
    Guid CorrelationId,
    DateTime CompletedAt
);
