using GameHub.Core.Models;

namespace GameHub.Core.Repositories;

public interface IGameRepository
{
    Task<List<Game>> GetAllGamesAsync();
    Task<Game?> GetGameAsync(Guid id);
    Task AddGameAsync(Game game);
    Task AddGamesAsync(IEnumerable<Game> games);
    Task UpdateGameAsync(Game game);
    Task DeleteGameAsync(Guid id);

    Task<List<Group>> GetAllGroupsAsync();
    Task AddGroupAsync(Group group);
    Task UpdateGroupAsync(Group group);
    Task DeleteGroupAsync(Guid id);

    Task AssignGameToGroupAsync(Guid gameId, Guid groupId);
    Task RemoveGameFromGroupAsync(Guid gameId, Guid groupId);

    /// <summary>Wipes the current library and replaces it with the given data (used by backup restore).</summary>
    Task ReplaceAllAsync(List<Game> games, List<Group> groups, List<(Guid GameId, Guid GroupId)> links);
}
