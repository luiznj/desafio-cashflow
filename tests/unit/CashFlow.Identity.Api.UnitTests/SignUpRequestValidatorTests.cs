using CashFlow.Identity.Api.Dtos;
using CashFlow.Identity.Api.Validators;
using FluentAssertions;
using Xunit;

namespace CashFlow.Identity.Api.UnitTests;

public class SignUpRequestValidatorTests
{
    private readonly SignUpRequestValidator _validator = new();

    [Fact]
    public async Task Validate_ValidEmailAndStrongPassword_IsValid()
    {
        var result = await _validator.ValidateAsync(new SignUpRequest("user@example.com", "Password1!"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_PasswordExactly8CharsAllRules_IsValid()
    {
        var result = await _validator.ValidateAsync(new SignUpRequest("user@example.com", "Abc!123X"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyEmail_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new SignUpRequest("", "Password1!"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("obrigatório"));
    }

    [Fact]
    public async Task Validate_MalformedEmail_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new SignUpRequest("not-an-email", "Password1!"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public async Task Validate_EmptyPassword_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new SignUpRequest("user@example.com", ""));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task Validate_PasswordShorterThan8Chars_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new SignUpRequest("user@example.com", "Ab1!"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("8 caracteres"));
    }

    [Fact]
    public async Task Validate_PasswordWithNoUppercase_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new SignUpRequest("user@example.com", "password1!"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("maiúscula"));
    }

    [Fact]
    public async Task Validate_PasswordWithNoLowercase_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new SignUpRequest("user@example.com", "PASSWORD1!"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("minúscula"));
    }

    [Fact]
    public async Task Validate_PasswordWithNoDigit_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new SignUpRequest("user@example.com", "Password!@"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("número"));
    }

    [Fact]
    public async Task Validate_PasswordWithNoSpecialChar_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new SignUpRequest("user@example.com", "Password123"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("especial"));
    }
}
