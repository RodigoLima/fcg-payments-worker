using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FCGPagamentos.Worker.Extensions;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Configurar serviços de pagamento
builder.Services.AddPaymentServices(builder.Configuration);

// Configurar Application Insights
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Configurar HttpClient
builder.Services.AddHttpClient();

var app = builder.Build();
app.Run();
