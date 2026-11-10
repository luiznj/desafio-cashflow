namespace CashFlow.Transaction.Api.Messaging;

public record TransactionCreatedEvent(
    Guid TransactionId,
    Guid UserId,
    decimal Amount,
    string Type,
    DateTime OccurredAt);
