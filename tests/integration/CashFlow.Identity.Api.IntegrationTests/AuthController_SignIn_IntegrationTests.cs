using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CashFlow.Identity.Api.Dtos;
using FluentAssertions;
using Xunit;

namespace CashFlow.Identity.Api.IntegrationTests;

[Collection("IdentityApi")]
public class AuthController_SignIn_IntegrationTests(IdentityApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static SignUpRequest MakeSignUp(string email) => new(email, "Password1!");

    private async Task<string> SignUpAndGetEmail()
    {
        var email = $"user_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/v1/auth/sign-up", MakeSignUp(email));
        return email;
    }

    [Fact]
    public async Task SignIn_ValidCredentials_Returns200WithTokens()
    {
        var email = await SignUpAndGetEmail();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/sign-in",
            new SignInRequest(email, "Password1!"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("refreshToken").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SignIn_UnknownEmail_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/sign-in",
            new SignInRequest($"nobody_{Guid.NewGuid()}@example.com", "Password1!"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignIn_WrongPassword_Returns401WithOpaqueMessage()
    {
        var email = await SignUpAndGetEmail();

        var wrongPasswordResponse = await _client.PostAsJsonAsync("/api/v1/auth/sign-in",
            new SignInRequest(email, "WrongPassword1!"));

        var unknownEmailResponse = await _client.PostAsJsonAsync("/api/v1/auth/sign-in",
            new SignInRequest($"nobody_{Guid.NewGuid()}@example.com", "Password1!"));

        wrongPasswordResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body1 = await wrongPasswordResponse.Content.ReadFromJsonAsync<JsonElement>();
        var body2 = await unknownEmailResponse.Content.ReadFromJsonAsync<JsonElement>();

        body1.GetProperty("message").GetString()
            .Should().Be(body2.GetProperty("message").GetString());
    }

    [Fact]
    public async Task SignIn_ValidCredentials_AccessTokenSubClaimMatchesSignedUpUserId()
    {
        var testEmail = $"subtest_{Guid.NewGuid()}@example.com";

        var suResp = await _client.PostAsJsonAsync("/api/v1/auth/sign-up",
            new SignUpRequest(testEmail, "Password1!"));
        var suBody = await suResp.Content.ReadFromJsonAsync<JsonElement>();
        var suSub = new JwtSecurityTokenHandler().ReadJwtToken(
            suBody.GetProperty("accessToken").GetString()!).Subject;

        var siResp = await _client.PostAsJsonAsync("/api/v1/auth/sign-in",
            new SignInRequest(testEmail, "Password1!"));
        var siBody = await siResp.Content.ReadFromJsonAsync<JsonElement>();
        var siSub = new JwtSecurityTokenHandler().ReadJwtToken(
            siBody.GetProperty("accessToken").GetString()!).Subject;

        suSub.Should().Be(siSub);
    }
}
