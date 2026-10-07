using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Receipts;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace KingraPOS.UI.Services;

public sealed class ReceiptPrinterService : IReceiptPrinterService
{
    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly ISetupService _setupService;

    public ReceiptPrinterService(IKingraPosDbContextFactory contextFactory, ISetupService setupService)
    {
        _contextFactory = contextFactory;
        _setupService = setupService;
    }

    public async Task<string> FormatAsync(
        TransactionDto transaction,
        CancellationToken cancellationToken = default)
    {
        var (profile, paperWidth) = await LoadContextAsync(cancellationToken);

        return ReceiptFormatter.Format(transaction, profile, paperWidth);
    }

    public async Task PrintAsync(TransactionDto transaction, CancellationToken cancellationToken = default)
    {
        var (profile, paperWidth) = await LoadContextAsync(cancellationToken);
        var printerName = await LoadPrinterNameAsync(cancellationToken);

        var text = ReceiptFormatter.Format(transaction, profile, paperWidth);

        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 9,
            PagePadding = new Thickness(0),
            ColumnWidth = double.PositiveInfinity
        };

        document.Blocks.Add(new Paragraph(new Run(text)) { Margin = new Thickness(0) });

        var dialog = new PrintDialog();

        if (!string.IsNullOrWhiteSpace(printerName))
        {
            using var server = new LocalPrintServer();
            dialog.PrintQueue = new PrintQueue(server, printerName);
        }

        dialog.PrintDocument(
            ((IDocumentPaginatorSource)document).DocumentPaginator,
            $"Struk {transaction.InvoiceNumber}");
    }

    public async Task<bool> TryAutoPrintAsync(
        TransactionDto transaction,
        CancellationToken cancellationToken = default)
    {
        var state = await _setupService.GetStateAsync(cancellationToken);

        if (!state.Settings.AutoPrintReceipt)
            return false;

        try
        {
            await PrintAsync(transaction, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            // Kegagalan cetak tidak boleh membatalkan transaksi yang sudah tersimpan.
            Log.Warning(exception, "Cetak struk otomatis gagal untuk {Invoice}.", transaction.InvoiceNumber);
            await SaveFallbackAsync(transaction, cancellationToken);
            return false;
        }
    }

    private async Task SaveFallbackAsync(TransactionDto transaction, CancellationToken cancellationToken)
    {
        try
        {
            var text = await FormatAsync(transaction, cancellationToken);

            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KingraPOS",
                "receipts");

            Directory.CreateDirectory(folder);

            await File.WriteAllTextAsync(
                Path.Combine(folder, $"{transaction.InvoiceNumber}.txt"),
                text,
                cancellationToken);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Gagal menyimpan salinan struk {Invoice}.", transaction.InvoiceNumber);
        }
    }

    private async Task<(BusinessProfileDto? Profile, int PaperWidth)> LoadContextAsync(
        CancellationToken cancellationToken)
    {
        var state = await _setupService.GetStateAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var paperWidth = await context.PrinterSettings
            .Where(printer => printer.DeletedAt == null && printer.IsDefault)
            .Select(printer => (int?)printer.PaperWidthMm)
            .FirstOrDefaultAsync(cancellationToken) ?? 80;

        return (state.BusinessProfile, paperWidth);
    }

    private async Task<string?> LoadPrinterNameAsync(CancellationToken cancellationToken)
    {
        using var context = _contextFactory.Create();

        return await context.PrinterSettings
            .Where(printer => printer.DeletedAt == null && printer.IsDefault)
            .Select(printer => printer.Address)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
