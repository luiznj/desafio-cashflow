using CashFlow.Consolidation.Api.Services;
using CashFlow.Consolidation.Db.Models;
using CashFlow.Consolidation.Db.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CashFlow.Consolidation.Api.UnitTests;

public class BalanceServiceTests
{
    private readonly IDailyBalanceRepository _repository = Substitute.For<IDailyBalanceRepository>();
    private readonly BalanceService _sut;

    public BalanceServiceTests()
    {
        _sut = new BalanceService(_repository);
    }

    [Fact]
    public async Task GetAsync_ExistingBalance_ReturnsTotalCreditsAndDebitsAndBalance()
    {
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 11);
        _repository.GetByUserAndDateAsync(userId, date).Returns(new DailyBalance
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Date = date,
            TotalCredits = 100m,
            TotalDebits = 30m
        });

        var result = await _sut.GetAsync(userId, date);

        result.TotalCredits.Should().Be(100m);
        result.TotalDebits.Should().Be(30m);
        result.Balance.Should().Be(70m);
    }

    [Fact]
    public async Task GetAsync_ExistingBalance_BalanceIsCreditsMinusDebits()
    {
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 10);
        _repository.GetByUserAndDateAsync(userId, date).Returns(new DailyBalance
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Date = date,
            TotalCredits = 200m,
            TotalDebits = 150m
        });

        var result = await _sut.GetAsync(userId, date);

        result.Balance.Should().Be(50m); // 200 - 150, not 150 - 200
    }

    [Fact]
    public async Task GetAsync_NoRecord_ReturnsAllZeros()
    {
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 11);
        _repository.GetByUserAndDateAsync(userId, date).Returns((DailyBalance?)null);

        var result = await _sut.GetAsync(userId, date);

        result.TotalCredits.Should().Be(0m);
        result.TotalDebits.Should().Be(0m);
        result.Balance.Should().Be(0m);
    }

    [Fact]
    public async Task GetAsync_NoRecord_ReturnsResponseWithoutThrowingException()
    {
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 11);
        _repository.GetByUserAndDateAsync(userId, date).Returns((DailyBalance?)null);

        var act = async () => await _sut.GetAsync(userId, date);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetAsync_NoRecord_ReturnsCorrectDate()
    {
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 11);
        _repository.GetByUserAndDateAsync(userId, date).Returns((DailyBalance?)null);

        var result = await _sut.GetAsync(userId, date);

        result.Date.Should().Be(date);
    }

    [Fact]
    public async Task GetAsync_PassesCorrectUserIdAndDateToRepository()
    {
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 11);
        _repository.GetByUserAndDateAsync(userId, date).Returns((DailyBalance?)null);

        await _sut.GetAsync(userId, date);

        await _repository.Received(1).GetByUserAndDateAsync(userId, date);
    }
}
