using CashFlow.Identity.Api.Controllers;
using CashFlow.Identity.Api.Dtos;
using CashFlow.Identity.Api.Services;
using CashFlow.Identity.Api.Validators;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CashFlow.Identity.Api.UnitTests;

public class AuthControllerTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly SignUpRequestValidator _validator = new SignUpRequestValidator();

    private AuthController CreateSut()
    {
        var controller = new AuthController(_authService, _validator);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    private static AuthResponse MakeAuthResponse() =>
        new("access-token", "refresh-token", DateTime.UtcNow.AddMinutes(15));

    [Fact]
    public async Task SignUp_ValidRequest_Returns201Created()
    {
        _authService.SignUpAsync(Arg.Any<SignUpRequest>()).Returns(MakeAuthResponse());

        var result = await CreateSut().SignUp(new SignUpRequest("user@example.com", "Password1!"));

        result.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    public async Task SignUp_InvalidRequest_WeakPassword_Returns400WithValidationErrors()
    {
        var result = await CreateSut().SignUp(new SignUpRequest("user@example.com", "weak"));

        result.Should().BeOfType<BadRequestObjectResult>();
        await _authService.DidNotReceive().SignUpAsync(Arg.Any<SignUpRequest>());
    }

    [Fact]
    public async Task SignUp_InvalidRequest_EmptyEmail_Returns400WithValidationErrors()
    {
        var result = await CreateSut().SignUp(new SignUpRequest("", "Password1!"));

        result.Should().BeOfType<BadRequestObjectResult>();
        await _authService.DidNotReceive().SignUpAsync(Arg.Any<SignUpRequest>());
    }

    [Fact]
    public async Task SignUp_DuplicateEmailException_Returns409Conflict()
    {
        _authService.SignUpAsync(Arg.Any<SignUpRequest>()).ThrowsAsync(new DuplicateEmailException());

        var result = await CreateSut().SignUp(new SignUpRequest("existing@example.com", "Password1!"));

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task SignIn_ValidCredentials_Returns200Ok()
    {
        _authService.SignInAsync(Arg.Any<SignInRequest>()).Returns(MakeAuthResponse());

        var result = await CreateSut().SignIn(new SignInRequest("user@example.com", "Password1!"));

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().BeOfType<AuthResponse>();
    }

    [Fact]
    public async Task SignIn_InvalidCredentials_Returns401Unauthorized()
    {
        _authService.SignInAsync(Arg.Any<SignInRequest>()).ThrowsAsync(new InvalidCredentialsException());

        var result = await CreateSut().SignIn(new SignInRequest("user@example.com", "wrong"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Refresh_ValidToken_Returns200Ok()
    {
        _authService.RefreshAsync(Arg.Any<string>()).Returns(MakeAuthResponse());

        var result = await CreateSut().Refresh(new RefreshRequest("valid-refresh-token"));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Refresh_InvalidRefreshToken_Returns401Unauthorized()
    {
        _authService.RefreshAsync(Arg.Any<string>()).ThrowsAsync(new InvalidRefreshTokenException());

        var result = await CreateSut().Refresh(new RefreshRequest("invalid-token"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task SignOut_ValidToken_Returns204NoContent()
    {
        _authService.SignOutAsync(Arg.Any<string>()).Returns(Task.CompletedTask);

        var result = await CreateSut().SignOut(new SignOutRequest("any-token"));

        result.Should().BeOfType<NoContentResult>();
    }
}
