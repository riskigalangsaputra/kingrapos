using CommunityToolkit.Mvvm.ComponentModel;
using KingraPOS.Application.Abstractions.Services;

namespace KingraPOS.UI.ViewModels.Pages;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ISetupService _setupService;
    private readonly ICurrentUserSession _session;

    [ObservableProperty]
    private string _businessName = "-";

    [ObservableProperty]
    private string _ownerName = "-";

    [ObservableProperty]
    private string _taxSummary = "-";

    [ObservableProperty]
    private string _serviceFeeSummary = "-";

    [ObservableProperty]
    private string _address = "-";

    public DashboardViewModel(ISetupService setupService, ICurrentUserSession session)
    {
        _setupService = setupService;
        _session = session;

        UserName = session.User?.Name ?? "-";
        RoleName = session.User?.RoleName ?? "-";
        Username = session.User?.Username ?? "-";
    }

    public string UserName { get; }

    public string RoleName { get; }

    public string Username { get; }

    public async Task InitializeAsync()
    {
        var state = await _setupService.GetStateAsync();

        BusinessName = state.BusinessProfile?.Name ?? "-";
        OwnerName = state.BusinessProfile?.OwnerName ?? "-";
        Address = string.IsNullOrWhiteSpace(state.BusinessProfile?.Address)
            ? "Belum diisi"
            : state.BusinessProfile!.Address!;

        TaxSummary = state.Settings.TaxEnabled
            ? $"{state.Settings.TaxName} {state.Settings.TaxRate:0.##}% ({(state.Settings.TaxInclusive ? "termasuk harga" : "ditambahkan")})"
            : "Nonaktif";

        ServiceFeeSummary = state.Settings.ServiceFeeEnabled ? "Aktif" : "Nonaktif";
    }
}
