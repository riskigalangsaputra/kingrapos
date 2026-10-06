using FluentValidation;
using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Validation;

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(request => request.Sku)
            .NotEmpty().WithMessage("SKU wajib diisi.")
            .MaximumLength(50);

        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Nama produk wajib diisi.")
            .MaximumLength(200);

        RuleFor(request => request.BaseUnitId)
            .NotEmpty().WithMessage("Satuan dasar wajib dipilih.");

        RuleFor(request => request.Description)
            .MaximumLength(1000);

        RuleFor(request => request.BaseCostPrice)
            .GreaterThanOrEqualTo(0).WithMessage("HPP tidak boleh negatif.")
            .When(request => request.BaseCostPrice.HasValue);

        RuleFor(request => request.DefaultServiceFee)
            .GreaterThanOrEqualTo(0).WithMessage("Biaya jasa tidak boleh negatif.")
            .When(request => request.DefaultServiceFee.HasValue);
    }
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(request => request.Sku)
            .NotEmpty().WithMessage("SKU wajib diisi.")
            .MaximumLength(50);

        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Nama produk wajib diisi.")
            .MaximumLength(200);

        RuleFor(request => request.Description)
            .MaximumLength(1000);

        RuleFor(request => request.BaseCostPrice)
            .GreaterThanOrEqualTo(0).WithMessage("HPP tidak boleh negatif.")
            .When(request => request.BaseCostPrice.HasValue);

        RuleFor(request => request.DefaultServiceFee)
            .GreaterThanOrEqualTo(0).WithMessage("Biaya jasa tidak boleh negatif.")
            .When(request => request.DefaultServiceFee.HasValue);
    }
}
