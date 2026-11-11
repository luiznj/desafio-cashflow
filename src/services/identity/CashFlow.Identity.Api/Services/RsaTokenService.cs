using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using CashFlow.Identity.Db.Models;
using Microsoft.IdentityModel.Tokens;

namespace CashFlow.Identity.Api.Services;

public class RsaTokenService : ITokenService
{
    private readonly RSA _signingRsa;
    private readonly RSA _publicRsa;
    private readonly IConfiguration _configuration;

    public RsaTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
        _signingRsa = LoadOrCreateKeyPair(configuration["Jwt:PrivateKeyPath"] ?? "keys/private.pem");

        var publicParams = _signingRsa.ExportParameters(includePrivateParameters: false);
        _publicRsa = RSA.Create();
        _publicRsa.ImportParameters(publicParams);
    }

    private static RSA LoadOrCreateKeyPair(string keyPath)
    {
        var rsa = RSA.Create(2048);

        if (File.Exists(keyPath))
        {
            rsa.ImportFromPem(File.ReadAllText(keyPath));
            return rsa;
        }

        var directory = Path.GetDirectoryName(keyPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(keyPath, rsa.ExportRSAPrivateKeyPem());
        return rsa;
    }

    public string GenerateAccessToken(User user)
    {
        var key = new RsaSecurityKey(_signingRsa);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var expiresInMinutes = _configuration.GetValue<int>("Jwt:AccessTokenExpiresInMinutes", 15);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public SecurityKey GetPublicSecurityKey() => new RsaSecurityKey(_publicRsa);

    public object GetJwks()
    {
        var rsaParams = _publicRsa.ExportParameters(includePrivateParameters: false);
        return new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    n = Base64UrlEncoder.Encode(rsaParams.Modulus!),
                    e = Base64UrlEncoder.Encode(rsaParams.Exponent!)
                }
            }
        };
    }
}
