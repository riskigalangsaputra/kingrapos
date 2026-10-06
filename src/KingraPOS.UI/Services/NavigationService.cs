using Microsoft.Extensions.DependencyInjection;

namespace KingraPOS.UI.Services;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public object? CurrentPage { get; private set; }

    public event EventHandler? CurrentPageChanged;

    public void Navigate(Type pageType)
    {
        ArgumentNullException.ThrowIfNull(pageType);

        if (CurrentPage?.GetType() == pageType)
            return;

        CurrentPage = _serviceProvider.GetRequiredService(pageType);
        CurrentPageChanged?.Invoke(this, EventArgs.Empty);
    }
}
