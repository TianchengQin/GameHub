using GameHub.Core.Models;
using GameHub.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Data;

/// <summary>
/// Registered as a singleton; creates a short-lived <see cref="LibraryDbContext"/>
/// per operation via the factory instead of holding one long-lived context,
/// which avoids DI lifetime mismatches between a singleton repository and a
/// scoped/transient DbContext.
/// </summary>
public class GameRepository : IGameRepository
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;

    public GameRepository(IDbContextFactory<LibraryDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<Game>> GetAllGamesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Games
            .Include(g => g.GroupMaps)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Game?> GetGameAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Games
            .Include(g => g.GroupMaps)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task AddGameAsync(Game game)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Games.Add(game);
        await db.SaveChangesAsync();
    }

    public async Task AddGamesAsync(IEnumerable<Game> games)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Games.AddRange(games);
        await db.SaveChangesAsync();
    }

    public async Task UpdateGameAsync(Game game)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Games.Update(game);
        await db.SaveChangesAsync();
    }

    public async Task DeleteGameAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var game = await db.Games.FindAsync(id);
        if (game is null) return;

        db.Games.Remove(game);
        await db.SaveChangesAsync();
    }

    public async Task<List<Group>> GetAllGroupsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Groups
            .AsNoTracking()
            .OrderBy(g => g.SortOrder)
            .ToListAsync();
    }

    public async Task AddGroupAsync(Group group)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Groups.Add(group);
        await db.SaveChangesAsync();
    }

    public async Task UpdateGroupAsync(Group group)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Groups.Update(group);
        await db.SaveChangesAsync();
    }

    public async Task DeleteGroupAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var group = await db.Groups.FindAsync(id);
        if (group is null) return;

        db.Groups.Remove(group);
        await db.SaveChangesAsync();
    }

    public async Task AssignGameToGroupAsync(Guid gameId, Guid groupId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var exists = await db.GameGroupMaps.AnyAsync(m => m.GameId == gameId && m.GroupId == groupId);
        if (exists) return;

        db.GameGroupMaps.Add(new GameGroupMap { GameId = gameId, GroupId = groupId });
        await db.SaveChangesAsync();
    }

    public async Task RemoveGameFromGroupAsync(Guid gameId, Guid groupId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var map = await db.GameGroupMaps.FirstOrDefaultAsync(m => m.GameId == gameId && m.GroupId == groupId);
        if (map is null) return;

        db.GameGroupMaps.Remove(map);
        await db.SaveChangesAsync();
    }

    public async Task ReplaceAllAsync(List<Game> games, List<Group> groups, List<(Guid GameId, Guid GroupId)> links)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        db.GameGroupMaps.RemoveRange(db.GameGroupMaps);
        db.Games.RemoveRange(db.Games);
        db.Groups.RemoveRange(db.Groups);
        await db.SaveChangesAsync();

        foreach (var game in games)
            game.GroupMaps.Clear();

        db.Games.AddRange(games);
        db.Groups.AddRange(groups);
        await db.SaveChangesAsync();

        foreach (var (gameId, groupId) in links)
            db.GameGroupMaps.Add(new GameGroupMap { GameId = gameId, GroupId = groupId });

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
