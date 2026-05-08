using FluentValidation;
using TaskManagement.Application.DTOs.Admin;

namespace TaskManagement.Application.Validation.Admin;

public sealed class AssignTasksRequestValidator : AbstractValidator<AssignTasksRequestDto>
{
    public AssignTasksRequestValidator()
    {
        RuleFor(x => x.AgentId).GreaterThan(0);
        RuleFor(x => x.TaskIds).NotEmpty();
        RuleForEach(x => x.TaskIds).GreaterThan(0);
    }
}

