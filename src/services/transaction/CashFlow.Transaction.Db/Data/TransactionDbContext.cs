using CashFlow.Transaction.Db.Models;
using Microsoft.EntityFrameworkCore;
using TransactionEntry = CashFlow.Transaction.Db.Models.Transaction;

namespace CashFlow.Transaction.Db.Data;

public class TransactionDbContext(DbContextOptions<TransactionDbContext> options) : DbContext(options)
{
    public DbSet<TransactionEntry> Transactions => Set<TransactionEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("transaction");

        modelBuilder.Entity<TransactionEntry>()
            .HasIndex(t => t.IdempotencyKey)
            .IsUnique();

        modelBuilder.Entity<OutboxMessage>()
            .HasIndex(m => m.ProcessedAt);
    }
}
