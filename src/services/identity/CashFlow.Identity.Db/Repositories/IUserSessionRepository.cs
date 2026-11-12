using CashFlow.Identity.Db.Models;

namespace CashFlow.Identity.Db.Repositories;

public interface IUserSessionRepository
{
    Task<(UserSession Session, string PlainToken)> CreateAsync(Guid userId, int expiresInDays);
    Task<UserSession?> FindByTokenAsync(string token);
    Task RevokeAsync(Guid sessionId);
}
