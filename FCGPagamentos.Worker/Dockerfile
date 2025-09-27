# Estágio de Build: Usa o SDK do .NET 8.0 para construir a aplicação
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Limpar configurações problemáticas do NuGet
RUN rm -rf /root/.nuget/NuGet.Config 2>/dev/null || true

# Copia os arquivos de projeto (.csproj) para restaurar as dependências
# Isso otimiza o cache do Docker
COPY ["FCGPagamentos_Worker.csproj", "."]

# Restaurar dependências com configurações limpas
RUN dotnet restore "FCGPagamentos_Worker.csproj" \
    --disable-parallel \
    --verbosity normal

# Copia todo o código-fonte restante
COPY . .

# Publica o projeto para a pasta de destino final com otimizações
RUN dotnet publish "FCGPagamentos_Worker.csproj" \
    -c Release \
    -o /app/publish \
    --runtime linux-x64 \
    --self-contained false \
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
    DOTNET_RUNNING_IN_CONTAINER=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated \
    FUNCTIONS_EXTENSION_VERSION=~4

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

# Health check otimizado para Azure Functions
# Usa o endpoint padrão de health do Azure Functions
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:80/admin/host/status || exit 1