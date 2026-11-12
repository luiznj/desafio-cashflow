using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using CashFlow.Identity.Api.Services;
using CashFlow.Identity.Db.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CashFlow.Identity.Api.UnitTests;

public class RsaTokenServiceTests : IDisposable
{
    private readonly string _tempKeyPath = Path.GetTempFileName() + ".pem";
    private readonly RsaTokenService _sut;

    public RsaTokenServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "cashflow-services",
                ["Jwt:AccessTokenExpiresInMinutes"] = "15",
                ["Jwt:PrivateKeyPath"] = _tempKeyPath
            })
            .Build();

        _sut = new RsaTokenService(config);
    }

    public void Dispose()
    {
        if (File.Exists(_tempKeyPath))
            File.Delete(_tempKeyPath);
    }

    private static User MakeUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "user@example.com",
        PasswordHash = "hash"
    };

    [Fact]
    public void GenerateAccessToken_ValidUser_ReturnsNonEmptyString()
    {
        var token = _sut.GenerateAccessToken(MakeUser());
        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateAccessToken_ValidUser_ContainsSubClaimEqualToUserId()
    {
        var user = MakeUser();
        var token = _sut.GenerateAccessToken(user);

        var decoded = new JwtSecurityTokenHandler().ReadJwtToken(token);

        decoded.Subject.Should().Be(user.Id.ToString());
    }

    [Fact]
    public void GenerateAccessToken_ValidUser_ContainsEmailClaim()
    {
        var user = MakeUser();
        var token = _sut.GenerateAccessToken(user);

        var decoded = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var emailClaim = decoded.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email);

        emailClaim.Should().NotBeNull();
        emailClaim!.Value.Should().Be(user.Email);
    }

    [Fact]
    public void GenerateAccessToken_ValidUser_ContainsJtiClaim()
    {
        var token = _sut.GenerateAccessToken(MakeUser());

        var decoded = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var jti = decoded.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);

        jti.Should().NotBeNull();
        jti!.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateAccessToken_EachCall_ProducesDifferentJti()
    {
        var user = MakeUser();
        var token1 = _sut.GenerateAccessToken(user);
        var token2 = _sut.GenerateAccessToken(user);

        var jti1 = new JwtSecurityTokenHandler().ReadJwtToken(token1)
            .Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = new JwtSecurityTokenHandler().ReadJwtToken(token2)
            .Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        jti1.Should().NotBe(jti2);
    }

    [Fact]
    public void GenerateAccessToken_ValidUser_UsesRS256Algorithm()
    {
        var token = _sut.GenerateAccessToken(MakeUser());
        var decoded = new JwtSecurityTokenHandler().ReadJwtToken(token);

        decoded.Header.Alg.Should().Be(SecurityAlgorithms.RsaSha256);
    }

    [Fact]
    public void GetPublicSecurityKey_ReturnsRsaSecurityKey()
    {
        var key = _sut.GetPublicSecurityKey();
        key.Should().BeOfType<RsaSecurityKey>();
    }

    [Fact]
    public void GetJwks_ReturnsObjectWithRsaKeyProperties()
    {
        var jwks = _sut.GetJwks();

        var json = JsonSerializer.Serialize(jwks);
        var doc = JsonDocument.Parse(json);
        var key = doc.RootElement.GetProperty("keys")[0];

        key.GetProperty("kty").GetString().Should().Be("RSA");
        key.GetProperty("use").GetString().Should().Be("sig");
        key.GetProperty("alg").GetString().Should().Be("RS256");
        key.GetProperty("n").GetString().Should().NotBeNullOrEmpty();
        key.GetProperty("e").GetString().Should().NotBeNullOrEmpty();
    }
}
