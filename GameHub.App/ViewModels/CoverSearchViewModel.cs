using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameHub.Core.Covers;

namespace GameHub.App.ViewModels;

public class CoverSearchResultViewModel
{
    public CoverSearchResultViewModel(SteamGridDbImage image)
    {
        ImageUrl = image.Url;
        ThumbUrl = image.ThumbUrl;
    }

    public string ImageUrl { get; }
    public string ThumbUrl { get; }
}

public partial class CoverSearchViewModel : ObservableObject
{
    private readonly SteamGridDbClient _client;

    public ObservableCollection<CoverSearchResultViewModel> Results { get; } = new();

    [ObservableProperty]
    private string _searchTerm;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private CoverSearchResultViewModel? _selectedResult;

    public CoverSearchViewModel(SteamGridDbClient client, string initialTerm)
    {
        _client = client;
        _searchTerm = initialTerm;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm)) return;

        IsSearching = true;
        StatusMessage = "搜索中…";
        Results.Clear();
        SelectedResult = null;

        try
        {
            var games = await _client.SearchGamesAsync(SearchTerm.Trim());
            if (games.Count == 0)
            {
                StatusMessage = "没有找到匹配的游戏，换个名称试试。";
                return;
            }

            var images = await _client.GetGridsAsync(games[0].Id);
            foreach (var image in images.Take(24))
                Results.Add(new CoverSearchResultViewModel(image));

            StatusMessage = Results.Count == 0
                ? $"找到了「{games[0].Name}」，但暂无可用封面。"
                : $"「{games[0].Name}」的封面，点击选择一张：";
        }
        catch (Exception ex)
        {
            StatusMessage = $"搜索失败：{ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    public Task<byte[]> DownloadSelectedAsync()
        => _client.DownloadImageAsync(SelectedResult!.ImageUrl);
}
