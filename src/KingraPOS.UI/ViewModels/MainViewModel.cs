using KingraPOS.Infrastructure.Persistence;
using KingraPOS.UI.Common;

namespace KingraPOS.UI.ViewModels;

public class MainViewModel : ObservableObject
{
    private string _statusMessage;

    public MainViewModel(DatabaseInitializationResult database)
    {
        DatabasePath = database.DatabasePath;
        SchemaVersion = database.SchemaVersion;
        TableCount = database.TableCount;
        _statusMessage = $"Database siap — skema v{database.SchemaVersion}, {database.TableCount} tabel.";
    }

    public string DatabasePath { get; }

    public int SchemaVersion { get; }

    public int TableCount { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }
}
