# FCG Pagamentos Worker

Worker Azure Functions para processamento de pagamentos em background.

## 🏗️ Estrutura do Projeto

```
FCGPagamentos.Worker/
├── Configuration/           # Classes de configuração
│   └── PaymentsApiOptions.cs
├── Extensions/             # Extensões para configuração de serviços
│   └── ServiceCollectionExtensions.cs
├── Functions/              # Azure Functions
│   └── ProcessPaymentFunction.cs
├── Models/                 # DTOs e entidades
│   └── PaymentRequestedMessage.cs
├── Services/               # Lógica de negócio
│   ├── IPaymentService.cs
│   └── PaymentService.cs
└── Tests/                  # Testes unitários (futuro)
```

## 🚀 Funcionalidades

- Processamento assíncrono de pagamentos via fila Azure Storage
- Configuração robusta com validação
- Tratamento de erros e logging estruturado
- Injeção de dependências e interfaces para testabilidade
- Timeout configurável para operações HTTP
- Suporte a Application Insights

## ⚙️ Configuração

### Configurações Obrigatórias

```json
{
  "PaymentsApi": {
    "BaseUrl": "https://api.exemplo.com",
    "InternalToken": "seu-token-secreto",
    "RetryCount": 3,
    "RetryDelayMs": 1000,
    "TimeoutSeconds": 30
  }
}
```

### Variáveis de Ambiente

- `AzureWebJobsStorage`: String de conexão do Azure Storage
- `PaymentsApi:BaseUrl`: URL base da API de pagamentos
- `PaymentsApi:InternalToken`: Token de autenticação interno

## 🔧 Desenvolvimento Local

1. Clone o repositório
2. Configure o `local.settings.json`
3. Execute `dotnet run`
4. Use Azure Storage Emulator ou Azurite para desenvolvimento local

## 📝 Logs

O projeto utiliza logging estruturado com Microsoft.Extensions.Logging e Application Insights para monitoramento em produção.

## 🧪 Testes

Para implementar testes unitários:
1. Adicione pacotes de teste ao projeto
2. Crie testes para `PaymentService` e `ProcessPaymentFunction`
3. Use mocks para `IPaymentService` e dependências externas

## 🚀 Deploy

O projeto está configurado para deploy no Azure Functions com:
- .NET 8.0
- Azure Functions v4
- Application Insights integrado
- Configurações via Application Settings
