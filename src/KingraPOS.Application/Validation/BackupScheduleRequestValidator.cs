using FluentValidation;
using KingraPOS.Application.Dtos;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Application.Validation;

public sealed class BackupScheduleRequestValidator : AbstractValidator<BackupScheduleRequest>
{
    public BackupScheduleRequestValidator()
    {
        RuleFor(request => request.RunTime)
            .NotEmpty().WithMessage("Jam backup wajib diisi.")
            .Matches(@"^([01]\d|2[0-3]):[0-5]\d$")
            .WithMessage("Jam backup harus berformat HH:MM (contoh 23:00).");

        RuleFor(request => request.DayOfWeek)
            .NotNull().WithMessage("Hari wajib dipilih untuk jadwal mingguan.")
            .InclusiveBetween(0, 6)
            .When(request => request.Frequency == BackupFrequency.WEEKLY);

        RuleFor(request => request.RetentionCount)
            .InclusiveBetween(1, 365).WithMessage("Jumlah retensi harus antara 1 dan 365.");

        RuleFor(request => request.DestinationPath)
            .NotEmpty().WithMessage("Folder tujuan backup wajib diisi.")
            .MaximumLength(500);
    }
}
