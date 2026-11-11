using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CashFlow.Consolidation.Db.Data;

public class ConsolidationDbContextFactory : IDesignTimeDbContextFactory<ConsolidationDbContext>
{
    public ConsolidationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ConsolidationDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=cashflow;Username=cashflow;Password=cashflow")
            .Options;

        return new ConsolidationDbContext(options);
    }
}
