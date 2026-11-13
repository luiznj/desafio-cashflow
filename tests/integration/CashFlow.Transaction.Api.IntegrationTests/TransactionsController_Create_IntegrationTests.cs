using CashFlow.Shared.Messaging;
using CashFlow.Tests.Common;
using FluentAssertions;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CashFlow.Transaction.Api.IntegrationTests;

[Collection("TransactionApi")]
public class TransactionsController_Create_IntegrationTests
{
    private readonly TransactionApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TransactionsController_Create_IntegrationTests(TransactionApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthenticatedClient(Guid? userId = null)
    {
        var client = _factory.CreateClient();
        var token = TestJwtTokenGenerator.GenerateToken(userId ?? Guid.NewGuid());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static HttpRequestMessage MakeCreateRequest(object body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transactions")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        return request;
    }

    [Fact]
    public async Task Create_NoAuthorizationHeader_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(MakeCreateRequest(new { amount = 100, type = "Credit" }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ExpiredToken_Returns401()
    {
        var client = _factory.CreateClient();
        var token = TestJwtTokenGenerator.GenerateExpiredToken(Guid.NewGuid());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(MakeCreateRequest(new { amount = 100, type = "Credit" }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_TokenWithWrongAudience_Returns401()
    {
        var client = _factory.CreateClient();
        var token = TestJwtTokenGenerator.GenerateTokenWithWrongAudience(Guid.NewGuid());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(MakeCreateRequest(new { amount = 100, type = "Credit" }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_TokenSignedByDifferentKey_Returns401()
    {
        var client = _factory.CreateClient();
        var token = TestJwtTokenGenerator.GenerateTokenSignedByDifferentKey(Guid.NewGuid());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(MakeCreateRequest(new { amount = 100, type = "Credit" }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ValidCreditRequest_Returns201()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.SendAsync(MakeCreateRequest(new { amount = 100, type = "Credit" }));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_ValidDebitRequest_Returns201()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.SendAsync(MakeCreateRequest(new { amount = 50, type = "Debit" }));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_ValidRequest_ResponseBodyContainsTransactionId()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.SendAsync(MakeCreateRequest(new { amount = 100, type = "Credit" }));
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        doc.RootElement.GetProperty("id").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Create_ValidCreditRequest_ResponseAmountIsAbsoluteValue()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.SendAsync(MakeCreateRequest(new { amount = 75.50m, type = "Credit" }));
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        doc.RootElement.GetProperty("amount").GetDecimal().Should().Be(75.50m);
    }

    [Fact]
    public async Task Create_DuplicateIdempotencyKey_Returns409()
    {
        var client = CreateAuthenticatedClient();
        var key = Guid.NewGuid().ToString();
        var body = new { amount = 100, type = "Credit" };

        var first = await client.SendAsync(MakeCreateRequest(body, key));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.SendAsync(MakeCreateRequest(body, key));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_MissingIdempotencyKeyHeader_Returns400()
    {
        var client = CreateAuthenticatedClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transactions")
        {
            Content = JsonContent.Create(new { amount = 100, type = "Credit" })
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_NonGuidIdempotencyKey_Returns400()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.SendAsync(MakeCreateRequest(new { amount = 100, type = "Credit" }, "not-a-guid"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ZeroAmount_Returns400()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.SendAsync(MakeCreateRequest(new { amount = 0, type = "Credit" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_InvalidType_Returns400()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.SendAsync(MakeCreateRequest(new { amount = 100, type = "Transfer" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ValidRequest_PublishesTransactionCreatedEvent()
    {
        var client = CreateAuthenticatedClient();
        var harness = _factory.Services.GetRequiredService<ITestHarness>();

        var response = await client.SendAsync(MakeCreateRequest(new { amount = 100, type = "Credit" }));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        (await harness.Published.Any<TransactionCreatedEvent>()).Should().BeTrue();
    }

    [Fact]
    public async Task Create_DuplicateIdempotencyKey_DoesNotPublishSecondEvent()
    {
        var client = CreateAuthenticatedClient();
        var harness = _factory.Services.GetRequiredService<ITestHarness>();
        var key = Guid.NewGuid().ToString();
        var body = new { amount = 100, type = "Credit" };

        // First request succeeds
        var first = await client.SendAsync(MakeCreateRequest(body, key));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var transactionId = JsonDocument.Parse(await first.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetGuid();

        // Duplicate request returns 409
        var second = await client.SendAsync(MakeCreateRequest(body, key));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Wait until the outbox delivers the event from the first (valid) request
        await harness.Published.Any<TransactionCreatedEvent>(
            x => x.Context.Message.TransactionId == transactionId);

        // The duplicate must not have produced a second event
        var matchingEvents = harness.Published
            .Select<TransactionCreatedEvent>()
            .Where(evt => evt.Context.Message.TransactionId == transactionId)
            .ToList();
        matchingEvents.Should().HaveCount(1);
    }
}
