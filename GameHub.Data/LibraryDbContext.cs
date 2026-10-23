using GameHub.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Data;

public class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
    {
    }

    public DbSet<Game> Games => Set<Game>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GameGroupMap> GameGroupMaps => Set<GameGroupMap>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Source).HasConversion<string>();
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(g => g.Id);
        });

        modelBuilder.Entity<GameGroupMap>(entity =>
        {
            entity.HasKey(m => new { m.GameId, m.GroupId });

            entity.HasOne(m => m.Game)
                .WithMany(g => g.GroupMaps)
                .HasForeignKey(m => m.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.Group)
                .WithMany(g => g.GameMaps)
                .HasForeignKey(m => m.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
