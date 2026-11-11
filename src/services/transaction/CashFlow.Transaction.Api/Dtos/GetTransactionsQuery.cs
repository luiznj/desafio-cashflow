namespace CashFlow.Transaction.Api.Dtos;

public class GetTransactionsQuery
{
    public string? Type { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 7;
}
