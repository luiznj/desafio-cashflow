using CashFlow.Consolidation.Db.Data;
using CashFlow.Consolidation.Db.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Consolidation.Db.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConsolidationDb(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ConsolidationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IDailyBalanceRepository, DailyBalanceRepository>();

        return services;
    }
}
