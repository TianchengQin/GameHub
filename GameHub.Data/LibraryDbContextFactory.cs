using GameHub.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GameHub.Data;

/// <summary>
/// Lets `dotnet ef migrations` construct a context at design time without
/// needing the WPF host to be running.
/// </summary>
public class LibraryDbContextFactory : IDesignTimeDbContextFactory<LibraryDbContext>
{
    public LibraryDbContext CreateDbContext(string[] args)
    {
        AppPaths.EnsureDirectories();

        var optionsBuilder = new DbContextOptionsBuilder<LibraryDbContext>();
        optionsBuilder.UseSqlite($"Data Source={AppPaths.DatabasePath}");
        return new LibraryDbContext(optionsBuilder.Options);
    }
}
