namespace GameHub.Core.Models;

public enum GameSource
{
    Manual,
    FolderScan
}

public class Game
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? IconCachePath { get; set; }
    public string? CoverImagePath { get; set; }
    public GameSource Source { get; set; } = GameSource.Manual;
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    public DateTime? LastPlayedAt { get; set; }
    public int PlayCount { get; set; }
    public long TotalPlaytimeSeconds { get; set; }
    public bool IsFavorite { get; set; }
    public string? Notes { get; set; }

    public List<GameGroupMap> GroupMaps { get; set; } = new();
}
