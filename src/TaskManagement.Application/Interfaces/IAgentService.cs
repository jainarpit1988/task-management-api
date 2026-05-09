using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs;
using TaskManagement.Application.DTOs.Admin;
using TaskManagement.Application.DTOs.Agent;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.DTOs.Tasks;

namespace TaskManagement.Application.Interfaces;

public interface IAgentService
{
    Task<PagedResult<UserDto>> ListAgentsAsync(AgentListFilterDto filter, PaginationQueryDto page, CancellationToken ct);

    Task<PagedResult<TaskListItemDto>> GetMyTasksAsync(AgentTaskFilterDto filter, PaginationQueryDto page, CancellationToken ct);
    Task ReassignTasksAsync(AgentReassignTasksRequestDto request, CancellationToken ct);
    Task AcknowledgeTaskAsync(long taskId, AcknowledgeTaskRequestDto request, CancellationToken ct);
    Task<TaskUpdateDto> AddUpdateAsync(long taskId, AddTaskUpdateRequestDto request, CancellationToken ct);
    Task<TaskDetailsDto> GetTaskHistoryAsync(long taskId, CancellationToken ct);
}

