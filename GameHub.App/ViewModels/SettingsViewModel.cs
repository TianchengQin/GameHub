using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameHub.App.Theming;
using GameHub.Core;
using GameHub.Core.Backup;
using GameHub.Core.Repositories;
using GameHub.Core.Settings;
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
    private string _steamGridDbApiKey;

    public SettingsViewModel(IGameRepository repository, ISettingsService settingsService)
    {
        _repository = repository;
        _settingsService = settingsService;

        _selectedTheme = settingsService.Current.Theme;
        _minimizeToTrayOnLaunch = settingsService.Current.MinimizeToTrayOnLaunch;
        _steamGridDbApiKey = settingsService.Current.SteamGridDbApiKey ?? string.Empty;
        foreach (var pattern in settingsService.Current.BlacklistPatterns)
            BlacklistPatterns.Add(pattern);
    }

    partial void OnMinimizeToTrayOnLaunchChanged(bool value)
    {
        _settingsService.Current.MinimizeToTrayOnLaunch = value;
        _settingsService.Save();
    }

    partial void OnSteamGridDbApiKeyChanged(string value)
    {
        _settingsService.Current.SteamGridDbApiKey = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        _settingsService.Save();
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
