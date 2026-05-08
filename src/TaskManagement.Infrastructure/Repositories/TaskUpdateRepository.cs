using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Interfaces.Repositories;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Repositories;

public sealed class TaskUpdateRepository : ITaskUpdateRepository
{
    private readonly AppDbContext _db;
    public TaskUpdateRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(TaskUpdate update, CancellationToken ct)
    {
        await _db.TaskUpdates.AddAsync(update, ct);
    }

    public async Task<IReadOnlyList<TaskUpdate>> ListByTaskIdAsync(long taskId, CancellationToken ct)
    {
        return await _db.TaskUpdates.AsNoTracking()
            .Where(x => x.TaskId == taskId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }
}

