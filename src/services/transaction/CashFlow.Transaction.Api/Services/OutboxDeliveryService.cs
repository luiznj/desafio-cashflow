using System.Text.Json;
using CashFlow.Shared.Messaging;
using CashFlow.Transaction.Db.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Transaction.Api.Services;

public class OutboxDeliveryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDeliveryService> _logger;
    private readonly TimeSpan _interval;

    public OutboxDeliveryService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<OutboxDeliveryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromMilliseconds(configuration.GetValue("Outbox:DeliveryIntervalMs", 5000));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeliverPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao entregar mensagens do outbox");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task DeliverPendingAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var msg in pending)
        {
            var evt = JsonSerializer.Deserialize<TransactionCreatedEvent>(msg.Payload)!;
            await publisher.Publish(evt, ct);
            msg.ProcessedAt = DateTime.UtcNow;
            _logger.LogInformation("Outbox: entregue {TransactionId}", evt.TransactionId);
        }

        if (pending.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
