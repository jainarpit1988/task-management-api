using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Interfaces.Repositories;

public interface ITaskUpdateRepository
{
    Task AddAsync(TaskUpdate update, CancellationToken ct);
    Task<IReadOnlyList<TaskUpdate>> ListByTaskIdAsync(long taskId, CancellationToken ct);
}

