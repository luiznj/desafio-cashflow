using CashFlow.Identity.Db.Models;

namespace CashFlow.Identity.Db.Repositories;

public interface IUserRepository
{
    Task AddAsync(User user);
    Task<User?> FindByEmailAsync(string email);
    Task<User?> FindByIdAsync(Guid id);
}
