using FCGPagamentos.Worker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            var connectionString = configuration.GetConnectionString("AzureWebJobsStorage");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("AzureWebJobsStorage connection string not configured");
            }
            
            return new QueueClientFactory(connectionString);
        });

        // Registrar serviços
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IEventPublisher, EventPublisher>();
        services.AddScoped<IObservabilityService, ObservabilityService>();

        return services;
    }
}
