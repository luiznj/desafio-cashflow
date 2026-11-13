using CashFlow.Shared.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability("cashflow-gateway");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Jwt:Authority"];
        options.Audience = builder.Configuration["Jwt:Audience"];
        options.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser());
});

// Rotas de autenticação: limite estrito por IP para proteção contra brute-force
var authPermitLimit  = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit",  20);
var authWindowSeconds = builder.Configuration.GetValue("RateLimiting:Auth:WindowSeconds", 60);

// Rotas de API autenticadas: limite por userId (generoso o suficiente para o pico de 50 req/s)
var apiPermitLimit   = builder.Configuration.GetValue("RateLimiting:Api:PermitLimit",   6000);
var apiWindowSeconds  = builder.Configuration.GetValue("RateLimiting:Api:WindowSeconds",  60);

builder.Services.AddRateLimiter(options =>
{
    // UseRateLimiter é registrado após UseAuthentication para que ctx.User já contenha as claims do JWT
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api/v1/auth"))
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter($"auth:{ip}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermitLimit,
                    Window      = TimeSpan.FromSeconds(authWindowSeconds),
                    QueueLimit  = 0
                });
        }

        var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? ctx.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"api:{userId}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = apiPermitLimit,
                Window      = TimeSpan.FromSeconds(apiWindowSeconds),
                QueueLimit  = 0
            });
    });
    options.RejectionStatusCode = 429;
});

builder.Services.AddHealthChecks();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseExceptionHandler(err => err.Run(async ctx =>
{
    var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogError(feature?.Error, "Exceção não tratada no Gateway");
    ctx.Response.StatusCode = 500;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(new { message = "Ocorreu um erro interno. Tente novamente mais tarde." });
}));

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapPrometheusScrapingEndpoint();
app.MapReverseProxy();
app.MapHealthChecks("/health");

app.Run();
