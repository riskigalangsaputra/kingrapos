using FluentValidation;
using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Validation;

public sealed class CategoryRequestValidator : AbstractValidator<CategoryRequest>
{
    public CategoryRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Nama kategori wajib diisi.")
            .MaximumLength(150);
    }
}
