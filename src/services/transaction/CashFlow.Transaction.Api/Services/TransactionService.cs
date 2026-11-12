using System.Text.Json;
using CashFlow.Shared.Messaging;
using CashFlow.Transaction.Api.Dtos;
using CashFlow.Transaction.Db.Models;
using CashFlow.Transaction.Db.Repositories;
using TransactionEntry = CashFlow.Transaction.Db.Models.Transaction;

namespace CashFlow.Transaction.Api.Services;

public class TransactionService(ITransactionRepository repository, ILogger<TransactionService> logger) : ITransactionService
{
    public async Task<(TransactionResponse Transaction, bool IsNew)> CreateAsync(Guid userId, CreateTransactionRequest request, string idempotencyKey)
    {
        var existing = await repository.FindByIdempotencyKeyAsync(idempotencyKey);
        if (existing is not null)
        {
            logger.LogWarning("Idempotência: chave {IdempotencyKey} já utilizada pelo usuário {UserId}", idempotencyKey, userId);
            return (Map(existing), IsNew: false);
        }

        var type = Enum.Parse<TransactionType>(request.Type, ignoreCase: true);

        // Stage entity (not saved yet)
        var entry = type == TransactionType.Credit
            ? repository.StageCredit(userId, request.Amount, request.Description, idempotencyKey)
            : repository.StageDebit(userId, request.Amount, request.Description, idempotencyKey);

        var evt = new TransactionCreatedEvent(entry.Id, entry.UserId, entry.Amount, entry.Type.ToString(), entry.OccurredAt);

        // Stage outbox message in the same unit of work
        repository.StageOutboxMessage(nameof(TransactionCreatedEvent), JsonSerializer.Serialize(evt));

        // Single SaveAsync: persists transaction + outbox message atomically
        await repository.SaveAsync();

        logger.LogInformation("Lançamento criado {TransactionId} | Usuário {UserId} | Tipo {Type} | Valor {Amount}", entry.Id, userId, type, entry.Amount);
        return (Map(entry), IsNew: true);
    }

    public async Task<PagedResult<DailyTransactionsResponse>> GetAsync(Guid userId, GetTransactionsQuery query)
    {
        TransactionType? type = query.Type is not null
            ? Enum.Parse<TransactionType>(query.Type, ignoreCase: true)
            : null;

        var transactions = await repository.GetAsync(userId, type, query.StartDate, query.EndDate);

        var grouped = transactions
            .GroupBy(t => DateOnly.FromDateTime(t.OccurredAt))
            .OrderByDescending(g => g.Key)
            .ToList();

        var totalItems = grouped.Count;
        var totalPages = (int)Math.Ceiling(totalItems / (double)query.PageSize);

        var pagedGroups = grouped
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(g => new DailyTransactionsResponse(
                g.Key,
                g.Sum(t => t.Amount),
                g.Select(Map).ToList()
            ))
            .ToList();

        return new PagedResult<DailyTransactionsResponse>(
            query.Page,
            query.PageSize,
            totalItems,
            totalPages,
            pagedGroups
        );
    }

    private static TransactionResponse Map(TransactionEntry t) => new(
        t.Id,
        Math.Abs(t.Amount),
        t.Type.ToString(),
        string.IsNullOrEmpty(t.Description) ? null : t.Description,
        t.OccurredAt
    );
}
