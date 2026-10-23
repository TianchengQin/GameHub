using GameHub.Core.Models;

namespace GameHub.Core.Backup;

public class LibraryExport
{
    public int Version { get; set; } = 1;
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public List<GameExportDto> Games { get; set; } = new();
    public List<GroupExportDto> Groups { get; set; } = new();
    public List<GameGroupLinkDto> Links { get; set; } = new();
}

public class GameExportDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? IconCachePath { get; set; }
    public string? CoverImagePath { get; set; }
    public string Source { get; set; } = nameof(GameSource.Manual);
    public DateTime DateAdded { get; set; }
    public DateTime? LastPlayedAt { get; set; }
    public int PlayCount { get; set; }
    public long TotalPlaytimeSeconds { get; set; }
    public bool IsFavorite { get; set; }
    public string? Notes { get; set; }
}

public class GroupExportDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? AccentColor { get; set; }
}

public class GameGroupLinkDto
{
    public Guid GameId { get; set; }
    public Guid GroupId { get; set; }
}

public static class LibraryExportMapper
{
    public static LibraryExport ToExport(IEnumerable<Game> games, IEnumerable<Group> groups)
    {
        var gameList = games.ToList();

        var export = new LibraryExport
        {
            Games = gameList.Select(g => new GameExportDto
            {
                Id = g.Id,
                Name = g.Name,
                ExecutablePath = g.ExecutablePath,
                Arguments = g.Arguments,
                WorkingDirectory = g.WorkingDirectory,
                IconCachePath = g.IconCachePath,
                CoverImagePath = g.CoverImagePath,
                Source = g.Source.ToString(),
                DateAdded = g.DateAdded,
                LastPlayedAt = g.LastPlayedAt,
                PlayCount = g.PlayCount,
                TotalPlaytimeSeconds = g.TotalPlaytimeSeconds,
                IsFavorite = g.IsFavorite,
                Notes = g.Notes
            }).ToList(),
            Groups = groups.Select(gr => new GroupExportDto
            {
                Id = gr.Id,
                Name = gr.Name,
                SortOrder = gr.SortOrder,
                AccentColor = gr.AccentColor
            }).ToList(),
            Links = gameList
                .SelectMany(g => g.GroupMaps.Select(m => new GameGroupLinkDto { GameId = g.Id, GroupId = m.GroupId }))
                .ToList()
        };

        return export;
    }

    public static (List<Game> Games, List<Group> Groups, List<(Guid GameId, Guid GroupId)> Links) FromExport(LibraryExport export)
    {
        var games = export.Games.Select(dto => new Game
        {
            Id = dto.Id,
            Name = dto.Name,
            ExecutablePath = dto.ExecutablePath,
            Arguments = dto.Arguments,
            WorkingDirectory = dto.WorkingDirectory,
            IconCachePath = dto.IconCachePath,
            CoverImagePath = dto.CoverImagePath,
            Source = Enum.TryParse<GameSource>(dto.Source, out var source) ? source : GameSource.Manual,
            DateAdded = dto.DateAdded,
            LastPlayedAt = dto.LastPlayedAt,
            PlayCount = dto.PlayCount,
            TotalPlaytimeSeconds = dto.TotalPlaytimeSeconds,
            IsFavorite = dto.IsFavorite,
            Notes = dto.Notes
        }).ToList();

        var groups = export.Groups.Select(dto => new Group
        {
            Id = dto.Id,
            Name = dto.Name,
            SortOrder = dto.SortOrder,
            AccentColor = dto.AccentColor
        }).ToList();

        var links = export.Links.Select(l => (l.GameId, l.GroupId)).ToList();

        return (games, groups, links);
    }
}
