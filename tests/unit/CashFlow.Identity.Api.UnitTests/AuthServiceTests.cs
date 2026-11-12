using CashFlow.Identity.Api.Dtos;
using CashFlow.Identity.Api.Services;
using CashFlow.Identity.Db.Models;
using CashFlow.Identity.Db.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CashFlow.Identity.Api.UnitTests;

public class AuthServiceTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IUserSessionRepository _sessionRepo = Substitute.For<IUserSessionRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IConfiguration _config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:AccessTokenExpiresInMinutes"] = "15",
            ["Jwt:RefreshTokenExpiresInDays"] = "7"
        })
        .Build();

    private AuthService CreateSut() => new(_userRepo, _sessionRepo, _tokenService, _config, NullLogger<AuthService>.Instance);

    private UserSession MakeSession() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        RefreshToken = "refresh-token-opaque",
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task SignUpAsync_NewEmail_CreatesUserWithHashedPassword()
    {
        _userRepo.FindByEmailAsync(Arg.Any<string>()).Returns((User?)null);
        _sessionRepo.CreateAsync(Arg.Any<Guid>(), Arg.Any<int>()).Returns(MakeSession());
        _tokenService.GenerateAccessToken(Arg.Any<User>()).Returns("access-token");

        var request = new SignUpRequest("Test@Example.com", "Password1!");
        var sut = CreateSut();

        await sut.SignUpAsync(request);

        await _userRepo.Received(1).AddAsync(Arg.Is<User>(u =>
            u.PasswordHash != request.Password &&
            !string.IsNullOrEmpty(u.PasswordHash)));
    }

    [Fact]
    public async Task SignUpAsync_NewEmail_EmailStoredAsLowerInvariant()
    {
        _userRepo.FindByEmailAsync(Arg.Any<string>()).Returns((User?)null);
        _sessionRepo.CreateAsync(Arg.Any<Guid>(), Arg.Any<int>()).Returns(MakeSession());
        _tokenService.GenerateAccessToken(Arg.Any<User>()).Returns("access-token");

        var request = new SignUpRequest("Test@EXAMPLE.COM", "Password1!");
        var sut = CreateSut();

        await sut.SignUpAsync(request);

        await _userRepo.Received(1).AddAsync(Arg.Is<User>(u =>
            u.Email == "test@example.com"));
    }

    [Fact]
    public async Task SignUpAsync_NewEmail_ReturnsTokensWithFutureExpiresAt()
    {
        _userRepo.FindByEmailAsync(Arg.Any<string>()).Returns((User?)null);
        _sessionRepo.CreateAsync(Arg.Any<Guid>(), Arg.Any<int>()).Returns(MakeSession());
        _tokenService.GenerateAccessToken(Arg.Any<User>()).Returns("access-token");

        var sut = CreateSut();
        var result = await sut.SignUpAsync(new SignUpRequest("user@example.com", "Password1!"));

        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task SignUpAsync_DuplicateEmail_ThrowsDuplicateEmailException()
    {
        _userRepo.FindByEmailAsync(Arg.Any<string>()).Returns(new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com",
            PasswordHash = "hash"
        });

        var sut = CreateSut();

        await sut.Invoking(s => s.SignUpAsync(new SignUpRequest("existing@example.com", "Password1!")))
            .Should().ThrowAsync<DuplicateEmailException>();
    }

    [Fact]
    public async Task SignUpAsync_NewEmail_CallsGenerateAccessToken()
    {
        _userRepo.FindByEmailAsync(Arg.Any<string>()).Returns((User?)null);
        _sessionRepo.CreateAsync(Arg.Any<Guid>(), Arg.Any<int>()).Returns(MakeSession());
        _tokenService.GenerateAccessToken(Arg.Any<User>()).Returns("access-token");

        var sut = CreateSut();
        await sut.SignUpAsync(new SignUpRequest("user@example.com", "Password1!"));

        _tokenService.Received(1).GenerateAccessToken(Arg.Is<User>(u => u.Email == "user@example.com"));
    }

    [Fact]
    public async Task SignInAsync_ValidCredentials_ReturnsAuthResponse()
    {
        var password = "Password1!";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = hash };

        _userRepo.FindByEmailAsync("user@example.com").Returns(user);
        _sessionRepo.CreateAsync(Arg.Any<Guid>(), Arg.Any<int>()).Returns(MakeSession());
        _tokenService.GenerateAccessToken(Arg.Any<User>()).Returns("access-token");

        var sut = CreateSut();
        var result = await sut.SignInAsync(new SignInRequest("user@example.com", password));

        result.Should().NotBeNull();
        result.AccessToken.Should().Be("access-token");
    }

    [Fact]
    public async Task SignInAsync_UnknownEmail_ThrowsInvalidCredentialsException()
    {
        _userRepo.FindByEmailAsync(Arg.Any<string>()).Returns((User?)null);

        var sut = CreateSut();

        await sut.Invoking(s => s.SignInAsync(new SignInRequest("nobody@example.com", "Password1!")))
            .Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task SignInAsync_WrongPassword_ThrowsInvalidCredentialsException()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword1!")
        };
        _userRepo.FindByEmailAsync(Arg.Any<string>()).Returns(user);

        var sut = CreateSut();

        await sut.Invoking(s => s.SignInAsync(new SignInRequest("user@example.com", "WrongPassword1!")))
            .Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task SignInAsync_WrongPassword_SameErrorMessageAsUnknownEmail()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword1!")
        };
        _userRepo.FindByEmailAsync("user@example.com").Returns(user);
        _userRepo.FindByEmailAsync("nobody@example.com").Returns((User?)null);

        var sut = CreateSut();

        var wrongPasswordEx = await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => sut.SignInAsync(new SignInRequest("user@example.com", "WrongPassword1!")));

        var unknownEmailEx = await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => sut.SignInAsync(new SignInRequest("nobody@example.com", "Password1!")));

        wrongPasswordEx.Message.Should().Be(unknownEmailEx.Message);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_RevokesOldSessionAndIssuesNew()
    {
        var session = MakeSession();
        var user = new User { Id = session.UserId, Email = "user@example.com", PasswordHash = "hash" };

        _sessionRepo.FindByTokenAsync(session.RefreshToken).Returns(session);
        _userRepo.FindByIdAsync(session.UserId).Returns(user);
        _sessionRepo.CreateAsync(Arg.Any<Guid>(), Arg.Any<int>()).Returns(new UserSession
        {
            Id = Guid.NewGuid(),
            RefreshToken = "new-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        _tokenService.GenerateAccessToken(Arg.Any<User>()).Returns("new-access-token");

        var sut = CreateSut();
        var result = await sut.RefreshAsync(session.RefreshToken);

        await _sessionRepo.Received(1).RevokeAsync(session.Id);
        result.AccessToken.Should().Be("new-access-token");
        result.RefreshToken.Should().Be("new-refresh-token");
    }

    [Fact]
    public async Task RefreshAsync_InvalidToken_ThrowsInvalidRefreshTokenException()
    {
        _sessionRepo.FindByTokenAsync(Arg.Any<string>()).Returns((UserSession?)null);

        var sut = CreateSut();

        await sut.Invoking(s => s.RefreshAsync("invalid-token"))
            .Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    [Fact]
    public async Task SignOutAsync_ValidToken_RevokesSession()
    {
        var session = MakeSession();
        _sessionRepo.FindByTokenAsync(session.RefreshToken).Returns(session);

        var sut = CreateSut();
        await sut.SignOutAsync(session.RefreshToken);

        await _sessionRepo.Received(1).RevokeAsync(session.Id);
    }

    [Fact]
    public async Task SignOutAsync_UnknownToken_DoesNotThrow_AndDoesNotCallRevoke()
    {
        _sessionRepo.FindByTokenAsync(Arg.Any<string>()).Returns((UserSession?)null);

        var sut = CreateSut();

        await sut.Invoking(s => s.SignOutAsync("unknown-token"))
            .Should().NotThrowAsync();

        await _sessionRepo.DidNotReceive().RevokeAsync(Arg.Any<Guid>());
    }
}
