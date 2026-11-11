using CashFlow.Transaction.Db.Data;
using CashFlow.Transaction.Db.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Transaction.Db.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransactionDb(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TransactionDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<ITransactionRepository, TransactionRepository>();

        return services;
    }
}
