using CashFlow.Tests.Common;
using CashFlow.Transaction.Db.Data;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

namespace CashFlow.Transaction.Api.IntegrationTests;

public class TransactionApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private readonly string? _envConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
    private ITestHarness? _harness;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(_envConnectionString))
        {
            _postgres = new PostgreSqlBuilder()
                .WithDatabase("cashflow_transaction_test")
                .WithUsername("test")
                .WithPassword("test")
                .Build();
            await _postgres.StartAsync();
        }
    }

    public new async Task DisposeAsync()
    {
        if (_harness is not null)
            await _harness.Stop();
        if (_postgres is not null)
            await _postgres.StopAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = _envConnectionString ?? _postgres!.GetConnectionString();


        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["Outbox:DeliveryIntervalMs"] = "100"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove all MassTransit registrations to replace RabbitMQ with in-memory harness
            var toRemove = services
                .Where(d =>
                    (d.ServiceType.FullName ?? string.Empty).Contains("MassTransit") ||
                    (d.ImplementationType?.FullName ?? string.Empty).Contains("MassTransit"))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddMassTransitTestHarness();

            // Override JWT: bypass OIDC authority discovery and use a static RSA key
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "test-issuer",
                    ValidateAudience = true,
                    ValidAudience = "cashflow-services",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TestJwtTokenGenerator.PublicKey,
                    ClockSkew = TimeSpan.Zero
                };
            });
        });

        builder.UseEnvironment("Test");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
            db.Database.Migrate();
        }

        _harness = host.Services.GetRequiredService<ITestHarness>();
        _harness.Start().GetAwaiter().GetResult();

        return host;
    }
}
