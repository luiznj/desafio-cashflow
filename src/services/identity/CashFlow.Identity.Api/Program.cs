using CashFlow.Identity.Api.Services;
using CashFlow.Identity.Api.Validators;
using CashFlow.Identity.Db.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
