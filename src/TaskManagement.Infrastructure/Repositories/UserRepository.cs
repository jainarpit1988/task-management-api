using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Interfaces.Repositories;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(long id, CancellationToken ct) =>
        _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);

    public Task<User?> FindByEmailOrMobileAsync(string emailOrMobile, CancellationToken ct)
    {
        emailOrMobile = emailOrMobile.Trim();
        return _db.Users.FirstOrDefaultAsync(
            x => !x.IsDeleted
                 && (x.Email == emailOrMobile || x.Mobile == emailOrMobile),
            ct);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct) =>
        _db.Users.AnyAsync(x => !x.IsDeleted && x.Email == email, ct);

    public Task<bool> MobileExistsAsync(string mobile, CancellationToken ct) =>
        _db.Users.AnyAsync(x => !x.IsDeleted && x.Mobile == mobile, ct);

    public async Task AddAsync(User user, CancellationToken ct)
    {
        await _db.Users.AddAsync(user, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<User> Items, long TotalCount)> ListAgentsAsync(
        string? search,
        UserStatus? status,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        var q = _db.Users.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Role == UserRole.AGENT);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(x =>
                x.Name.Contains(search) ||
                (x.Email != null && x.Email.Contains(search)) ||
                (x.Mobile != null && x.Mobile.Contains(search)));
        }

        if (status.HasValue)
            q = q.Where(x => x.Status == status.Value);

        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}

