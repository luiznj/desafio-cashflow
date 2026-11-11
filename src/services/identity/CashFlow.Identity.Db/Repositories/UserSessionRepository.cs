using System.Security.Cryptography;
using CashFlow.Identity.Db.Data;
using CashFlow.Identity.Db.Models;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Identity.Db.Repositories;

public class UserSessionRepository(IdentityDbContext db) : IUserSessionRepository
{
    public async Task<UserSession> CreateAsync(Guid userId, int expiresInDays)
    {
        var tokenBytes = new byte[32];
        RandomNumberGenerator.Fill(tokenBytes);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RefreshToken = token,
            ExpiresAt = DateTime.UtcNow.AddDays(expiresInDays),
            CreatedAt = DateTime.UtcNow
        };

        db.UserSessions.Add(session);
        await db.SaveChangesAsync();

        return session;
    }

    public Task<UserSession?> FindByTokenAsync(string token) =>
        db.UserSessions
            .FirstOrDefaultAsync(s =>
                s.RefreshToken == token &&
                s.RevokedAt == null &&
                s.ExpiresAt > DateTime.UtcNow);

    public async Task RevokeAsync(Guid sessionId)
    {
        var session = await db.UserSessions.FindAsync(sessionId);
        if (session is null) return;

        session.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
