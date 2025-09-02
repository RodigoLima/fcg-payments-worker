namespace FCGPagamentos.Worker.Models;

public record PaymentRequestedMessage(
    Guid PaymentId, 
    Guid CorrelationId,
    Guid UserId, 
    Guid GameId, 
    decimal Amount, 
    string Currency,
    string PaymentMethod,
    DateTime OccurredAt,
    string Version
);

// DTO para deserialização JSON que aceita strings para campos GUID
public class PaymentRequestedMessageDto
{
    public string PaymentId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string Version { get; set; } = string.Empty;

    public PaymentRequestedMessage ToPaymentRequestedMessage()
    {
        return new PaymentRequestedMessage(
            ParseGuid(PaymentId, nameof(PaymentId)),
            ParseGuid(CorrelationId, nameof(CorrelationId)),
            ParseGuid(UserId, nameof(UserId)),
            ParseGuid(GameId, nameof(GameId)),
            Amount,
            Currency,
            PaymentMethod,
            OccurredAt,
            Version
        );
    }

    private static Guid ParseGuid(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"O campo {fieldName} não pode ser nulo ou vazio.", fieldName);
        }

        if (Guid.TryParse(value, out var guid))
        {
            return guid;
        }

        // Se não for um GUID válido, gerar um GUID determinístico baseado no valor
        // Isso permite que strings como "user123" sejam convertidas para GUIDs consistentes
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        var guidBytes = new byte[16];
        Array.Copy(hash, 0, guidBytes, 0, 16);
        return new Guid(guidBytes);
    }
}
