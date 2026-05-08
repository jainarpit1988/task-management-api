using FluentValidation;
using TaskManagement.Application.DTOs.Agent;

namespace TaskManagement.Application.Validation.Agent;

public sealed class AddTaskUpdateRequestValidator : AbstractValidator<AddTaskUpdateRequestDto>
{
    public AddTaskUpdateRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Comment).MaximumLength(2000);
        RuleFor(x => x.MeetingPersonName).MaximumLength(255);
        RuleFor(x => x.MeetingPersonMobile).MaximumLength(20);
    }
}

