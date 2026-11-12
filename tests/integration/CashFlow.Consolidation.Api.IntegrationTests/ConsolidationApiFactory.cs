using CashFlow.Consolidation.Api.Messaging;
using CashFlow.Consolidation.Db.Data;
using CashFlow.Tests.Common;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

namespace CashFlow.Consolidation.Api.IntegrationTests;

public class ConsolidationApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private readonly string? _envConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(_envConnectionString))
        {
            _postgres = new PostgreSqlBuilder()
                .WithDatabase("cashflow_test")
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
        var connectionString = _envConnectionString ?? _postgres!.GetConnectionString();


        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove all MassTransit registrations (RabbitMQ transport)
            var massTransitDescriptors = services
                .Where(d => d.ServiceType.Namespace?.StartsWith("MassTransit") == true)
                .ToList();
            foreach (var d in massTransitDescriptors)
                services.Remove(d);

            // Add in-memory test harness with the consumer
            services.AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<TransactionCreatedConsumer>();
            });

            // Override JWT: replace OIDC-based auth with static key validation
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.BackchannelHttpHandler = null;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "test-issuer",
                    ValidateAudience = true,
                    ValidAudience = "cashflow-services",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TestJwtTokenGenerator.PublicKey
                };
            });
        });

        builder.ConfigureServices(services =>
        {
            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
            db.Database.Migrate();
        });
    }

}
