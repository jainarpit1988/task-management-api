using FluentValidation;
using TaskManagement.Application.DTOs.Agent;

namespace TaskManagement.Application.Validation.Agent;

public sealed class AgentReassignTasksRequestValidator : AbstractValidator<AgentReassignTasksRequestDto>
{
    public AgentReassignTasksRequestValidator()
    {
        RuleFor(x => x.ToAgentId).GreaterThan(0);
        RuleFor(x => x.TaskIds).NotEmpty();
        RuleForEach(x => x.TaskIds).GreaterThan(0);
    }
}
