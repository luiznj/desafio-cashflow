using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CashFlow.Transaction.Db.Data;

public class TransactionDbContextFactory : IDesignTimeDbContextFactory<TransactionDbContext>
{
    public TransactionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=cashflow;Username=cashflow;Password=cashflow")
            .Options;

        return new TransactionDbContext(options);
    }
}
