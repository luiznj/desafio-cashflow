using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CashFlow.Identity.Api.Dtos;
using FluentAssertions;
using Xunit;

namespace CashFlow.Identity.Api.IntegrationTests;

[Collection("IdentityApi")]
public class AuthController_Refresh_IntegrationTests(IdentityApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(string AccessToken, string RefreshToken)> SignUpAsync()
    {
        var email = $"user_{Guid.NewGuid()}@example.com";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/sign-up",
            new SignUpRequest(email, "Password1!"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (
            body.GetProperty("accessToken").GetString()!,
            body.GetProperty("refreshToken").GetString()!
        );
    }

    [Fact]
    public async Task Refresh_ValidRefreshToken_Returns200WithNewTokens()
    {
        var (_, refreshToken) = await SignUpAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequest(refreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("refreshToken").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Refresh_ValidRefreshToken_OldRefreshTokenBecomeInvalid()
    {
        var (_, refreshToken) = await SignUpAsync();

        await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(refreshToken));

        var secondRefresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequest(refreshToken));

        secondRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_RevokedToken_Returns401()
    {
        var (_, refreshToken) = await SignUpAsync();

        await _client.PostAsJsonAsync("/api/v1/auth/sign-out", new SignOutRequest(refreshToken));

        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequest(refreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_UnknownToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequest("completely-unknown-token"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
