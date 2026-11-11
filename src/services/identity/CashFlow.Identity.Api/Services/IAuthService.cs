using CashFlow.Identity.Api.Dtos;

namespace CashFlow.Identity.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> SignUpAsync(SignUpRequest request);
    Task<AuthResponse> SignInAsync(SignInRequest request);
    Task<AuthResponse> RefreshAsync(string refreshToken);
    Task SignOutAsync(string refreshToken);
}
