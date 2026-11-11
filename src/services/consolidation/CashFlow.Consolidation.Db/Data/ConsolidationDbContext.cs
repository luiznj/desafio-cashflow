using CashFlow.Consolidation.Db.Models;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.Db.Data;

public class ConsolidationDbContext(DbContextOptions<ConsolidationDbContext> options) : DbContext(options)
{
    public DbSet<DailyBalance> DailyBalances => Set<DailyBalance>();
    public DbSet<ProcessedTransaction> ProcessedTransactions => Set<ProcessedTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("consolidation");

        modelBuilder.Entity<DailyBalance>()
            .HasIndex(b => new { b.UserId, b.Date })
            .IsUnique();

        modelBuilder.Entity<ProcessedTransaction>()
            .HasKey(p => p.TransactionId);
    }
}
