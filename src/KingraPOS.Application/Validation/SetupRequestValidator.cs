using FluentValidation;
using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Validation;

public sealed class SetupRequestValidator : AbstractValidator<SetupRequest>
{
    public SetupRequestValidator()
    {
        RuleFor(request => request.BusinessName)
            .NotEmpty().WithMessage("Nama usaha wajib diisi.")
            .MaximumLength(150);

        RuleFor(request => request.OwnerName)
            .NotEmpty().WithMessage("Nama pemilik wajib diisi.")
            .MaximumLength(150);

        RuleFor(request => request.TaxName)
            .NotEmpty().WithMessage("Nama pajak wajib diisi.")
            .MaximumLength(50);

        RuleFor(request => request.TaxRate)
            .InclusiveBetween(0, 100).WithMessage("Tarif pajak harus antara 0 dan 100 persen.");

        RuleFor(request => request.CashRounding)
            .GreaterThanOrEqualTo(0).WithMessage("Pembulatan kas tidak boleh negatif.");

        RuleFor(request => request.InvoicePrefix)
            .NotEmpty().WithMessage("Prefix invoice wajib diisi.")
            .MaximumLength(20);

        RuleFor(request => request.Timezone)
            .NotEmpty().WithMessage("Zona waktu wajib diisi.")
            .MaximumLength(64);

        RuleFor(request => request.OwnerFullName)
            .NotEmpty().WithMessage("Nama lengkap pemilik wajib diisi.")
            .MaximumLength(150);

        RuleFor(request => request.OwnerUsername)
            .NotEmpty().WithMessage("Username wajib diisi.")
            .MinimumLength(3).WithMessage("Username minimal 3 karakter.")
            .MaximumLength(50)
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("Username hanya boleh berisi huruf, angka, titik, garis bawah, dan strip.");

        RuleFor(request => request.OwnerPassword)
            .NotEmpty().WithMessage("Password wajib diisi.")
            .MinimumLength(8).WithMessage("Password minimal 8 karakter.")
            .MaximumLength(128);
    }
}
