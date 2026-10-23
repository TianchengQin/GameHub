using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GameHub.Core.Models;
using GameHub.Core.Scanning;

namespace GameHub.App.ViewModels;

public partial class ScanRowViewModel : ObservableObject
{
    public ScanCandidate Candidate { get; }
    public List<string> ExePaths { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string? _selectedExePath;

    public ScanRowViewModel(ScanCandidate candidate, bool isDefaultSelected)
    {
        Candidate = candidate;
        ExePaths = candidate.ExeCandidates.Select(c => c.FullPath).ToList();
        _name = candidate.SuggestedName;
        _selectedExePath = candidate.BestExePath;
        _isSelected = isDefaultSelected && candidate.IncludeByDefault;
    }
}

public class ScanConfirmationViewModel
{
    public ObservableCollection<ScanRowViewModel> Rows { get; }

    /// <param name="isDefaultSelected">Folder scans pre-tick every row; the start menu lists many non-games, so it starts unticked.</param>
    public ScanConfirmationViewModel(IEnumerable<ScanCandidate> candidates, bool isDefaultSelected = true)
    {
        Rows = new ObservableCollection<ScanRowViewModel>(candidates.Select(c => new ScanRowViewModel(c, isDefaultSelected)));
    }

    public List<Game> BuildSelectedGames()
    {
        return Rows
            .Where(r => r.IsSelected && !string.IsNullOrWhiteSpace(r.SelectedExePath))
            .Select(r => new Game
            {
                Name = string.IsNullOrWhiteSpace(r.Name) ? "未命名游戏" : r.Name.Trim(),
                ExecutablePath = r.SelectedExePath!,
                Source = GameSource.FolderScan
            })
            .ToList();
    }
}
