using CashFlow.Consolidation.Db.Repositories;
using CashFlow.Tests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace CashFlow.Consolidation.Api.IntegrationTests;

public class BalancesController_IntegrationTests : IClassFixture<ConsolidationApiFactory>
{
    private readonly ConsolidationApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public BalancesController_IntegrationTests(ConsolidationApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private string AuthHeader(Guid? userId = null) =>
        TestJwtTokenGenerator.GenerateToken(
            userId ?? Guid.NewGuid(), "user@test.com", "test-issuer", "cashflow-services");

    private void SetAuth(Guid userId) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", AuthHeader(userId));

    private async Task SeedBalanceAsync(Guid userId, DateOnly date, decimal credits, decimal debits)
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IDailyBalanceRepository>();
        if (credits > 0)
            await repo.ApplyTransactionAsync(Guid.NewGuid(), userId, date, credits);
        if (debits > 0)
            await repo.ApplyTransactionAsync(Guid.NewGuid(), userId, date, -debits);
    }

    [Fact]
    public async Task GetBalance_NoAuthorizationHeader_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/v1/balances?date=2026-11-11");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBalance_ExpiredToken_Returns401()
    {
        var token = TestJwtTokenGenerator.GenerateExpiredToken(Guid.NewGuid());
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/balances?date=2026-11-11");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBalance_InvalidDateFormat_Returns400()
    {
        SetAuth(Guid.NewGuid());

        var response = await _client.GetAsync("/api/v1/balances?date=11/12/2026");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBalance_MissingDate_Returns400()
    {
        SetAuth(Guid.NewGuid());

        var response = await _client.GetAsync("/api/v1/balances");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBalance_NoRecordForDate_Returns200WithZeroBalance()
    {
        var userId = Guid.NewGuid();
        SetAuth(userId);

        var response = await _client.GetAsync("/api/v1/balances?date=2026-01-01");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("totalCredits").GetDecimal().Should().Be(0m);
        json.GetProperty("totalDebits").GetDecimal().Should().Be(0m);
        json.GetProperty("balance").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task GetBalance_ExistingRecord_Returns200WithCorrectValues()
    {
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 12);
        await SeedBalanceAsync(userId, date, credits: 100m, debits: 30m);
        SetAuth(userId);

        var response = await _client.GetAsync($"/api/v1/balances?date={date:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("totalCredits").GetDecimal().Should().Be(100m);
        json.GetProperty("totalDebits").GetDecimal().Should().Be(30m);
        json.GetProperty("balance").GetDecimal().Should().Be(70m);
    }

    [Fact]
    public async Task GetBalance_UserOnlySeesOwnBalance()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 13);

        await SeedBalanceAsync(userA, date, credits: 500m, debits: 0m);
        await SeedBalanceAsync(userB, date, credits: 200m, debits: 50m);

        // User A sees their own balance
        SetAuth(userA);
        var responseA = await _client.GetAsync($"/api/v1/balances?date={date:yyyy-MM-dd}");
        responseA.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyA = await responseA.Content.ReadAsStringAsync();
        var jsonA = JsonDocument.Parse(bodyA).RootElement;
        jsonA.GetProperty("totalCredits").GetDecimal().Should().Be(500m);

        // User B sees their own balance
        SetAuth(userB);
        var responseB = await _client.GetAsync($"/api/v1/balances?date={date:yyyy-MM-dd}");
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyB = await responseB.Content.ReadAsStringAsync();
        var jsonB = JsonDocument.Parse(bodyB).RootElement;
        jsonB.GetProperty("totalCredits").GetDecimal().Should().Be(200m);
        jsonB.GetProperty("totalDebits").GetDecimal().Should().Be(50m);
    }
}
