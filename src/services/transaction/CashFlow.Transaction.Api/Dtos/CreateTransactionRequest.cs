namespace CashFlow.Transaction.Api.Dtos;

public record CreateTransactionRequest(decimal Amount, string Type, string? Description = null);
