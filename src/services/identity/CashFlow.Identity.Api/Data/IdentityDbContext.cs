using CashFlow.Identity.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Identity.Api.Data;

public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
    }
}
