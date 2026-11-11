using Microsoft.EntityFrameworkCore;
using TransactionEntry = CashFlow.Transaction.Db.Models.Transaction;

namespace CashFlow.Transaction.Db.Data;

public class TransactionDbContext(DbContextOptions<TransactionDbContext> options) : DbContext(options)
{
    public DbSet<TransactionEntry> Transactions => Set<TransactionEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("transaction");
        modelBuilder.Entity<TransactionEntry>()
            .HasIndex(t => t.IdempotencyKey)
            .IsUnique();
    }
}
