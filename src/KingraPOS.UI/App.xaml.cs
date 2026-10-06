using System.Windows;
using KingraPOS.Application.Abstractions;
using KingraPOS.Application.Services;
using KingraPOS.Infrastructure.Persistence;
using KingraPOS.Infrastructure.Repositories;
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

        var dataStore = new InMemoryDataStore();

        var categoryRepository = new InMemoryCategoryRepository(dataStore);
        var productRepository = new InMemoryProductRepository(dataStore);
        var saleRepository = new InMemorySaleRepository(dataStore);

        IProductService productService = new ProductService(productRepository, categoryRepository);
        ISaleService saleService = new SaleService(saleRepository, productRepository);

        var mainViewModel = new MainViewModel(productService, saleService);
        var mainWindow = new MainWindow
        {
            DataContext = mainViewModel
        };

        MainWindow = mainWindow;
        mainWindow.Show();

        _ = mainViewModel.InitializeAsync();
    }
}
