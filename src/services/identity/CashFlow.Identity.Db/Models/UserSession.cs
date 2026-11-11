namespace CashFlow.Identity.Db.Models;

public class UserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
