# Estágio de Build: Usa o SDK do .NET 8.0 para construir a aplicação
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia os arquivos de projeto (.csproj) para restaurar as dependências
# Isso otimiza o cache do Docker
COPY ["FCGPagamentos.Worker.csproj", "."]
RUN dotnet restore "FCGPagamentos.Worker.csproj" \
    --runtime linux-x64 \
    --self-contained false

# Copia todo o código-fonte restante
COPY . .

# Publica o projeto para a pasta de destino final com otimizações
RUN dotnet publish "FCGPagamentos.Worker.csproj" \
    -c Release \
    -o /app/publish \
    --runtime linux-x64 \
    --self-contained false \
    --no-restore \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false

# Estágio Final: Usa a imagem de runtime OFICIAL do Azure Functions
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0

# Cria um usuário não-root para segurança
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Define variáveis de ambiente necessárias para o runtime do Functions
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    DOTNET_RUNNING_IN_CONTAINER=true

# Define o diretório de trabalho
WORKDIR /home/site/wwwroot

# Copia os arquivos publicados do estágio 'build'
COPY --from=build /app/publish .

# Altera a propriedade dos arquivos para o usuário não-root
RUN chown -R appuser:appuser /home/site/wwwroot

# Muda para o usuário não-root
USER appuser

# Expõe a porta padrão do Azure Functions
EXPOSE 80

# Health check para verificar se a aplicação está funcionando
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:80/api/health || exit 1