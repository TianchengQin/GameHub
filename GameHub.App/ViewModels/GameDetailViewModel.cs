using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameHub.Core;
using GameHub.Core.Models;
using GameHub.Core.Repositories;
using GameHub.Core.Settings;
using Microsoft.Win32;

namespace GameHub.App.ViewModels;

public partial class GroupMembershipRowViewModel : ObservableObject
{
    public Guid GroupId { get; }
    public string Name { get; }

    [ObservableProperty]
    private bool _isChecked;

    public GroupMembershipRowViewModel(Group group, bool isChecked)
    {
        GroupId = group.Id;
        Name = group.Name;
        _isChecked = isChecked;
    }
}

public partial class GameDetailViewModel : ObservableObject
{
    private readonly Game _game;
    private readonly IGameRepository _repository;

    public Guid Id => _game.Id;
    public DateTime DateAdded => _game.DateAdded;
    public int PlayCount => _game.PlayCount;
    public DateTime? LastPlayedAt => _game.LastPlayedAt;
    public string TotalPlaytimeDisplay => FormatPlaytime(_game.TotalPlaytimeSeconds);

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _executablePath;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _arguments;

    [ObservableProperty]
    private string? _workingDirectory;

    /// <summary>Only the user-chosen cover; the auto-extracted icon is never stored here.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewImagePath))]
    private string? _coverImagePath;

    public string? PreviewImagePath => CoverImagePath ?? _game.IconCachePath;

    public ISettingsService SettingsService { get; }

    public ObservableCollection<GroupMembershipRowViewModel> GroupMemberships { get; } = new();

    public GameDetailViewModel(Game game, IGameRepository repository, List<Group> allGroups, ISettingsService settingsService)
    {
        _game = game;
        _repository = repository;
        SettingsService = settingsService;

        _name = game.Name;
        _executablePath = game.ExecutablePath;
        _notes = game.Notes;
        _arguments = game.Arguments;
        _workingDirectory = game.WorkingDirectory;
        _coverImagePath = game.CoverImagePath;

        var currentGroupIds = game.GroupMaps.Select(m => m.GroupId).ToHashSet();
        foreach (var group in allGroups)
            GroupMemberships.Add(new GroupMembershipRowViewModel(group, currentGroupIds.Contains(group.Id)));
    }

    [RelayCommand]
    private void ChooseCoverImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择封面图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.webp"
        };
        if (dialog.ShowDialog() != true) return;

        var extension = Path.GetExtension(dialog.FileName);
        var destination = Path.Combine(AppPaths.CoversFolder, Id + extension);

        File.Copy(dialog.FileName, destination, overwrite: true);
        CoverImagePath = destination;
    }

    [RelayCommand]
    private void RelocateExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "重新选择游戏可执行文件",
            Filter = "可执行文件 (*.exe)|*.exe"
        };
        if (dialog.ShowDialog() != true) return;

        ExecutablePath = dialog.FileName;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        _game.Name = string.IsNullOrWhiteSpace(Name) ? _game.Name : Name.Trim();
        _game.ExecutablePath = ExecutablePath;
        _game.Notes = Notes;
        _game.Arguments = Arguments;
        _game.WorkingDirectory = WorkingDirectory;
        _game.CoverImagePath = CoverImagePath;

        await _repository.UpdateGameAsync(_game);

        foreach (var row in GroupMemberships)
        {
            if (row.IsChecked)
                await _repository.AssignGameToGroupAsync(Id, row.GroupId);
            else
                await _repository.RemoveGameFromGroupAsync(Id, row.GroupId);
        }
    }

    private static string FormatPlaytime(long totalSeconds)
    {
        var span = TimeSpan.FromSeconds(totalSeconds);
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours} 小时 {span.Minutes} 分钟";
        if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes} 分钟";
        return "少于 1 分钟";
    }
}
