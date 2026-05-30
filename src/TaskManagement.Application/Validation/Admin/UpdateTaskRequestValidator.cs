using FluentValidation;
using TaskManagement.Application.DTOs.Tasks;

namespace TaskManagement.Application.Validation.Admin;

public sealed class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequestDto>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);

        RuleFor(x => x.OtherText).MaximumLength(255).When(x => !string.IsNullOrWhiteSpace(x.OtherText));

        RuleFor(x => x)
            .Must(x => x.Status.HasValue || x.PdStatus.HasValue || x.TaskStatusLookupId.HasValue ||
                       x.PdDate.HasValue || x.DueDate.HasValue || x.OtherTextProvided)
            .WithMessage("Provide at least one field: status, pdStatus, taskStatusId, pdDate, dueDate, or other_text.");
    }
}
