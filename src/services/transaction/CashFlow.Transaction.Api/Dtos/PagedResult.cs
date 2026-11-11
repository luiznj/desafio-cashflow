namespace CashFlow.Transaction.Api.Dtos;

public record PagedResult<T>(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyList<T> Data
);
