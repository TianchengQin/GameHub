using System.Windows;
using GameHub.App.Theming;
using GameHub.App.ViewModels;
using GameHub.Core;
using GameHub.Core.Launching;
using GameHub.Core.Repositories;
using GameHub.Core.Scanning;
using GameHub.Core.Settings;
using GameHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GameHub.App;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        AppPaths.EnsureDirectories();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddDbContextFactory<LibraryDbContext>(options =>
                    options.UseSqlite($"Data Source={AppPaths.DatabasePath}"));

                services.AddSingleton<IGameRepository, GameRepository>();
                services.AddSingleton<GameScanner>();
                services.AddSingleton<GameLauncher>();
                services.AddSingleton<ISettingsService, SettingsService>();

                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        using (var scope = _host.Services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LibraryDbContext>>();
            using var db = factory.CreateDbContext();
            db.Database.Migrate();
        }

        var settingsService = _host.Services.GetRequiredService<ISettingsService>();
        ThemeApplier.Apply(settingsService.Current.Theme);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
