namespace CashFlow.Identity.Api.Dtos;

public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);
