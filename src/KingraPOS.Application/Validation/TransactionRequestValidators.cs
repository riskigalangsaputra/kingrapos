using FluentValidation;
using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Validation;

public sealed class CartLineRequestValidator : AbstractValidator<CartLineRequest>
{
    public CartLineRequestValidator()
    {
        RuleFor(line => line.ProductId)
            .NotEmpty().WithMessage("Produk wajib dipilih.");

        RuleFor(line => line.Quantity)
            .GreaterThan(0).WithMessage("Jumlah harus lebih dari nol.");

        RuleFor(line => line.DiscountAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Diskon tidak boleh negatif.");

        RuleFor(line => line.SellingPriceOverride)
            .GreaterThanOrEqualTo(0).WithMessage("Harga jual tidak boleh negatif.")
            .When(line => line.SellingPriceOverride.HasValue);

        RuleFor(line => line.ServiceFee)
            .GreaterThanOrEqualTo(0).WithMessage("Biaya jasa tidak boleh negatif.")
            .When(line => line.ServiceFee.HasValue);
    }
}

public sealed class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(request => request.Lines)
            .NotEmpty().WithMessage("Keranjang masih kosong.");

        RuleForEach(request => request.Lines)
            .SetValidator(new CartLineRequestValidator());

        RuleFor(request => request.PaymentMethod)
            .NotEmpty().WithMessage("Metode pembayaran wajib dipilih.");

        RuleFor(request => request.AmountPaid)
            .GreaterThanOrEqualTo(0).WithMessage("Uang bayar tidak boleh negatif.");

        RuleFor(request => request.TransactionDiscountAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Diskon transaksi tidak boleh negatif.");

        RuleFor(request => request.PaymentProvider)
            .MaximumLength(100);

        RuleFor(request => request.PaymentReference)
            .MaximumLength(100);

        RuleFor(request => request.Notes)
            .MaximumLength(500);
    }
}

public sealed class VoidTransactionRequestValidator : AbstractValidator<VoidTransactionRequest>
{
    public VoidTransactionRequestValidator()
    {
        RuleFor(request => request.TransactionId)
            .NotEmpty().WithMessage("Transaksi wajib dipilih.");

        RuleFor(request => request.Reason)
            .NotEmpty().WithMessage("Alasan pembatalan wajib diisi.")
            .MaximumLength(500);
    }
}
