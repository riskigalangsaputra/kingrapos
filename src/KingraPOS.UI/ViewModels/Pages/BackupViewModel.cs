using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Enums;
using Microsoft.Win32;

namespace KingraPOS.UI.ViewModels.Pages;

public partial class BackupViewModel : ObservableObject
{
    private readonly IBackupService _backupService;
    private readonly IRestoreService _restoreService;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserSession _session;
    private readonly IValidator<BackupScheduleRequest> _validator;

    [ObservableProperty]
    private string _lastBackupText = "-";

    [ObservableProperty]
    private string? _healthWarning;

    [ObservableProperty]
    private bool _isScheduleActive = true;

    [ObservableProperty]
    private BackupFrequency _frequency = BackupFrequency.DAILY;

    [ObservableProperty]
    private string _runTime = "23:00";

    [ObservableProperty]
    private DayOption? _selectedDay;

    [ObservableProperty]
    private int _retentionCount = 7;

    [ObservableProperty]
    private string _destinationPath = string.Empty;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _selectedBackupFile;

    public BackupViewModel(
        IBackupService backupService,
        IRestoreService restoreService,
        IPermissionService permissionService,
        ICurrentUserSession session,
        IValidator<BackupScheduleRequest> validator)
    {
        _backupService = backupService;
        _restoreService = restoreService;
        _permissionService = permissionService;
        _session = session;
        _validator = validator;
    }

    public ObservableCollection<BackupLogDto> History { get; } = new();

    public IReadOnlyList<BackupFrequency> Frequencies { get; } = new[]
    {
        BackupFrequency.HOURLY,
        BackupFrequency.DAILY,
        BackupFrequency.WEEKLY
    };

    public IReadOnlyList<DayOption> Days { get; } = DayOption.All;

    public bool CanBackup => _permissionService.HasPermission(PermissionCatalog.SettingsBackup);

    public bool CanRestore => _permissionService.HasPermission(PermissionCatalog.SettingsRestore);

    public bool ShowDayOfWeek => Frequency == BackupFrequency.WEEKLY;

    public event EventHandler? RestoreCompleted;

    public async Task InitializeAsync()
    {
        IsBusy = true;

        try
        {
            var schedule = await _backupService.GetScheduleAsync();

            if (schedule is null)
            {
                DestinationPath = await _backupService.GetDefaultDestinationAsync();
            }
            else
            {
                Frequency = schedule.Frequency;
                RunTime = schedule.RunTime;
                SelectedDay = Days.FirstOrDefault(day => day.Value == (schedule.DayOfWeek ?? 0)) ?? Days[0];
                RetentionCount = schedule.RetentionCount;
                DestinationPath = schedule.DestinationPath;
                IsScheduleActive = schedule.IsActive;
            }

            SelectedDay ??= Days[0];

            await ReloadHistoryAsync();
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BrowseDestinationAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pilih folder tujuan backup"
        };

        if (dialog.ShowDialog() == true)
            DestinationPath = dialog.FolderName;

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveScheduleAsync()
    {
        Message = null;

        var request = new BackupScheduleRequest(
            Frequency,
            RunTime,
            ShowDayOfWeek ? SelectedDay?.Value : null,
            RetentionCount,
            DestinationPath,
            IsScheduleActive);

        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            Message = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
            return;
        }

        IsBusy = true;

        try
        {
            await _backupService.SaveScheduleAsync(request);
            Message = "Jadwal backup tersimpan.";
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        Message = null;
        IsBusy = true;

        try
        {
            var result = await _backupService.RunBackupAsync(
                BackupTriggerType.MANUAL,
                _session.User?.UserId);

            Message = result.Status == JobStatus.SUCCESS
                ? $"Backup berhasil: {result.FileName} ({FormatSize(result.FileSizeBytes)})"
                : $"Backup gagal: {result.ErrorMessage}";

            await ReloadHistoryAsync();
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BrowseBackupFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Pilih file backup",
            Filter = "Database SQLite (*.db)|*.db|Semua file (*.*)|*.*",
            InitialDirectory = Directory.Exists(DestinationPath) ? DestinationPath : null
        };

        if (dialog.ShowDialog() == true)
            SelectedBackupFile = dialog.FileName;

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        Message = null;

        if (string.IsNullOrWhiteSpace(SelectedBackupFile))
        {
            Message = "Pilih file backup terlebih dahulu.";
            return;
        }

        var inspection = await _restoreService.InspectAsync(SelectedBackupFile);
        if (!inspection.IsValid)
        {
            Message = inspection.Message;
            return;
        }

        var confirmation = MessageBox.Show(
            $"Seluruh data saat ini akan digantikan oleh:\n{SelectedBackupFile}\n\n{inspection.Message}\n\nLanjutkan?",
            "Konfirmasi Restore",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
            return;

        IsBusy = true;

        try
        {
            var result = await _restoreService.RestoreAsync(SelectedBackupFile, _session.User?.UserId);

            Message = result.Message;

            if (result.Success)
            {
                MessageBox.Show(result.Message, "Restore", MessageBoxButton.OK, MessageBoxImage.Information);
                RestoreCompleted?.Invoke(this, EventArgs.Empty);
            }

            await ReloadHistoryAsync();
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadHistoryAsync()
    {
        var health = await _backupService.GetHealthAsync();

        LastBackupText = health.LastSuccessAt is null
            ? "Belum pernah ada backup yang berhasil."
            : health.LastSuccessAt.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm");

        HealthWarning = health.IsStale
            ? $"Perhatian: backup terakhir sudah melewati batas aman ({FormatThreshold(health.StaleThreshold)}). Segera jalankan backup."
            : health.LastError is not null
                ? $"Backup terakhir gagal: {health.LastError}"
                : null;

        History.Clear();

        foreach (var entry in await _backupService.GetHistoryAsync())
            History.Add(entry);
    }

    private static string FormatThreshold(TimeSpan? threshold) =>
        threshold is null ? "-" : $"{threshold.Value.TotalHours:0} jam";

    private static string FormatSize(long? bytes) => bytes switch
    {
        null => "-",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.#} MB"
    };

    partial void OnFrequencyChanged(BackupFrequency value)
    {
        OnPropertyChanged(nameof(ShowDayOfWeek));
    }
}

public sealed record DayOption(int Value, string Name)
{
    public static IReadOnlyList<DayOption> All { get; } = new[]
    {
        new DayOption(0, "Minggu"),
        new DayOption(1, "Senin"),
        new DayOption(2, "Selasa"),
        new DayOption(3, "Rabu"),
        new DayOption(4, "Kamis"),
        new DayOption(5, "Jumat"),
        new DayOption(6, "Sabtu")
    };
}
