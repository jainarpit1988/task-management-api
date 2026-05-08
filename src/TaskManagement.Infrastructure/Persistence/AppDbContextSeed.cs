using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Helpers;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Infrastructure.Persistence;

public static class AppDbContextSeed
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct)
    {
        //await db.Database.MigrateAsync(ct);

        //if (!await db.Users.AnyAsync(ct))
        //{
        //    var admin = new User
        //    {
        //        Name = "Admin",
        //        Email = "admin@test.com",
        //        Mobile = "9999999999",
        //        Role = UserRole.ADMIN,
        //        Status = UserStatus.ACTIVE,
        //        PasswordHash = PasswordHasher.Hash("Admin@123")
        //    };

        //    var agent = new User
        //    {
        //        Name = "Agent One",
        //        Email = "agent@test.com",
        //        Mobile = "8888888888",
        //        Role = UserRole.AGENT,
        //        Status = UserStatus.ACTIVE,
        //        PasswordHash = PasswordHasher.Hash("Agent@123")
        //    };

        //    db.Users.AddRange(admin, agent);
        //    await db.SaveChangesAsync(ct);
        //}
    }
}

