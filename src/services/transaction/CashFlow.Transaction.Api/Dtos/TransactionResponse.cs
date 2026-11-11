namespace CashFlow.Transaction.Api.Dtos;

public record TransactionResponse(
    Guid Id,
    decimal Amount,
    string Type,
    string? Description,
    DateTime OccurredAt
);
