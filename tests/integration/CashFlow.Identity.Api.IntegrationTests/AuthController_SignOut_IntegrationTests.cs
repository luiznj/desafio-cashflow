using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CashFlow.Identity.Api.Dtos;
using FluentAssertions;
using Xunit;

namespace CashFlow.Identity.Api.IntegrationTests;

[Collection("IdentityApi")]
public class AuthController_SignOut_IntegrationTests(IdentityApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> SignUpAndGetRefreshToken()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/sign-up",
            new SignUpRequest($"user_{Guid.NewGuid()}@example.com", "Password1!"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("refreshToken").GetString()!;
    }

    [Fact]
    public async Task SignOut_ValidToken_Returns204NoContent()
    {
        var refreshToken = await SignUpAndGetRefreshToken();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/sign-out",
            new SignOutRequest(refreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SignOut_ValidToken_RefreshTokenNoLongerWorksAfterSignOut()
    {
        var refreshToken = await SignUpAndGetRefreshToken();

        await _client.PostAsJsonAsync("/api/v1/auth/sign-out", new SignOutRequest(refreshToken));

        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequest(refreshToken));

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignOut_UnknownToken_Returns204NoContent()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/sign-out",
            new SignOutRequest("completely-unknown-refresh-token"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
