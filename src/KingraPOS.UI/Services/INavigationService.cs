namespace KingraPOS.UI.Services;

public interface INavigationService
{
    object? CurrentPage { get; }

    event EventHandler? CurrentPageChanged;

    void Navigate(Type pageType);
}
