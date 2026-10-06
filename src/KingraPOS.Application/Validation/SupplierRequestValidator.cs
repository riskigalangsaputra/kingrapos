using FluentValidation;
using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Validation;

public sealed class SupplierRequestValidator : AbstractValidator<SupplierRequest>
{
    public SupplierRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Nama supplier wajib diisi.")
            .MaximumLength(150);

        RuleFor(request => request.PhoneNumber)
            .NotEmpty().WithMessage("Nomor telepon wajib diisi.")
            .MaximumLength(30);

        RuleFor(request => request.Email)
            .EmailAddress().WithMessage("Format email tidak valid.")
            .MaximumLength(200)
            .When(request => !string.IsNullOrWhiteSpace(request.Email));

        RuleFor(request => request.Address)
            .MaximumLength(500);

        RuleFor(request => request.Notes)
            .MaximumLength(500);
    }
}
