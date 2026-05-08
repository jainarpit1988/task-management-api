namespace TaskManagement.Application.DTOs.Reports;

public sealed class TasksPerAgentReportRowDto
{
    public long AgentId { get; set; }
    public string AgentName { get; set; } = null!;
    public long TotalTasks { get; set; }
    public long OpenTasks { get; set; }
    public long InProgressTasks { get; set; }
    public long ClosedTasks { get; set; }
}

