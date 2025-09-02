using FCGPagamentos.Worker.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using Npgsql;

namespace FCGPagamentos.Worker.Services;

public class PaymentRepository : IPaymentRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PaymentRepository> _logger;
    private readonly IObservabilityService _observabilityService;

    public PaymentRepository(
        IConfiguration configuration, 
        ILogger<PaymentRepository> logger,
        IObservabilityService observabilityService)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found");
        _logger = logger;
        _observabilityService = observabilityService;
    }

    public async Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var query = "SELECT id, user_id, game_id, amount, currency, status, created_at, processed_at, provider_response, failure_reason FROM payments WHERE id = @paymentId";
        
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@paymentId", paymentId);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            if (await reader.ReadAsync(cancellationToken))
            {
                var payment = new Payment
                {
                    Id = reader.GetGuid("id"),
                    UserId = reader.GetGuid("user_id"),
                    GameId = reader.GetGuid("game_id"),
                    Amount = reader.GetDecimal("amount"),
                    Currency = reader.GetString("currency"),
                    Status = reader.GetString("status"),
                    CreatedAt = reader.GetDateTime("created_at"),
                    ProcessedAt = reader.IsDBNull("processed_at") ? null : reader.GetDateTime("processed_at"),
                    ProviderResponse = reader.IsDBNull("provider_response") ? null : reader.GetString("provider_response"),
                    FailureReason = reader.IsDBNull("failure_reason") ? null : reader.GetString("failure_reason")
                };

                stopwatch.Stop();
                _observabilityService.TrackPostgresDependency("SELECT", query, stopwatch.Elapsed, true);
                
                return payment;
            }

            stopwatch.Stop();
            _observabilityService.TrackPostgresDependency("SELECT", query, stopwatch.Elapsed, true);
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _observabilityService.TrackPostgresDependency("SELECT", query, stopwatch.Elapsed, false);
            _logger.LogError(ex, "Erro ao buscar pagamento {PaymentId} no banco de dados", paymentId);
            throw;
        }
    }

    public async Task UpdateStatusAsync(Guid paymentId, string status, string? providerResponse = null, string? failureReason = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var query = "UPDATE payments SET status = @status, processed_at = @processedAt, provider_response = @providerResponse, failure_reason = @failureReason WHERE id = @paymentId";
        
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@paymentId", paymentId);
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@processedAt", DateTime.UtcNow);
            command.Parameters.AddWithValue("@providerResponse", (object?)providerResponse ?? DBNull.Value);
            command.Parameters.AddWithValue("@failureReason", (object?)failureReason ?? DBNull.Value);

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            
            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Pagamento {paymentId} não encontrado para atualização");
            }

            stopwatch.Stop();
            _observabilityService.TrackPostgresDependency("UPDATE", query, stopwatch.Elapsed, true);
            _logger.LogDebug("Status do pagamento {PaymentId} atualizado para {Status}", paymentId, status);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _observabilityService.TrackPostgresDependency("UPDATE", query, stopwatch.Elapsed, false);
            _logger.LogError(ex, "Erro ao atualizar status do pagamento {PaymentId}", paymentId);
            throw;
        }
    }
}
