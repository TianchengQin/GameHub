namespace GameHub.App.ViewModels;

public enum LibraryFilterKind
{
    All,
    Favorites,
    RecentlyPlayed,
    Group
}

public class NavItem
{
    public string Icon { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public LibraryFilterKind Kind { get; init; }
    public Guid? GroupId { get; init; }
}
