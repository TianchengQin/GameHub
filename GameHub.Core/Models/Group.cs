namespace GameHub.Core.Models;

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? AccentColor { get; set; }

    public List<GameGroupMap> GameMaps { get; set; } = new();
}

public class GameGroupMap
{
    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;

    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
}
