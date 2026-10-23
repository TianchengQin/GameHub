using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameHub.Core.Launching;
using GameHub.Core.Models;
using GameHub.Core.Repositories;

namespace GameHub.App.ViewModels;

public partial class GameCardViewModel : ObservableObject
{
    private readonly Game _game;
    private readonly GameLauncher _launcher;
    private readonly IGameRepository _repository;

    /// <summary>The underlying entity, exposed so the detail window can edit it directly.</summary>
    public Game GameModel => _game;

    public Guid Id => _game.Id;
    public string Name => _game.Name;
    public string ExecutablePath => _game.ExecutablePath;
    public string? CoverImagePath => _game.CoverImagePath ?? _game.IconCachePath;
    public List<Guid> GroupIds { get; }

    /// <summary>True when the user supplied real cover art (shown full-bleed); otherwise only the exe icon is available.</summary>
    public bool HasCustomCover => !string.IsNullOrWhiteSpace(_game.CoverImagePath) && File.Exists(_game.CoverImagePath);
    public bool IsIconOnly => !HasCustomCover;
    public string? IconImagePath => _game.IconCachePath;

    public event EventHandler? RemoveRequested;
    public event EventHandler? DetailsRequested;
    public event EventHandler? Launched;
    public event EventHandler<string>? LaunchFailed;

    [ObservableProperty]
    private DateTime? _lastPlayedAt;

    [ObservableProperty]
    private int _playCount;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isPathMissing;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    [NotifyPropertyChangedFor(nameof(LaunchLabel))]
    private bool _isRunning;

    public string LaunchLabel => IsRunning ? "运行中…" : "启动";

    public GameCardViewModel(Game game, GameLauncher launcher, IGameRepository repository)
    {
        _game = game;
        _launcher = launcher;
        _repository = repository;

        GroupIds = game.GroupMaps.Select(m => m.GroupId).ToList();
        _lastPlayedAt = game.LastPlayedAt;
        _playCount = game.PlayCount;
        _isFavorite = game.IsFavorite;
        _isPathMissing = !File.Exists(game.ExecutablePath);
        _isRunning = launcher.IsRunning(game.Id);
    }

    private bool CanLaunch => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task LaunchAsync()
    {
        var result = _launcher.Launch(_game.Id, _game.ExecutablePath, _game.Arguments, _game.WorkingDirectory);
        if (!result.Success)
        {
            IsPathMissing = !File.Exists(_game.ExecutablePath);
            LaunchFailed?.Invoke(this, $"无法启动「{Name}」：{result.ErrorMessage ?? "未知错误"}");
            return;
        }

        IsRunning = true;
        IsPathMissing = false;
        LastPlayedAt = result.LaunchedAt.ToLocalTime();
        PlayCount++;

        _game.LastPlayedAt = result.LaunchedAt;
        _game.PlayCount = PlayCount;
        Launched?.Invoke(this, EventArgs.Empty);
        await _repository.UpdateGameAsync(_game);
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        IsFavorite = !IsFavorite;
        _game.IsFavorite = IsFavorite;
        await _repository.UpdateGameAsync(_game);
    }

    /// <summary>Called on the UI thread by the owner after the launched process exits.</summary>
    public async void OnSessionEnded(long durationSeconds)
    {
        IsRunning = false;
        _game.TotalPlaytimeSeconds += durationSeconds;
        await _repository.UpdateGameAsync(_game);
    }

    /// <summary>Re-reads the display fields from the underlying model (after the detail window edits it).</summary>
    public void RefreshDisplayFields()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(CoverImagePath));
        OnPropertyChanged(nameof(HasCustomCover));
        OnPropertyChanged(nameof(IsIconOnly));
        OnPropertyChanged(nameof(IconImagePath));
        OnPropertyChanged(nameof(ExecutablePath));
        IsPathMissing = !File.Exists(_game.ExecutablePath);
    }

    [RelayCommand]
    private void OpenDetails() => DetailsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenContainingFolder()
    {
        try
        {
            Process.Start("explorer.exe", $"/select,\"{_game.ExecutablePath}\"");
        }
        catch
        {
            // Best-effort; nothing sensible to recover if Explorer can't be launched.
        }
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        var confirm = MessageBox.Show(
            $"确定要从库中移除「{Name}」吗？（不会删除游戏本体文件）",
            "移除游戏", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        await _repository.DeleteGameAsync(Id);
        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }
}
