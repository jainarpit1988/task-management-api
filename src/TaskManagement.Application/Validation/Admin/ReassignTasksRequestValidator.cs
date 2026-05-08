using FluentValidation;
using TaskManagement.Application.DTOs.Admin;

namespace TaskManagement.Application.Validation.Admin;

public sealed class ReassignTasksRequestValidator : AbstractValidator<ReassignTasksRequestDto>
{
    public ReassignTasksRequestValidator()
    {
        RuleFor(x => x.FromAgentId).GreaterThan(0);
        RuleFor(x => x.ToAgentId).GreaterThan(0).NotEqual(x => x.FromAgentId);
        RuleFor(x => x.TaskIds).NotEmpty();
        RuleForEach(x => x.TaskIds).GreaterThan(0);
    }
}

