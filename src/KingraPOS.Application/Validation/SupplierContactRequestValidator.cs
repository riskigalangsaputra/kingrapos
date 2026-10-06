using FluentValidation;
using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Validation;

public sealed class SupplierContactRequestValidator : AbstractValidator<SupplierContactRequest>
{
    public SupplierContactRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Nama kontak wajib diisi.")
            .MaximumLength(150);

        RuleFor(request => request.PhoneNumber)
            .MaximumLength(30);

        RuleFor(request => request.Position)
            .MaximumLength(100);
    }
}
