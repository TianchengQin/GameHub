using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameHub.App.Views;
using GameHub.Core;
using GameHub.Core.Icons;
using GameHub.Core.Launching;
using GameHub.Core.Models;
using GameHub.Core.Repositories;
using GameHub.Core.Scanning;
using GameHub.Core.Settings;
using Microsoft.Win32;

namespace GameHub.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IGameRepository _repository;
    private readonly GameLauncher _launcher;
    private readonly GameScanner _scanner;
    private readonly ISettingsService _settingsService;

    public ObservableCollection<GameCardViewModel> Games { get; } = new();
    public ObservableCollection<NavItem> NavItems { get; } = new();
    public ICollectionView GamesView { get; }
    public ICollectionView FavoritesView { get; }

    public event EventHandler? MinimizeToTrayRequested;

    private FloatingFavoritesWindow? _floatingWindow;
    private DispatcherTimer? _toastTimer;

    [ObservableProperty]
    private string? _toastMessage;

    [ObservableProperty]
    private bool _isToastVisible;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasNoGames;

    public MainWindowViewModel(IGameRepository repository, GameLauncher launcher, GameScanner scanner, ISettingsService settingsService)
    {
        _repository = repository;
        _launcher = launcher;
        _scanner = scanner;
        _settingsService = settingsService;
        _launcher.SessionEnded += OnSessionEnded;

        GamesView = CollectionViewSource.GetDefaultView(Games);
        GamesView.Filter = FilterGame;
        GamesView.SortDescriptions.Add(new SortDescription(nameof(GameCardViewModel.IsFavorite), ListSortDirection.Descending));
        GamesView.SortDescriptions.Add(new SortDescription(nameof(GameCardViewModel.LastPlayedAt), ListSortDirection.Descending));
        EnableLiveShaping(GamesView, filter: true, sort: true);

        var favorites = new ListCollectionView(Games) { Filter = o => o is GameCardViewModel g && g.IsFavorite };
        favorites.SortDescriptions.Add(new SortDescription(nameof(GameCardViewModel.LastPlayedAt), ListSortDirection.Descending));
        EnableLiveShaping(favorites, filter: true, sort: false);
        FavoritesView = favorites;

        RebuildFixedNavItems();
    }

    private static void EnableLiveShaping(ICollectionView view, bool filter, bool sort)
    {
        if (view is not ICollectionViewLiveShaping live) return;

        if (filter && live.CanChangeLiveFiltering)
        {
            live.LiveFilteringProperties.Add(nameof(GameCardViewModel.IsFavorite));
            live.IsLiveFiltering = true;
        }

        if (sort && live.CanChangeLiveSorting)
        {
            live.LiveSortingProperties.Add(nameof(GameCardViewModel.IsFavorite));
            live.LiveSortingProperties.Add(nameof(GameCardViewModel.LastPlayedAt));
            live.IsLiveSorting = true;
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var games = await _repository.GetAllGamesAsync();
            Games.Clear();
            foreach (var game in games.OrderByDescending(g => g.LastPlayedAt ?? DateTime.MinValue))
                Games.Add(CreateCardViewModel(game));

            HasNoGames = Games.Count == 0;

            var groups = await _repository.GetAllGroupsAsync();
            RebuildFixedNavItems(groups);

            _ = UpgradeLowResIconsOnceAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool _iconUpgradeStarted;

    /// <summary>Re-extracts small cached icons from older versions at full resolution, once per app run.</summary>
    private async Task UpgradeLowResIconsOnceAsync()
    {
        if (_iconUpgradeStarted) return;
        _iconUpgradeStarted = true;

        // Icons written by an earlier build came out upside down; regenerate everything once, then drop a marker.
        var marker = Path.Combine(AppPaths.IconsFolder, ".format-v2");
        var force = !File.Exists(marker);

        var cards = Games.ToList();
        var upgraded = await Task.Run(() =>
        {
            var changed = new List<GameCardViewModel>();
            foreach (var card in cards)
            {
                var iconPath = Path.Combine(AppPaths.IconsFolder, card.Id + ".png");
                if (!IconExtractor.TryUpgradeIcon(card.ExecutablePath, iconPath, force)) continue;

                card.GameModel.IconCachePath = iconPath;
                changed.Add(card);
            }

            if (force)
                File.WriteAllText(marker, "2");

            return changed;
        });

        foreach (var card in upgraded)
        {
            await _repository.UpdateGameAsync(card.GameModel);
            card.RefreshDisplayFields();
        }

        if (upgraded.Count > 0)
            ShowToast($"已为 {upgraded.Count} 个游戏刷新了高清图标");
    }

    private void RebuildFixedNavItems(List<Group>? groups = null)
    {
        var previousKind = SelectedNavItem?.Kind ?? LibraryFilterKind.All;
        var previousGroupId = SelectedNavItem?.GroupId;

        NavItems.Clear();
        NavItems.Add(new NavItem { Icon = "", Label = "全部游戏", Kind = LibraryFilterKind.All });
        NavItems.Add(new NavItem { Icon = "", Label = "收藏", Kind = LibraryFilterKind.Favorites });
        NavItems.Add(new NavItem { Icon = "", Label = "最近玩过", Kind = LibraryFilterKind.RecentlyPlayed });

        foreach (var group in groups ?? new List<Group>())
        {
            NavItems.Add(new NavItem { Icon = "", Label = group.Name, Kind = LibraryFilterKind.Group, GroupId = group.Id });
        }

        SelectedNavItem = NavItems.FirstOrDefault(n => n.Kind == previousKind && n.GroupId == previousGroupId)
                          ?? NavItems[0];
    }

    [RelayCommand]
    private async Task AddGameAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择游戏可执行文件",
            Filter = "可执行文件 (*.exe)|*.exe"
        };

        if (dialog.ShowDialog() != true) return;

        await AddGameFromPathAsync(dialog.FileName);
    }

    /// <summary>Adds a single exe (e.g. dropped onto the window) as a manually-added game.</summary>
    public async Task AddGameFromPathAsync(string exePath)
    {
        if (!File.Exists(exePath) || !exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return;

        if (Games.Any(g => string.Equals(g.ExecutablePath, exePath, StringComparison.OrdinalIgnoreCase)))
            return;

        var game = new Game
        {
            Name = Path.GetFileNameWithoutExtension(exePath),
            ExecutablePath = exePath,
            Source = GameSource.Manual
        };

        TryAssignIcon(game);

        await _repository.AddGameAsync(game);
        Games.Insert(0, CreateCardViewModel(game));
        HasNoGames = false;
    }

    [RelayCommand]
    private async Task ScanFolderAsync()
    {
        var dialog = new OpenFolderDialog { Title = "选择游戏所在的根目录" };
        if (dialog.ShowDialog() != true) return;

        var scanOptions = new ScanOptions { BlacklistPatterns = _settingsService.Current.BlacklistPatterns };
        var candidates = _scanner.ScanFolder(dialog.FolderName, scanOptions)
            .Where(c => c.IncludeByDefault)
            .ToList();

        if (candidates.Count == 0)
        {
            MessageBox.Show("没有在该目录下找到可能的游戏可执行文件。", "扫描完成",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmVm = new ScanConfirmationViewModel(candidates);
        var window = new ScanConfirmationWindow(confirmVm) { Owner = Application.Current.MainWindow };

        if (window.ShowDialog() != true) return;

        var newGames = confirmVm.BuildSelectedGames();
        if (newGames.Count == 0) return;

        foreach (var game in newGames)
            TryAssignIcon(game);

        await _repository.AddGamesAsync(newGames);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ManageGroupsAsync()
    {
        var vm = new GroupManagerViewModel(_repository);
        await vm.LoadAsync();

        var window = new GroupManagerWindow(vm) { Owner = Application.Current.MainWindow };
        window.ShowDialog();

        await LoadAsync();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var vm = new SettingsViewModel(_repository, _settingsService);
        var window = new SettingsWindow(vm) { Owner = Application.Current.MainWindow };
        window.ShowDialog();
    }

    private static void TryAssignIcon(Game game)
    {
        var iconPath = Path.Combine(AppPaths.IconsFolder, game.Id + ".png");
        if (IconExtractor.TryExtractIcon(game.ExecutablePath, iconPath))
            game.IconCachePath = iconPath;
    }

    private GameCardViewModel CreateCardViewModel(Game game)
    {
        var vm = new GameCardViewModel(game, _launcher, _repository);
        vm.RemoveRequested += OnGameRemoveRequested;
        vm.DetailsRequested += OnGameDetailsRequested;
        vm.Launched += OnGameLaunched;
        vm.LaunchFailed += OnGameLaunchFailed;
        return vm;
    }

    private void OnGameRemoveRequested(object? sender, EventArgs e)
    {
        if (sender is not GameCardViewModel vm) return;

        vm.RemoveRequested -= OnGameRemoveRequested;
        vm.DetailsRequested -= OnGameDetailsRequested;
        vm.Launched -= OnGameLaunched;
        vm.LaunchFailed -= OnGameLaunchFailed;
        Games.Remove(vm);
        HasNoGames = Games.Count == 0;
    }

    private void OnGameLaunched(object? sender, EventArgs e)
    {
        if (_settingsService.Current.MinimizeToTrayOnLaunch)
            MinimizeToTrayRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnGameLaunchFailed(object? sender, string message) => ShowToast(message);

    public void ShowToast(string message)
    {
        ToastMessage = message;
        IsToastVisible = true;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += (_, _) =>
        {
            IsToastVisible = false;
            _toastTimer?.Stop();
        };
        _toastTimer.Start();
    }

    public async Task AssignGameToGroupAsync(Guid gameId, Guid groupId)
    {
        await _repository.AssignGameToGroupAsync(gameId, groupId);

        var groupName = NavItems.FirstOrDefault(n => n.GroupId == groupId)?.Label ?? "分组";
        var gameName = Games.FirstOrDefault(g => g.Id == gameId)?.Name ?? "游戏";
        await LoadAsync();
        ShowToast($"已把「{gameName}」加入「{groupName}」");
    }

    [RelayCommand]
    private async Task ScanStartMenuAsync()
    {
        var scanOptions = new ScanOptions { BlacklistPatterns = _settingsService.Current.BlacklistPatterns };
        var candidates = await Task.Run(() => _scanner.ScanStartMenu(scanOptions));

        var existing = Games.Select(g => g.ExecutablePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        candidates = candidates.Where(c => c.BestExePath is not null && !existing.Contains(c.BestExePath)).ToList();

        if (candidates.Count == 0)
        {
            ShowToast("开始菜单中没有发现新的可导入程序。");
            return;
        }

        var confirmVm = new ScanConfirmationViewModel(candidates, isDefaultSelected: false);
        var window = new ScanConfirmationWindow(confirmVm) { Owner = Application.Current.MainWindow };
        if (window.ShowDialog() != true) return;

        var newGames = confirmVm.BuildSelectedGames();
        if (newGames.Count == 0) return;

        foreach (var game in newGames)
            TryAssignIcon(game);

        await _repository.AddGamesAsync(newGames);
        await LoadAsync();
        ShowToast($"已导入 {newGames.Count} 个程序");
    }

    [RelayCommand]
    private void ToggleFloatingWindow()
    {
        if (_floatingWindow is not null)
        {
            _floatingWindow.Close();
            return;
        }

        var window = new FloatingFavoritesWindow(this);
        var left = _settingsService.Current.FloatingWindowLeft;
        var top = _settingsService.Current.FloatingWindowTop;
        if (left is not null && top is not null)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = left.Value;
            window.Top = top.Value;
        }

        window.Closed += (_, _) =>
        {
            _settingsService.Current.FloatingWindowLeft = window.Left;
            _settingsService.Current.FloatingWindowTop = window.Top;
            if (!_isShuttingDown)
                _settingsService.Current.ShowFloatingFavoritesWindow = false;
            _settingsService.Save();
            _floatingWindow = null;
        };

        _floatingWindow = window;
        _settingsService.Current.ShowFloatingFavoritesWindow = true;
        _settingsService.Save();
        window.Show();
    }

    private bool _isShuttingDown;

    /// <summary>Called when the app exits so the floating window's open state is remembered for next launch.</summary>
    public void PrepareForShutdown() => _isShuttingDown = true;

    public void RestoreFloatingWindowIfNeeded()
    {
        if (_settingsService.Current.ShowFloatingFavoritesWindow && _floatingWindow is null)
            ToggleFloatingWindow();
    }

    private async void OnGameDetailsRequested(object? sender, EventArgs e)
    {
        if (sender is not GameCardViewModel cardVm) return;

        var groups = await _repository.GetAllGroupsAsync();
        var detailVm = new GameDetailViewModel(cardVm.GameModel, _repository, groups, _settingsService);
        var window = new GameDetailWindow(detailVm) { Owner = Application.Current.MainWindow };

        if (window.ShowDialog() == true)
            await LoadAsync();
    }

    private bool FilterGame(object obj)
    {
        if (obj is not GameCardViewModel vm) return false;

        if (!string.IsNullOrWhiteSpace(SearchText) &&
            !vm.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            return false;

        var nav = SelectedNavItem;
        if (nav is null) return true;

        return nav.Kind switch
        {
            LibraryFilterKind.Favorites => vm.IsFavorite,
            LibraryFilterKind.RecentlyPlayed => vm.LastPlayedAt is not null,
            LibraryFilterKind.Group => nav.GroupId is not null && vm.GroupIds.Contains(nav.GroupId.Value),
            _ => true
        };
    }

    partial void OnSelectedNavItemChanged(NavItem? value) => GamesView.Refresh();

    partial void OnSearchTextChanged(string value) => GamesView.Refresh();

    private void OnSessionEnded(object? sender, GameSessionEndedEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var vm = Games.FirstOrDefault(g => g.Id == e.GameId);
            vm?.OnSessionEnded(e.DurationSeconds);
        });
    }
}
