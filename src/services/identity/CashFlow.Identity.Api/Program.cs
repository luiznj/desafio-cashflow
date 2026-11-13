using CashFlow.Identity.Api.Services;
using CashFlow.Identity.Api.Validators;
using CashFlow.Identity.Db.Extensions;
using CashFlow.Shared.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability("cashflow-identity");

builder.Services.AddIdentityDb(builder.Configuration.GetConnectionString("Default")!);

// Token service criado como singleton: carrega/gera o par RSA uma única vez
var tokenService = new RsaTokenService(builder.Configuration);
builder.Services.AddSingleton<ITokenService>(tokenService);

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<SignUpRequestValidator>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = tokenService.GetPublicSecurityKey()
        };
    });

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CashFlow.Identity.Db.Data.IdentityDbContext>("postgres");

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
