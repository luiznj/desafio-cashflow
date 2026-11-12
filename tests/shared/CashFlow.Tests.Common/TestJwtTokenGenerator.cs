using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace CashFlow.Tests.Common;

public static class TestJwtTokenGenerator
{
    private static readonly RSA _rsa = RSA.Create(2048);

    public static RsaSecurityKey PublicKey => new(RSA.Create(_rsa.ExportParameters(false)));

    private static RsaSecurityKey PrivateKey => new(_rsa);

    public static string GenerateToken(
        Guid userId,
        string email = "test@example.com",
        string issuer = "test-issuer",
        string audience = "cashflow-services",
        DateTime? expires = null)
    {
        var credentials = new SigningCredentials(PrivateKey, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            expires: expires ?? DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GenerateExpiredToken(Guid userId) =>
        GenerateToken(userId, expires: DateTime.UtcNow.AddMinutes(-1));

    public static string GenerateTokenWithWrongAudience(Guid userId) =>
        GenerateToken(userId, audience: "wrong-audience");

    public static string GenerateTokenSignedByDifferentKey(Guid userId)
    {
        using var otherRsa = RSA.Create(2048);
        var credentials = new SigningCredentials(new RsaSecurityKey(otherRsa), SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "cashflow-services",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())],
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
