using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CashFlow.Identity.Db.Data;

public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=cashflow;Username=cashflow;Password=cashflow")
            .Options;

        return new IdentityDbContext(options);
    }
}
