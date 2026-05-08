using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(long id, CancellationToken ct);
    Task<User?> FindByEmailOrMobileAsync(string emailOrMobile, CancellationToken ct);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct);
    Task<bool> MobileExistsAsync(string mobile, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    Task<(IReadOnlyList<User> Items, long TotalCount)> ListAgentsAsync(
        string? search,
        UserStatus? status,
        int page,
        int pageSize,
        CancellationToken ct);
}

