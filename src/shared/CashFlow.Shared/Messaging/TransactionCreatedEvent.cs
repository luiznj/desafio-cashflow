namespace CashFlow.Shared.Messaging;

public record TransactionCreatedEvent(
    Guid TransactionId,
    Guid UserId,
    decimal Amount,
    string Type,
    DateTime OccurredAt);
