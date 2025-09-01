namespace FCGPagamentos.Worker.Configuration;

public class PaymentsApiOptions
{
    public const string SectionName = "PaymentsApi";
    
    public string BaseUrl { get; set; } = string.Empty;
    public string InternalToken { get; set; } = string.Empty;
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public int TimeoutSeconds { get; set; } = 30;
}
