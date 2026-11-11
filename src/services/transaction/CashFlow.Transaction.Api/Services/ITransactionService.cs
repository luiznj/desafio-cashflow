using CashFlow.Transaction.Api.Dtos;

namespace CashFlow.Transaction.Api.Services;

public interface ITransactionService
{
    Task<(TransactionResponse Transaction, bool IsNew)> CreateAsync(Guid userId, CreateTransactionRequest request, string idempotencyKey);
    Task<PagedResult<DailyTransactionsResponse>> GetAsync(Guid userId, GetTransactionsQuery query);
}
