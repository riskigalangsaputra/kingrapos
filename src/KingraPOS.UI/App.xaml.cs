using System.IO;
using System.Windows;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.DependencyInjection;
using KingraPOS.Infrastructure.DependencyInjection;
using KingraPOS.Infrastructure.Persistence;
using KingraPOS.UI.Services;
using KingraPOS.UI.ViewModels;
using KingraPOS.UI.ViewModels.Pages;
using KingraPOS.UI.Views;
using KingraPOS.UI.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Wpf.Ui.Appearance;

namespace KingraPOS.UI;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            _host = BuildHost();
            await _host.StartAsync();

            ApplicationThemeManager.Apply(ApplicationTheme.Dark);

            var database = _host.Services.GetRequiredService<DatabaseInitializer>().Ensure();

            Log.Information(
                "KingraPOS dimulai. Database: {DatabasePath} (skema v{SchemaVersion}, {TableCount} tabel, dibuat: {Created}).",
                database.DatabasePath,
                database.SchemaVersion,
                database.TableCount,
                database.Created);

            await RefreshProductBadgesAsync();

            await RunStartupFlowAsync();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Startup aplikasi gagal");
            MessageBox.Show(exception.Message, "KingraPOS", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
            _host = null;
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory
        });

        ConfigureLogging(builder);
        ConfigureServices(builder);

        return builder.Build();
    }

    private static void ConfigureLogging(HostApplicationBuilder builder)
    {
        var logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KingraPOS",
            "logs",
            "kingrapos-.log");

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .WriteTo.Debug()
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);
    }

    private static void ConfigureServices(HostApplicationBuilder builder)
    {
        var databasePath = KingraPosDatabase.GetDatabasePath();

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(databasePath);

        builder.Services.AddSingleton<INavigationService, NavigationService>();

        builder.Services.AddTransient<SetupViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<MainShellViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<UsersViewModel>();
        builder.Services.AddTransient<LicenseViewModel>();
        builder.Services.AddTransient<BackupViewModel>();
        builder.Services.AddTransient<PermissionsViewModel>();
        builder.Services.AddTransient<CategoriesViewModel>();
        builder.Services.AddTransient<UnitsViewModel>();
        builder.Services.AddTransient<SuppliersViewModel>();
        builder.Services.AddTransient<ProductsViewModel>();
        builder.Services.AddTransient<StockViewModel>();
        builder.Services.AddTransient<RejectsViewModel>();

        builder.Services.AddTransient<SetupWindow>();
        builder.Services.AddTransient<LoginWindow>();
        builder.Services.AddTransient<MainWindow>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<UsersPage>();
        builder.Services.AddTransient<LicensePage>();
        builder.Services.AddTransient<BackupPage>();
        builder.Services.AddTransient<PermissionsPage>();
        builder.Services.AddTransient<CategoriesPage>();
        builder.Services.AddTransient<UnitsPage>();
        builder.Services.AddTransient<SuppliersPage>();
        builder.Services.AddTransient<ProductsPage>();
        builder.Services.AddTransient<StockPage>();
        builder.Services.AddTransient<RejectsPage>();
    }

    private async Task RefreshProductBadgesAsync()
    {
        try
        {
            var cleared = await _host!.Services
                .GetRequiredService<IProductService>()
                .RefreshNewProductBadgesAsync();

            if (cleared > 0)
                Log.Information("Penanda produk baru dibersihkan untuk {Count} produk.", cleared);
        }
        catch (Exception exception)
        {
            // perawatan penanda tidak boleh menggagalkan startup
            Log.Warning(exception, "Gagal memperbarui penanda produk.");
        }
    }

    private async Task RunStartupFlowAsync()
    {
        var services = _host!.Services;

        var setupService = services.GetRequiredService<ISetupService>();
        if (!await setupService.IsCompletedAsync())
        {
            var setupWindow = services.GetRequiredService<SetupWindow>();
            if (setupWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        var loginWindow = services.GetRequiredService<LoginWindow>();
        if (loginWindow.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        var mainWindow = services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }
}
