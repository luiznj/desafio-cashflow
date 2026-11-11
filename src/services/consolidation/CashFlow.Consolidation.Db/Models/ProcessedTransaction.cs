namespace CashFlow.Consolidation.Db.Models;

public class ProcessedTransaction
{
    public Guid TransactionId { get; set; }
    public DateTime ProcessedAt { get; set; }
}
