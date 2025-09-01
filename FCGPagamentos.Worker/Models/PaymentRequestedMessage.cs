namespace FCGPagamentos.Worker.Models;

public record PaymentRequestedMessage(
    Guid PaymentId, 
    Guid UserId, 
    Guid GameId, 
    decimal Amount, 
    string Currency
);
