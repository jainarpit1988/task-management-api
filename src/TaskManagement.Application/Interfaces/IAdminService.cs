using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs;
using TaskManagement.Application.DTOs.Admin;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.DTOs.Reports;
using TaskManagement.Application.DTOs.Tasks;

namespace TaskManagement.Application.Interfaces;

public interface IAdminService
{
    Task<UserDto> CreateAgentAsync(CreateAgentRequestDto request, CancellationToken ct);
    Task<PagedResult<UserDto>> ListAgentsAsync(AgentListFilterDto filter, PaginationQueryDto page, CancellationToken ct);

    // Creates an upload record + persists the file, then queues background processing.
    Task<long> EnqueueExcelUploadAsync(byte[] fileBytes, string fileName, CancellationToken ct);

    // Background worker calls this to parse Excel + create tasks.
    Task ProcessExcelUploadAsync(long uploadId, CancellationToken ct);

    Task AssignTasksAsync(AssignTasksRequestDto request, CancellationToken ct);
    Task ReassignTasksAsync(ReassignTasksRequestDto request, CancellationToken ct);

    Task<PagedResult<TaskListItemDto>> GetTasksAsync(TaskFilterDto filter, PaginationQueryDto page, CancellationToken ct);
    Task<TaskDetailsDto> GetTaskDetailsAsync(long taskId, CancellationToken ct);
    Task<TaskDetailsDto> GetTaskDetailsForCallerAsync(long taskId, CancellationToken ct);
    Task<TaskDetailsDto> UpdateTaskAsync(long taskId, UpdateTaskRequestDto request, CancellationToken ct);

    Task<(IReadOnlyList<TasksPerAgentReportRowDto> tasksPerAgent, StatusSummaryReportDto statusSummary)> GetReportsAsync(
        TaskFilterDto filter,
        CancellationToken ct);

    Task<(byte[] FileBytes, string FileName)> ExportReportsToExcelAsync(TaskFilterDto filter, CancellationToken ct);

    Task<ExcelUploadHistoryDto> GetExcelUploadHistoryAsync(int page, int pageSize, CancellationToken ct);
    Task<ExcelUploadDetailsDto> GetExcelUploadDetailsAsync(long uploadId, int recentErrors = 50, CancellationToken ct = default);
}

