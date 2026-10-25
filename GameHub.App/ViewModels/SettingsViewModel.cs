using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameHub.App.Theming;
using GameHub.App.Updates;
using GameHub.Core;
using GameHub.Core.Backup;
using GameHub.Core.Covers;
using GameHub.Core.Repositories;
using GameHub.Core.Settings;
using GameHub.Core.Updates;
using Microsoft.Win32;

namespace GameHub.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IGameRepository _repository;
    private readonly ISettingsService _settingsService;

    public ObservableCollection<string> BlacklistPatterns { get; } = new();
    public string AppDataRoot => AppPaths.AppDataRoot;

    public static Array ThemeOptions => Enum.GetValues(typeof(AppTheme));

    [ObservableProperty]
    private AppTheme _selectedTheme;

    [ObservableProperty]
    private string _newPattern = string.Empty;

    [ObservableProperty]
    private bool _minimizeToTrayOnLaunch;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestApiKeyCommand))]
    private string _steamGridDbApiKey;

    [ObservableProperty]
    private bool _autoCheckUpdates;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
    [NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
    private bool _isUpdateBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
    private UpdateInfo? _availableUpdate;

    [ObservableProperty]
    private string _updateStatus = string.Empty;

    [ObservableProperty]
    private double _updateProgress;

    public string CurrentVersionText => _updateService.CurrentVersionText;

    private readonly UpdateService _updateService;

    public SettingsViewModel(IGameRepository repository, ISettingsService settingsService, UpdateService updateService)
    {
        _repository = repository;
        _settingsService = settingsService;
        _updateService = updateService;

        _selectedTheme = settingsService.Current.Theme;
        _minimizeToTrayOnLaunch = settingsService.Current.MinimizeToTrayOnLaunch;
        _autoCheckUpdates = settingsService.Current.AutoCheckUpdates;
        _steamGridDbApiKey = settingsService.Current.SteamGridDbApiKey ?? string.Empty;
        foreach (var pattern in settingsService.Current.BlacklistPatterns)
            BlacklistPatterns.Add(pattern);

        _availableUpdate = updateService.Available;
        if (_availableUpdate is not null)
            _updateStatus = $"发现新版本 {_availableUpdate.Version.ToString(3)}";
    }

    partial void OnAutoCheckUpdatesChanged(bool value)
    {
        _settingsService.Current.AutoCheckUpdates = value;
        _settingsService.Save();
    }

    [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
    private async Task CheckForUpdatesAsync()
    {
        IsUpdateBusy = true;
        UpdateStatus = "正在检查更新…";
        try
        {
            AvailableUpdate = await _updateService.CheckAsync();
            UpdateStatus = AvailableUpdate is null
                ? "已是最新版本。"
                : $"发现新版本 {AvailableUpdate.Version.ToString(3)}，可以下载并安装。";
        }
        catch (Exception ex)
        {
            UpdateStatus = $"检查更新失败：{ex.Message}";
        }
        finally
        {
            IsUpdateBusy = false;
        }
    }

    private bool CanCheckForUpdates() => !IsUpdateBusy;

    [RelayCommand(CanExecute = nameof(CanInstallUpdate))]
    private async Task InstallUpdateAsync()
    {
        if (AvailableUpdate is null) return;

        IsUpdateBusy = true;
        UpdateProgress = 0;
        UpdateStatus = "正在下载更新…";
        try
        {
            var progress = new Progress<double>(p => UpdateProgress = p * 100);
            await _updateService.LaunchInstallerAsync(AvailableUpdate, progress);
            UpdateStatus = "安装程序已启动，GameHub 即将退出并自动重启。";
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateStatus = $"更新失败：{ex.Message}";
            IsUpdateBusy = false;
        }
    }

    private bool CanInstallUpdate() => AvailableUpdate is not null && !IsUpdateBusy;

    [RelayCommand]
    private void OpenReleasePage()
    {
        var url = AvailableUpdate?.PageUrl;
        if (string.IsNullOrEmpty(url)) url = "https://github.com/TianchengQin/GameHub/releases";

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Best-effort; no browser available.
        }
    }

    partial void OnMinimizeToTrayOnLaunchChanged(bool value)
    {
        _settingsService.Current.MinimizeToTrayOnLaunch = value;
        _settingsService.Save();
    }

    partial void OnSteamGridDbApiKeyChanged(string value)
    {
        _settingsService.Current.SteamGridDbApiKey = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        ApiKeyStatus = _settingsService.Save()
            ? (string.IsNullOrWhiteSpace(value) ? "未设置" : "已自动保存")
            : "保存失败：无法写入设置文件，请检查数据文件夹的权限。";
    }

    [ObservableProperty]
    private string _apiKeyStatus = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestApiKeyCommand))]
    private bool _isTestingApiKey;

    private bool CanTestApiKey() => !IsTestingApiKey && !string.IsNullOrWhiteSpace(SteamGridDbApiKey);

    [RelayCommand(CanExecute = nameof(CanTestApiKey))]
    private async Task TestApiKeyAsync()
    {
        IsTestingApiKey = true;
        ApiKeyStatus = "正在验证…";
        try
        {
            using var client = new SteamGridDbClient(SteamGridDbApiKey.Trim());
            var games = await client.SearchGamesAsync("Hades");
            ApiKeyStatus = games.Count > 0 ? "✓ API Key 有效，已保存。" : "Key 可用，但测试搜索没有返回结果。";
        }
        catch (Exception ex)
        {
            ApiKeyStatus = $"验证失败：{ex.Message}";
        }
        finally
        {
            IsTestingApiKey = false;
        }
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        _settingsService.Current.Theme = value;
        _settingsService.Save();
        ThemeApplier.Apply(value);
    }

    [RelayCommand]
    private void AddPattern()
    {
        if (string.IsNullOrWhiteSpace(NewPattern)) return;

        BlacklistPatterns.Add(NewPattern.Trim());
        NewPattern = string.Empty;
        PersistPatterns();
    }

    [RelayCommand]
    private void RemovePattern(string? pattern)
    {
        if (pattern is null) return;

        BlacklistPatterns.Remove(pattern);
        PersistPatterns();
    }

    private void PersistPatterns()
    {
        _settingsService.Current.BlacklistPatterns = BlacklistPatterns.ToList();
        _settingsService.Save();
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            Process.Start("explorer.exe", AppDataRoot);
        }
        catch
        {
            // Best-effort; nothing sensible to recover if Explorer can't be launched.
        }
    }

    [RelayCommand]
    private async Task ExportLibraryAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出游戏库备份",
            Filter = "JSON 文件 (*.json)|*.json",
            FileName = $"GameHub-backup-{DateTime.Now:yyyyMMdd}.json"
        };
        if (dialog.ShowDialog() != true) return;

        var games = await _repository.GetAllGamesAsync();
        var groups = await _repository.GetAllGroupsAsync();
        var export = LibraryExportMapper.ToExport(games, groups);

        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(dialog.FileName, json);

        MessageBox.Show("导出完成。", "备份", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task ImportLibraryAsync()
    {
        var dialog = new OpenFileDialog { Title = "选择备份文件", Filter = "JSON 文件 (*.json)|*.json" };
        if (dialog.ShowDialog() != true) return;

        var confirm = MessageBox.Show(
            "导入会清空并替换当前库中的全部游戏和分组数据，确定要继续吗？",
            "导入备份", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var json = await File.ReadAllTextAsync(dialog.FileName);
        var export = JsonSerializer.Deserialize<LibraryExport>(json);
        if (export is null) return;

        var (games, groups, links) = LibraryExportMapper.FromExport(export);
        await _repository.ReplaceAllAsync(games, groups, links);

        MessageBox.Show("导入完成，重新打开 GameHub 即可看到最新数据。", "备份", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
