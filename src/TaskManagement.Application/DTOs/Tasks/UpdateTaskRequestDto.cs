using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs.Tasks;

public sealed class UpdateTaskRequestDto
{
    public TaskStatus? Status { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? PdDate { get; set; }
}

