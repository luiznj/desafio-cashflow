namespace CashFlow.Consolidation.Db.Models;

public class DailyBalance
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal Balance => TotalCredits - TotalDebits;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
