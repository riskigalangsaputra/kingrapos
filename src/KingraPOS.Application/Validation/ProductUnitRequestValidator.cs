using FluentValidation;
using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Validation;

public sealed class ProductUnitRequestValidator : AbstractValidator<ProductUnitRequest>
{
    public ProductUnitRequestValidator()
    {
        RuleFor(request => request.UnitId)
            .NotEmpty().WithMessage("Satuan wajib dipilih.");

        RuleFor(request => request.ConversionFactor)
            .GreaterThan(0).WithMessage("Faktor konversi harus lebih dari nol.");

        RuleFor(request => request.Barcode)
            .MaximumLength(100);
    }
}

public sealed class ProductPriceTierRequestValidator : AbstractValidator<ProductPriceTierRequest>
{
    public ProductPriceTierRequestValidator()
    {
        RuleFor(request => request.TierName)
            .NotEmpty().WithMessage("Nama tier wajib diisi.")
            .MaximumLength(100);

        RuleFor(request => request.MinimumQuantity)
            .GreaterThan(0).WithMessage("Kuantitas minimum harus lebih dari nol.");

        RuleFor(request => request.TierPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Harga tier tidak boleh negatif.");
    }
}

public sealed class ProductPriceTierListValidator : AbstractValidator<IReadOnlyCollection<ProductPriceTierRequest>>
{
    public ProductPriceTierListValidator()
    {
        RuleForEach(tiers => tiers).SetValidator(new ProductPriceTierRequestValidator());

        RuleFor(tiers => tiers)
            .Must(tiers => tiers.Select(tier => tier.MinimumQuantity).Distinct().Count() == tiers.Count)
            .WithMessage("Kuantitas minimum antar tier tidak boleh sama.");
    }
}
