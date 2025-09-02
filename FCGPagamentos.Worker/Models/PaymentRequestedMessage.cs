namespace FCGPagamentos.Worker.Models;

public record PaymentRequestedMessage(
    Guid PaymentId, 
    Guid CorrelationId,
    Guid UserId, 
    Guid GameId, 
    decimal Amount, 
    string Currency
);
