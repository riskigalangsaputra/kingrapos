using FluentValidation;
using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Validation;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Nama wajib diisi.")
            .MaximumLength(150);

        RuleFor(request => request.Username)
            .NotEmpty().WithMessage("Username wajib diisi.")
            .MinimumLength(3).WithMessage("Username minimal 3 karakter.")
            .MaximumLength(50)
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("Username hanya boleh berisi huruf, angka, titik, garis bawah, dan strip.");

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Password wajib diisi.")
            .MinimumLength(8).WithMessage("Password minimal 8 karakter.")
            .MaximumLength(128);

        RuleFor(request => request.RoleId)
            .NotEmpty().WithMessage("Role wajib dipilih.");

        RuleFor(request => request.EmployeeCode)
            .MaximumLength(50);
    }
}
