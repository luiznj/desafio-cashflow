using CashFlow.Identity.Db.Data;
using CashFlow.Identity.Db.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Identity.Db.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityDb(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();

        return services;
    }
}
