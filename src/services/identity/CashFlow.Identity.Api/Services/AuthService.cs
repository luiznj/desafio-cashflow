using CashFlow.Identity.Api.Dtos;
using CashFlow.Identity.Db.Models;
using CashFlow.Identity.Db.Repositories;

namespace CashFlow.Identity.Api.Services;

public class AuthService(
    IUserRepository userRepository,
    IUserSessionRepository sessionRepository,
    ITokenService tokenService,
    IConfiguration configuration,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> SignUpAsync(SignUpRequest request)
    {
        var existing = await userRepository.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            logger.LogWarning("Sign-up recusado: e-mail já cadastrado {Email}", request.Email);
            throw new DuplicateEmailException();
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        await userRepository.AddAsync(user);
        logger.LogInformation("Novo usuário cadastrado {UserId}", user.Id);
        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponse> SignInAsync(SignInRequest request)
    {
        var user = await userRepository.FindByEmailAsync(request.Email.ToLowerInvariant());

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Tentativa de sign-in com credenciais inválidas para {Email}", request.Email);
            throw new InvalidCredentialsException();
        }

        logger.LogInformation("Sign-in realizado {UserId}", user.Id);
        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken)
    {
        var session = await sessionRepository.FindByTokenAsync(refreshToken)
            ?? throw new InvalidRefreshTokenException();

        var user = await userRepository.FindByIdAsync(session.UserId)
            ?? throw new InvalidRefreshTokenException();

        await sessionRepository.RevokeAsync(session.Id);

        logger.LogInformation("Refresh token rotacionado para {UserId}", user.Id);
        return await IssueTokensAsync(user);
    }

    public async Task SignOutAsync(string refreshToken)
    {
        var session = await sessionRepository.FindByTokenAsync(refreshToken);
        if (session is null) return;

        await sessionRepository.RevokeAsync(session.Id);
        logger.LogInformation("Sign-out: sessão {SessionId} revogada", session.Id);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user)
    {
        var accessToken = tokenService.GenerateAccessToken(user);
        var expiresInDays = configuration.GetValue<int>("Jwt:RefreshTokenExpiresInDays", 7);
        var expiresInMinutes = configuration.GetValue<int>("Jwt:AccessTokenExpiresInMinutes", 15);
        var session = await sessionRepository.CreateAsync(user.Id, expiresInDays);

        return new AuthResponse(accessToken, session.RefreshToken, DateTime.UtcNow.AddMinutes(expiresInMinutes));
    }
}
