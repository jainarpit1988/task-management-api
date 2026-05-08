using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Interfaces.Repositories;

public interface IUserSessionRepository
{
    Task AddAsync(UserSession session, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

