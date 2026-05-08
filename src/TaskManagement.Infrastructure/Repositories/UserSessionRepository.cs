using TaskManagement.Application.Interfaces.Repositories;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Repositories;

public sealed class UserSessionRepository : IUserSessionRepository
{
    private readonly AppDbContext _db;
    public UserSessionRepository(AppDbContext db) => _db = db;

    public Task AddAsync(UserSession session, CancellationToken ct) => _db.UserSessions.AddAsync(session, ct).AsTask();
    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}

