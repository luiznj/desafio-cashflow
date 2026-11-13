using CashFlow.Transaction.Api.Services;
using CashFlow.Transaction.Db.Data;
using CashFlow.Transaction.Api.Validators;
using CashFlow.Transaction.Db.Extensions;
using CashFlow.Shared.Observability;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability("cashflow-transaction");

builder.Services.AddTransactionDb(builder.Configuration.GetConnectionString("Default")!);

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"], builder.Configuration["RabbitMq:VirtualHost"], h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"]!);
            h.Password(builder.Configuration["RabbitMq:Password"]!);
        });

        cfg.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15)));

        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Jwt:Authority"];
        options.Audience = builder.Configuration["Jwt:Audience"];
        options.RequireHttpsMetadata = false;
    });

builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<CreateTransactionRequestValidator>();
builder.Services.AddHostedService<OutboxDeliveryService>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CashFlow.Transaction.Db.Data.TransactionDbContext>("postgres");

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

app.UseExceptionHandler(err => err.Run(async ctx =>
{
    var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogError(feature?.Error, "Exceção não tratada");
    ctx.Response.StatusCode = 500;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(new { message = "Ocorreu um erro interno. Tente novamente mais tarde." });
}));

app.UseAuthentication();
app.UseAuthorization();
app.MapPrometheusScrapingEndpoint();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
