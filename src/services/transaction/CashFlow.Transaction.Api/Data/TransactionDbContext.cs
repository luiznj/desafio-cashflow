using Microsoft.EntityFrameworkCore;

namespace CashFlow.Transaction.Api.Data;

public class TransactionDbContext(DbContextOptions<TransactionDbContext> options) : DbContext(options)
{
    public DbSet<Models.Transaction> Transactions => Set<Models.Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("transaction");
    }
}
