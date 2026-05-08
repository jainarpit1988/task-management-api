using FluentValidation;
using TaskManagement.Application.DTOs.Admin;

namespace TaskManagement.Application.Validation.Admin;

public sealed class CreateAgentRequestValidator : AbstractValidator<CreateAgentRequestDto>
{
    public CreateAgentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).MaximumLength(100);

        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
        {
            RuleFor(x => x.Email!).EmailAddress().MaximumLength(150);
        });

        When(x => !string.IsNullOrWhiteSpace(x.Mobile), () =>
        {
            RuleFor(x => x.Mobile!).MaximumLength(20);
        });
    }
}

