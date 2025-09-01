using FCGPagamentos.Worker.Configuration;
using FCGPagamentos.Worker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FCGPagamentos.Worker.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configurar opções
        services.Configure<PaymentsApiOptions>(
            configuration.GetSection(PaymentsApiOptions.SectionName));

        // Validar configurações obrigatórias
        var options = configuration.GetSection(PaymentsApiOptions.SectionName).Get<PaymentsApiOptions>();
        if (options == null || string.IsNullOrEmpty(options.BaseUrl) || string.IsNullOrEmpty(options.InternalToken))
        {
            throw new InvalidOperationException("Configurações da API de pagamentos são obrigatórias");
        }

        // Registrar serviços
        services.AddScoped<IPaymentService, PaymentService>();

        // Configurar HttpClient nomeado
        services.AddHttpClient("PaymentsApi", client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.Add("User-Agent", "FCGPagamentos-Worker/1.0");
        });

        return services;
    }
}
