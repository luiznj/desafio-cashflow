using CashFlow.Consolidation.Api.Controllers;
using CashFlow.Consolidation.Api.Dtos;
using CashFlow.Consolidation.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using System.Security.Claims;
using Xunit;

namespace CashFlow.Consolidation.Api.UnitTests;

public class BalancesControllerTests
{
    private readonly IBalanceService _service = Substitute.For<IBalanceService>();
    private readonly BalancesController _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public BalancesControllerTests()
    {
        _sut = new BalancesController(_service)
        {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, _userId.ToString())], "test"))
            }
        }
        };
    }

    [Fact]
    public async Task GetBalance_ValidDate_Returns200WithBalance()
    {
        var date = new DateOnly(2026, 11, 11);
        var response = new DailyBalanceResponse(date, 100m, 30m, 70m);
        _service.GetAsync(_userId, date).Returns(response);

        var result = await _sut.GetBalance("2026-11-11");

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().Be(response);
    }

    [Fact]
    public async Task GetBalance_InvalidDateFormat_Returns400WithMessage()
    {
        var result = await _sut.GetBalance("11/12/2026");

        result.Should().BeOfType<BadRequestObjectResult>();
        await _service.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>());
    }

    [Fact]
    public async Task GetBalance_MalformedDate_Returns400()
    {
        var result = await _sut.GetBalance("not-a-date");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetBalance_MissingDate_Returns400()
    {
        var result = await _sut.GetBalance(null!);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetBalance_ExtractsUserIdFromSubClaim()
    {
        var date = new DateOnly(2026, 11, 11);
        _service.GetAsync(_userId, date)
            .Returns(new DailyBalanceResponse(date, 0m, 0m, 0m));

        await _sut.GetBalance("2026-11-11");

        await _service.Received(1).GetAsync(_userId, date);
    }

    [Fact]
    public async Task GetBalance_CorrectDatePassedToService()
    {
        var expectedDate = new DateOnly(2026, 11, 11);
        _service.GetAsync(_userId, expectedDate)
            .Returns(new DailyBalanceResponse(expectedDate, 0m, 0m, 0m));

        await _sut.GetBalance("2026-11-11");

        await _service.Received(1).GetAsync(Arg.Any<Guid>(), expectedDate);
    }
}
