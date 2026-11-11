namespace CashFlow.Transaction.Api.Dtos;

public record DailyTransactionsResponse(
    DateOnly Date,
    decimal DailyBalance,
    IReadOnlyList<TransactionResponse> Transactions
);
