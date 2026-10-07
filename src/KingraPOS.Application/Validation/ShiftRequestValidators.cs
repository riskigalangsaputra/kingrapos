using FluentValidation;
using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Validation;

public sealed class OpenShiftRequestValidator : AbstractValidator<OpenShiftRequest>
{
    public OpenShiftRequestValidator()
    {
        RuleFor(request => request.CashDrawerStart)
            .GreaterThanOrEqualTo(0).WithMessage("Modal awal tidak boleh negatif.");

        RuleFor(request => request.Notes)
            .MaximumLength(500);
    }
}

public sealed class CloseShiftRequestValidator : AbstractValidator<CloseShiftRequest>
{
    public CloseShiftRequestValidator()
    {
        RuleFor(request => request.ShiftId)
            .NotEmpty().WithMessage("Shift wajib dipilih.");

        RuleFor(request => request.CashDrawerEndPhysical)
            .GreaterThanOrEqualTo(0).WithMessage("Uang fisik tidak boleh negatif.");

        RuleFor(request => request.Notes)
            .MaximumLength(500);
    }
}

public sealed class SwitchShiftRequestValidator : AbstractValidator<SwitchShiftRequest>
{
    public SwitchShiftRequestValidator()
    {
        RuleFor(request => request.NewCashDrawerStart)
            .GreaterThanOrEqualTo(0).WithMessage("Modal awal shift baru tidak boleh negatif.");

        RuleFor(request => request.CashDrawerEndPhysical)
            .GreaterThanOrEqualTo(0).WithMessage("Uang fisik shift lama tidak boleh negatif.");

        RuleFor(request => request.Pin)
            .MaximumLength(32);

        RuleFor(request => request.Notes)
            .MaximumLength(500);
    }
}
