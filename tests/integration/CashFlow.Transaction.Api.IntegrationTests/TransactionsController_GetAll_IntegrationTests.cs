using CashFlow.Tests.Common;
using CashFlow.Transaction.Db.Data;
using CashFlow.Transaction.Db.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransactionEntry = CashFlow.Transaction.Db.Models.Transaction;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace CashFlow.Transaction.Api.IntegrationTests;

public class TransactionsController_GetAll_IntegrationTests : IClassFixture<TransactionApiFactory>
{
    private readonly TransactionApiFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public TransactionsController_GetAll_IntegrationTests(TransactionApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = _factory.CreateClient();
        var token = TestJwtTokenGenerator.GenerateToken(userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task SeedTransactionAsync(Guid userId, decimal amount, TransactionType type, DateTime occurredAt)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
        db.Add(new TransactionEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Type = type,
            Description = "",
            IdempotencyKey = Guid.NewGuid().ToString(),
            OccurredAt = occurredAt
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAll_NoAuthorizationHeader_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_ValidToken_EmptyDb_Returns200WithEmptyData()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId);

        var response = await client.GetAsync("/api/v1/transactions");
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        doc.RootElement.GetProperty("data").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetAll_MultipleTransactions_GroupedByDayDescending()
    {
        var userId = Guid.NewGuid();
        var day1 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var day2 = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

        await SeedTransactionAsync(userId, 100m, TransactionType.Credit, day1);
        await SeedTransactionAsync(userId, 50m, TransactionType.Credit, day1);
        await SeedTransactionAsync(userId, 200m, TransactionType.Credit, day2);

        var client = CreateAuthenticatedClient(userId);
        var response = await client.GetAsync("/api/v1/transactions");
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = doc.RootElement.GetProperty("data");
        data.GetArrayLength().Should().Be(2);

        // First group should be the most recent day
        var firstGroupDate = data[0].GetProperty("date").GetString();
        firstGroupDate.Should().Be("2026-10-02");
    }

    [Fact]
    public async Task GetAll_FilterByCredit_ReturnsOnlyCredits()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedTransactionAsync(userId, 100m, TransactionType.Credit, now);
        await SeedTransactionAsync(userId, -50m, TransactionType.Debit, now);

        var client = CreateAuthenticatedClient(userId);
        var response = await client.GetAsync("/api/v1/transactions?type=Credit");
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = doc.RootElement.GetProperty("data");
        data.GetArrayLength().Should().Be(1);
        // Only one day group, and its daily balance should be the credit amount
        data[0].GetProperty("dailyBalance").GetDecimal().Should().Be(100m);
    }

    [Fact]
    public async Task GetAll_FilterByDebit_ReturnsOnlyDebits()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SeedTransactionAsync(userId, 100m, TransactionType.Credit, now);
        await SeedTransactionAsync(userId, -75m, TransactionType.Debit, now);

        var client = CreateAuthenticatedClient(userId);
        var response = await client.GetAsync("/api/v1/transactions?type=Debit");
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = doc.RootElement.GetProperty("data");
        data.GetArrayLength().Should().Be(1);
        // Debit is stored as negative; group daily balance is -75
        data[0].GetProperty("dailyBalance").GetDecimal().Should().Be(-75m);
    }

    [Fact]
    public async Task GetAll_Pagination_Page2_ReturnsCorrectSlice()
    {
        var userId = Guid.NewGuid();
        await SeedTransactionAsync(userId, 10m, TransactionType.Credit, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        await SeedTransactionAsync(userId, 20m, TransactionType.Credit, new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc));
        await SeedTransactionAsync(userId, 30m, TransactionType.Credit, new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc));

        var client = CreateAuthenticatedClient(userId);
        var response = await client.GetAsync("/api/v1/transactions?page=2&pageSize=2");
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        doc.RootElement.GetProperty("page").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("data").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("totalItems").GetInt32().Should().Be(3);
        doc.RootElement.GetProperty("totalPages").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task GetAll_UserOnlySeesOwnTransactions()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await SeedTransactionAsync(userA, 100m, TransactionType.Credit, now);
        await SeedTransactionAsync(userB, 999m, TransactionType.Credit, now);

        var clientA = CreateAuthenticatedClient(userA);
        var response = await clientA.GetAsync("/api/v1/transactions");
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // userA should only see their own transaction; dailyBalance should not include userB's 999
        var data = doc.RootElement.GetProperty("data");
        data.GetArrayLength().Should().Be(1);
        data[0].GetProperty("dailyBalance").GetDecimal().Should().Be(100m);
    }
}
