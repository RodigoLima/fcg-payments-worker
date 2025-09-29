# FCG Payments Worker

Um worker Azure Functions desenvolvido em .NET 8 para processamento de pagamentos de jogos, utilizando filas do Azure Storage para comunicação assíncrona.

## 📋 Visão Geral

O FCG Payments Worker é responsável por processar pagamentos de jogos de forma assíncrona através de duas funções principais:

- **CreatePaymentFunction**: Processa eventos de compra de jogos e cria pagamentos
- **ProcessPaymentFunction**: Processa mensagens de pagamento da fila e executa o pagamento

## 🏗️ Arquitetura

```
┌─────────────────────┐    ┌─────────────────────┐    ┌─────────────────────┐
│   Game Purchase     │───▶│  CreatePayment      │───▶│  ProcessPayment     │
│   Requested Event   │    │  Function           │    │  Function           │
└─────────────────────┘    └─────────────────────┘    └─────────────────────┘
                                    │                           │
                                    ▼                           ▼
                           ┌─────────────────────┐    ┌─────────────────────┐
                           │ game-purchase-       │    │ payments-to-process │
                           │ requested queue      │    │ queue               │
                           └─────────────────────┘    └─────────────────────┘
```

## 🚀 Pré-requisitos

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools v4](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [Azure Storage Emulator](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator) ou conta do Azure Storage
- [Docker](https://www.docker.com/get-started) (opcional, para containerização)

## 📦 Instalação

### 1. Clone o repositório

```bash
git clone https://github.com/RodigoLima/fcg-payments-worker.git
cd fcg-payments-worker/FCGPagamentos.Worker
```

### 2. Restaure as dependências

```bash
dotnet restore
```

### 3. Configure as variáveis de ambiente

Copie o arquivo `local.settings.json.example` para `local.settings.json` e configure:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "PaymentsApi:BaseUrl": "http://localhost:5080",
    "PaymentsApi:InternalToken": "seu-token-aqui",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": "sua-connection-string-aqui",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

### 4. Execute localmente

```bash
func start
```

## 🐳 Execução com Docker

### Build da imagem

```bash
docker build -t fcg-payments-worker .
```

### Execução do container

```bash
docker run -p 8080:80 \
  -e AzureWebJobsStorage="sua-connection-string" \
  -e PaymentsApi__BaseUrl="http://host.docker.internal:5080" \
  -e PaymentsApi__InternalToken="seu-token" \
  fcg-payments-worker
```

## ⚙️ Configuração

### Variáveis de Ambiente Obrigatórias

| Variável | Descrição | Exemplo |
|----------|-----------|---------|
| `AzureWebJobsStorage` | String de conexão do Azure Storage | `DefaultEndpointsProtocol=https;AccountName=...` |
| `PaymentsApi:BaseUrl` | URL base da API de pagamentos | `https://api.payments.com` |
| `PaymentsApi:InternalToken` | Token de autenticação interno | `super-secret-token` |

### Variáveis Opcionais

| Variável | Descrição | Padrão |
|----------|-----------|---------|
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Connection string do Application Insights | - |
| `FUNCTIONS_WORKER_RUNTIME` | Runtime do Azure Functions | `dotnet-isolated` |

## 📊 Filas Utilizadas

### `game-purchase-requested`
- **Função**: `CreatePaymentFunction`
- **Payload**: `GamePurchaseRequestedEvent`
- **Descrição**: Processa eventos de compra de jogos e cria pagamentos

### `payments-to-process`
- **Função**: `ProcessPaymentFunction`
- **Payload**: `PaymentRequestedMessage`
- **Descrição**: Processa mensagens de pagamento e executa o pagamento

## 🔧 Desenvolvimento

### Estrutura do Projeto

```
FCGPagamentos.Worker/
├── Functions/                 # Azure Functions
│   ├── CreatePaymentFunction.cs
│   └── ProcessPaymentFunction.cs
├── Models/                    # Modelos de dados
│   ├── GamePurchaseRequestedEvent.cs
│   ├── PaymentRequestedMessage.cs
│   └── Payment.cs
├── Services/                  # Serviços de negócio
│   ├── PaymentService.cs
│   ├── PaymentsApiClient.cs
│   └── EventPublisher.cs
├── Extensions/               # Extensões de configuração
│   └── ServiceCollectionExtensions.cs
└── Program.cs                # Ponto de entrada
```

### Executar testes

```bash
dotnet test
```

### Build para produção

```bash
dotnet publish -c Release -o ./output
```

