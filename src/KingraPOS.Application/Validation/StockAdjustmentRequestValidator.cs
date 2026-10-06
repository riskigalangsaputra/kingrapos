using FluentValidation;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Stock;

namespace KingraPOS.Application.Validation;

public sealed class StockAdjustmentRequestValidator : AbstractValidator<StockAdjustmentRequest>
{
    public StockAdjustmentRequestValidator()
    {
        RuleFor(request => request.Items)
            .NotEmpty().WithMessage("Tambahkan minimal satu produk.");

        RuleForEach(request => request.Items)
            .SetValidator(new StockAdjustmentItemRequestValidator());

        RuleFor(request => request.ReferenceNumber)
            .MaximumLength(100);

        RuleFor(request => request.Notes)
            .MaximumLength(500);
    }
}

public sealed class StockAdjustmentItemRequestValidator : AbstractValidator<StockAdjustmentItemRequest>
{
    public StockAdjustmentItemRequestValidator()
    {
        RuleFor(item => item.ProductId)
            .NotEmpty().WithMessage("Produk wajib dipilih.");

        RuleFor(item => item.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("Jumlah tidak boleh negatif.");

        RuleFor(item => item.CostPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Harga beli tidak boleh negatif.")
            .When(item => item.CostPrice.HasValue);
    }
}
