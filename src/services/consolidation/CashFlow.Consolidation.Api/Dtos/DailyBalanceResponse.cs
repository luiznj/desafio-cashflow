namespace CashFlow.Consolidation.Api.Dtos;

public record DailyBalanceResponse(
    DateOnly Date,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal Balance
);
