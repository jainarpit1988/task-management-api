using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Interfaces.Repositories;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Repositories;

public sealed class TaskAssignmentRepository : ITaskAssignmentRepository
{
    private readonly AppDbContext _db;
    public TaskAssignmentRepository(AppDbContext db) => _db = db;

    public async Task AddRangeAsync(IEnumerable<TaskAssignment> assignments, CancellationToken ct)
    {
        await _db.TaskAssignments.AddRangeAsync(assignments, ct);
    }

    public async Task<IReadOnlyList<TaskAssignment>> ListByTaskIdAsync(long taskId, CancellationToken ct)
    {
        return await _db.TaskAssignments.AsNoTracking()
            .Where(x => x.TaskId == taskId)
            .OrderByDescending(x => x.AssignedAt)
            .ToListAsync(ct);
    }
}

