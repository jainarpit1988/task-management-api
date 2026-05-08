using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Interfaces.Repositories;

public interface ITaskAssignmentRepository
{
    Task AddRangeAsync(IEnumerable<TaskAssignment> assignments, CancellationToken ct);
    Task<IReadOnlyList<TaskAssignment>> ListByTaskIdAsync(long taskId, CancellationToken ct);
}

