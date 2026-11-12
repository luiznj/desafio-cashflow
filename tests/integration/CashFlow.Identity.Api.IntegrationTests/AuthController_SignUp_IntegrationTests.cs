using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CashFlow.Identity.Api.Dtos;
using FluentAssertions;
using Xunit;

namespace CashFlow.Identity.Api.IntegrationTests;

[Collection("IdentityApi")]
public class AuthController_SignUp_IntegrationTests(IdentityApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static SignUpRequest ValidRequest(string? email = null) =>
        new(email ?? $"user_{Guid.NewGuid()}@example.com", "Password1!");

    [Fact]
    public async Task SignUp_ValidRequest_Returns201AndTokensInBody()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/sign-up", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("refreshToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("expiresAt").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SignUp_ValidRequest_AccessTokenIsValidJwt()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/sign-up", ValidRequest());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;

        var decoded = new JwtSecurityTokenHandler().ReadJwtToken(token);
        decoded.Should().NotBeNull();
        decoded.Claims.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SignUp_ValidRequest_AccessTokenSubClaimIsGuid()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/sign-up", ValidRequest());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;

        var decoded = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var sub = decoded.Subject;

        Guid.TryParse(sub, out _).Should().BeTrue();
    }

    [Fact]
    public async Task SignUp_DuplicateEmail_Returns409()
    {
        var request = ValidRequest();
        await _client.PostAsJsonAsync("/api/v1/auth/sign-up", request);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/sign-up", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SignUp_WeakPassword_Returns400WithValidationErrors()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/sign-up",
            new SignUpRequest($"user_{Guid.NewGuid()}@example.com", "weak"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SignUp_EmptyEmail_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/sign-up",
            new SignUpRequest("", "Password1!"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
