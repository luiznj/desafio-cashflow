using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace CashFlow.Identity.Api.IntegrationTests;

public class JwksController_IntegrationTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetJwks_Returns200()
    {
        var response = await _client.GetAsync("/.well-known/jwks.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetJwks_KeyTypeIsRsa()
    {
        var response = await _client.GetAsync("/.well-known/jwks.json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var kty = body.GetProperty("keys")[0].GetProperty("kty").GetString();
        kty.Should().Be("RSA");
    }

    [Fact]
    public async Task GetJwks_AlgorithmIsRS256()
    {
        var response = await _client.GetAsync("/.well-known/jwks.json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var alg = body.GetProperty("keys")[0].GetProperty("alg").GetString();
        alg.Should().Be("RS256");
    }

    [Fact]
    public async Task GetJwks_ContainsModulusAndExponent()
    {
        var response = await _client.GetAsync("/.well-known/jwks.json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var key = body.GetProperty("keys")[0];
        key.GetProperty("n").GetString().Should().NotBeNullOrEmpty();
        key.GetProperty("e").GetString().Should().NotBeNullOrEmpty();
    }
}
