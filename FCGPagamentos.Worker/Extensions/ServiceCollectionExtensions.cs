using FCGPagamentos.Worker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.Storage.Queues;

namespace FCGPagamentos.Worker.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configurar HttpClient para API
        services.AddHttpClient<IPaymentsApiClient, PaymentsApiClient>(client =>
        {
            var baseUrl = configuration["PaymentsApi:BaseUrl"];
            if (!string.IsNullOrEmpty(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }
            client.DefaultRequestHeaders.Add("User-Agent", "FCGPagamentos-Worker/1.0");
        });

        // Configurar QueueClientFactory para múltiplas filas
        services.AddSingleton<IQueueClientFactory>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<QueueClientFactory>>();
            
            // Tentar diferentes formas de obter a string de conexão
            var connectionString = configuration["AzureWebJobsStorage"] ?? 
                                 configuration.GetConnectionString("AzureWebJobsStorage");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                logger.LogError("AzureWebJobsStorage connection string not configured. " +
                              "Please verify the Application Settings in Azure Function App configuration.");
                throw new InvalidOperationException("AzureWebJobsStorage connection string not configured");
            }
            
            logger.LogInformation("AzureWebJobsStorage connection string configured successfully");
            return new QueueClientFactory(connectionString);
        });

        // Registrar serviços
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IEventPublisher, EventPublisher>();
        services.AddScoped<IObservabilityService, ObservabilityService>();

        return services;
    }
}
