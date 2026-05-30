using FluentValidation;
using TaskManagement.Application.DTOs.Tasks;

namespace TaskManagement.Application.Validation.Admin;

public sealed class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequestDto>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);

        RuleFor(x => x)
            .Must(x => x.Status.HasValue || x.DueDate.HasValue || x.PdDate.HasValue)
            .WithMessage("Provide at least one field: status, dueDate, or pdDate.");
    }
}

