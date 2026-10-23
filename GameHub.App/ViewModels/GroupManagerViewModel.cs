using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameHub.Core.Models;
using GameHub.Core.Repositories;

namespace GameHub.App.ViewModels;

public partial class GroupRowViewModel : ObservableObject
{
    public Guid Id { get; }
    public int SortOrder { get; set; }

    [ObservableProperty]
    private string _name;

    public GroupRowViewModel(Group group)
    {
        Id = group.Id;
        SortOrder = group.SortOrder;
        _name = group.Name;
    }
}

public partial class GroupManagerViewModel : ObservableObject
{
    private readonly IGameRepository _repository;

    public ObservableCollection<GroupRowViewModel> Groups { get; } = new();

    [ObservableProperty]
    private string _newGroupName = string.Empty;

    [ObservableProperty]
    private GroupRowViewModel? _selectedGroup;

    public GroupManagerViewModel(IGameRepository repository)
    {
        _repository = repository;
    }

    public async Task LoadAsync()
    {
        Groups.Clear();
        foreach (var group in await _repository.GetAllGroupsAsync())
            Groups.Add(new GroupRowViewModel(group));
    }

    /// <summary>Moves <paramref name="dragged"/> to the position currently held by <paramref name="target"/> and saves the new order.</summary>
    public async Task MoveGroupAsync(GroupRowViewModel dragged, GroupRowViewModel target)
    {
        var from = Groups.IndexOf(dragged);
        var to = Groups.IndexOf(target);
        if (from < 0 || to < 0 || from == to) return;

        Groups.Move(from, to);
        SelectedGroup = dragged;

        for (var i = 0; i < Groups.Count; i++)
            Groups[i].SortOrder = i;

        foreach (var row in Groups)
            await _repository.UpdateGroupAsync(new Group { Id = row.Id, Name = row.Name, SortOrder = row.SortOrder });
    }

    [RelayCommand]
    private async Task AddGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGroupName)) return;

        var group = new Group { Name = NewGroupName.Trim(), SortOrder = Groups.Count };
        await _repository.AddGroupAsync(group);
        Groups.Add(new GroupRowViewModel(group));
        NewGroupName = string.Empty;
    }

    [RelayCommand]
    private async Task RenameSelectedGroupAsync()
    {
        var row = SelectedGroup;
        if (row is null || string.IsNullOrWhiteSpace(row.Name)) return;

        await _repository.UpdateGroupAsync(new Group { Id = row.Id, Name = row.Name.Trim(), SortOrder = row.SortOrder });
    }

    [RelayCommand]
    private async Task DeleteSelectedGroupAsync()
    {
        var row = SelectedGroup;
        if (row is null) return;

        await _repository.DeleteGroupAsync(row.Id);
        Groups.Remove(row);
        SelectedGroup = null;
    }
}
