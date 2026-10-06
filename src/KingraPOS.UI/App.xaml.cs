using System.Windows;
using KingraPOS.Infrastructure.Persistence;
using KingraPOS.UI.ViewModels;
using KingraPOS.UI.Views;
using Wpf.Ui.Appearance;

namespace KingraPOS.UI;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        var databasePath = KingraPosDatabase.GetDatabasePath();
        var database = InitializeDatabase(databasePath);

        var mainWindow = new MainWindow
        {
            DataContext = new MainViewModel(database)
        };

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private static DatabaseInitializationResult InitializeDatabase(string databasePath)
    {
        using var context = KingraPosDatabase.CreateContext(databasePath);
        var initializer = new DatabaseInitializer(context, databasePath);

        return initializer.Ensure();
    }
}
