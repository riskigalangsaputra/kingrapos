using FluentValidation;
using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Validation;

public sealed class UnitRequestValidator : AbstractValidator<UnitRequest>
{
    public UnitRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Nama satuan wajib diisi.")
            .MaximumLength(50);

        RuleFor(request => request.Description)
            .MaximumLength(500);
    }
}
