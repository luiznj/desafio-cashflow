using CashFlow.Identity.Db.Data;
using CashFlow.Identity.Db.Models;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Identity.Db.Repositories;

public class UserRepository(IdentityDbContext db) : IUserRepository
{
    public async Task AddAsync(User user)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    public Task<User?> FindByEmailAsync(string email) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email);

    public Task<User?> FindByIdAsync(Guid id) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id);
}
