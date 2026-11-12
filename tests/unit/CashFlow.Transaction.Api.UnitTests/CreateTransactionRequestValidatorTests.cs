using CashFlow.Transaction.Api.Dtos;
using CashFlow.Transaction.Api.Validators;
using FluentAssertions;
using Xunit;

namespace CashFlow.Transaction.Api.UnitTests;

public class CreateTransactionRequestValidatorTests
{
    private readonly CreateTransactionRequestValidator _validator = new();

    [Fact]
    public async Task Validate_CreditType_PositiveAmount_IsValid()
    {
        var result = await _validator.ValidateAsync(new CreateTransactionRequest(100m, "Credit"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_DebitType_PositiveAmount_IsValid()
    {
        var result = await _validator.ValidateAsync(new CreateTransactionRequest(50m, "Debit"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_TypeCredit_CaseInsensitive_IsValid()
    {
        var result = await _validator.ValidateAsync(new CreateTransactionRequest(100m, "credit"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_TypeDebit_CaseInsensitive_IsValid()
    {
        var result = await _validator.ValidateAsync(new CreateTransactionRequest(100m, "DEBIT"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ZeroAmount_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new CreateTransactionRequest(0m, "Credit"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_NegativeAmount_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new CreateTransactionRequest(-10m, "Credit"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_EmptyType_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new CreateTransactionRequest(100m, ""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_InvalidType_Transfer_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new CreateTransactionRequest(100m, "Transfer"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_NullType_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new CreateTransactionRequest(100m, null!));
        result.IsValid.Should().BeFalse();
    }
}
