using CashFlow.Transaction.Api.Controllers;
using CashFlow.Transaction.Api.Dtos;
using CashFlow.Transaction.Api.Services;
using CashFlow.Transaction.Api.Validators;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using System.Security.Claims;
using Xunit;

namespace CashFlow.Transaction.Api.UnitTests;

public class TransactionsControllerTests
{
    private readonly ITransactionService _service = Substitute.For<ITransactionService>();
    private readonly CreateTransactionRequestValidator _validator = new();
    private readonly Guid _userId = Guid.NewGuid();

    private TransactionsController BuildController(string? idempotencyKey = "auto")
    {
        var controller = new TransactionsController(_service, _validator);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, _userId.ToString()) };
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        if (idempotencyKey == "auto")
            httpContext.Request.Headers.Append("Idempotency-Key", Guid.NewGuid().ToString());
        else if (idempotencyKey is not null)
            httpContext.Request.Headers.Append("Idempotency-Key", idempotencyKey);

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static PagedResult<DailyTransactionsResponse> EmptyPagedResult() =>
        new(1, 7, 0, 0, []);

    private static TransactionResponse MakeResponse() =>
        new(Guid.NewGuid(), 100m, "Credit", null, DateTime.UtcNow);

    [Fact]
    public async Task Create_MissingIdempotencyKeyHeader_Returns400()
    {
        var controller = BuildController(idempotencyKey: null);

        var result = await controller.Create(new CreateTransactionRequest(100m, "Credit"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_NonGuidIdempotencyKeyHeader_Returns400()
    {
        var controller = BuildController(idempotencyKey: "not-a-guid");

        var result = await controller.Create(new CreateTransactionRequest(100m, "Credit"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_InvalidBody_ValidatorFails_Returns400()
    {
        var controller = BuildController();

        var result = await controller.Create(new CreateTransactionRequest(0m, "Credit"));

        result.Should().BeOfType<BadRequestObjectResult>();
        await _service.DidNotReceive().CreateAsync(Arg.Any<Guid>(), Arg.Any<CreateTransactionRequest>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Create_ValidRequest_ServiceReturnsIsNewTrue_Returns201()
    {
        var key = Guid.NewGuid().ToString();
        var controller = BuildController(idempotencyKey: key);
        _service.CreateAsync(_userId, Arg.Any<CreateTransactionRequest>(), key)
            .Returns((MakeResponse(), true));

        var result = await controller.Create(new CreateTransactionRequest(100m, "Credit"));

        result.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    public async Task Create_ValidRequest_ServiceReturnsIsNewFalse_Returns409()
    {
        var key = Guid.NewGuid().ToString();
        var controller = BuildController(idempotencyKey: key);
        _service.CreateAsync(_userId, Arg.Any<CreateTransactionRequest>(), key)
            .Returns((MakeResponse(), false));

        var result = await controller.Create(new CreateTransactionRequest(100m, "Credit"));

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Create_ExtractsUserIdFromSubClaim()
    {
        var key = Guid.NewGuid().ToString();
        var controller = BuildController(idempotencyKey: key);
        _service.CreateAsync(Arg.Any<Guid>(), Arg.Any<CreateTransactionRequest>(), Arg.Any<string>())
            .Returns((MakeResponse(), true));

        await controller.Create(new CreateTransactionRequest(100m, "Credit"));

        await _service.Received(1).CreateAsync(
            _userId,
            Arg.Any<CreateTransactionRequest>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task GetAll_InvalidType_Returns400()
    {
        var controller = BuildController();

        var result = await controller.GetAll(new GetTransactionsQuery { Type = "Transfer" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetAll_ValidRequest_Returns200()
    {
        var controller = BuildController();
        _service.GetAsync(_userId, Arg.Any<GetTransactionsQuery>()).Returns(EmptyPagedResult());

        var result = await controller.GetAll(new GetTransactionsQuery());

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAll_PageLessThan1_NormalizesTo1()
    {
        var controller = BuildController();
        _service.GetAsync(_userId, Arg.Any<GetTransactionsQuery>()).Returns(EmptyPagedResult());
        var query = new GetTransactionsQuery { Page = 0, PageSize = 10 };

        await controller.GetAll(query);

        await _service.Received(1).GetAsync(_userId, Arg.Is<GetTransactionsQuery>(q => q.Page == 1));
    }

    [Fact]
    public async Task GetAll_PageSizeGreaterThan100_NormalizesTo7()
    {
        var controller = BuildController();
        _service.GetAsync(_userId, Arg.Any<GetTransactionsQuery>()).Returns(EmptyPagedResult());
        var query = new GetTransactionsQuery { Page = 1, PageSize = 150 };

        await controller.GetAll(query);

        await _service.Received(1).GetAsync(_userId, Arg.Is<GetTransactionsQuery>(q => q.PageSize == 7));
    }
}
