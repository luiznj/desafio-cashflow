using CashFlow.Identity.Db.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace CashFlow.Identity.Api.IntegrationTests;

public class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private readonly string? _envConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(_envConnectionString))
        {
            _postgres = new PostgreSqlBuilder()
                .WithDatabase("cashflow_identity_test")
                .WithUsername("test")
                .WithPassword("test")
                .Build();
            await _postgres.StartAsync();
        }
    }

    public new async Task DisposeAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.StopAsync();
        }
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var tempKeyPath = Path.GetTempFileName() + ".pem";
        var connectionString = _envConnectionString ?? _postgres!.GetConnectionString();

        if (!string.IsNullOrEmpty(_envConnectionString))
        {
            connectionString = ReplaceDatabase(connectionString, "cashflow_identity_test");
        }

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["Jwt:PrivateKeyPath"] = tempKeyPath,
                ["Jwt:Issuer"] = "cashflow-identity",
                ["Jwt:Audience"] = "cashflow-services",
                ["Jwt:AccessTokenExpiresInMinutes"] = "15",
                ["Jwt:RefreshTokenExpiresInDays"] = "7"
            });
        });

        builder.ConfigureServices(services =>
        {
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.Database.Migrate();
        });
    }

    private static string ReplaceDatabase(string connectionString, string databaseName)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = databaseName
        };
        return builder.ConnectionString;
    }
}
