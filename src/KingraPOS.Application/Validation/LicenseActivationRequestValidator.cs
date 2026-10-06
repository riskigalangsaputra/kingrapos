using FluentValidation;
using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Validation;

public sealed class LicenseActivationRequestValidator : AbstractValidator<LicenseActivationRequest>
{
    public LicenseActivationRequestValidator()
    {
        RuleFor(request => request.LicenseKey)
            .NotEmpty().WithMessage("Kunci lisensi wajib diisi.")
            .MaximumLength(200);

        RuleFor(request => request.LicensedTo)
            .MaximumLength(200);
    }
}
