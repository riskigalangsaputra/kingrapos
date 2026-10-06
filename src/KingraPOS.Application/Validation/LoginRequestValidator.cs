using FluentValidation;
using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Username)
            .NotEmpty().WithMessage("Username wajib diisi.");

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Password wajib diisi.");
    }
}
