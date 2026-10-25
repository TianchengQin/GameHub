# GameHub

**English** | [简体中文](README.zh-CN.md)

A native Windows desktop launcher for your game library. Add any `.exe` game by hand, by drag-and-drop, by scanning a folder or from the Start Menu; organise games into groups, pin favorites to a floating launcher, and see when you last played each one. Ships with an installer and a built-in updater.

The UI uses [WPF-UI](https://github.com/lepoco/wpfui) for a Windows 11 style (Fluent / Mica, dark by default).

## Features

### Adding games
- **Add one exe** with a file picker, or **drag an `.exe` onto the window**
- **Scan a folder** (e.g. `D:\Games`): every subfolder becomes a candidate. Installers, uninstallers, redistributables, crash reporters and the like are filtered out by a configurable blacklist, the remaining exes are ranked (name similarity to the folder, depth, file size), and you confirm what to import and can switch to another detected exe
- **Import from the Start Menu**: reads `.lnk` shortcuts, skips uninstall/help/readme entries, and lets you tick what to import

### Organising and browsing
- Built-in views: All games, Favorites, Recently played
- Custom groups; a game can belong to several groups
- **Drag a game card onto a group** in the sidebar to add it; **drag the ⠿ handle** in "Manage groups" to reorder groups
- Search, right-click menu (details, favorite, show in folder, remove)
- **Favorites** are marked with a clickable ★ on the card and sorted to the front

### Launching and stats
- The last-played time is recorded the moment you click launch, regardless of how the game process behaves afterwards
- Launch count and total play time, a "running" state that prevents double launches
- Missing or invalid paths show a toast message and a "path invalid" badge; the details window can relocate the exe
- Optional: minimize to the system tray after launching a game (tray menu: show window, toggle floating window, exit)

### Floating favorites window
A small always-on-top, draggable window that shows only your favorites. Click an icon to launch. Open it from the sidebar ("收藏悬浮窗") or from the tray menu; its position and open/closed state are remembered.

### Icons and covers
- On import the exe's **256 px** icon is extracted (what Explorer shows for extra-large icons); older low-resolution caches are upgraded automatically
- Games without a custom cover show the icon centered on the tile instead of stretched; custom covers fill the tile
- Choose your own cover image, or **search SteamGridDB** from the details window (needs your own free API key, set in Settings)

### Settings
Theme (light / dark / system), launch behavior, SteamGridDB key, scan blacklist rules, data folder shortcut, **JSON backup / restore** of the whole library, and the **Update** section.

## Install

Download `GameHubSetup-<version>.exe` from the [Releases](https://github.com/TianchengQin/GameHub/releases) page and run it. It is self-contained, so no .NET runtime is needed. It creates a Start Menu shortcut and (by default) a desktop shortcut. Running a newer installer over an existing install upgrades it in place and keeps your data.

Your data lives in `%LocalAppData%\GameHub\`:

```
library.db     SQLite database
icons\         extracted exe icons
covers\        custom cover images
settings.json  settings
```

Uninstalling asks whether to also delete that folder (a silent uninstall keeps it).

## Updating

Open **Settings → Update** to check for and install a new version in one click; GameHub also checks quietly at startup (can be turned off). The updater reads the latest GitHub Release of this repository, downloads the `GameHubSetup-*.exe` asset, **verifies its SHA-256**, runs it silently and relaunches GameHub. A release without a checksum is refused (open the release page and install manually instead).

## Tech stack

| Layer | Technology |
|---|---|
| UI | .NET 8, WPF, [WPF-UI](https://github.com/lepoco/wpfui) |
| MVVM | [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) |
| Storage | SQLite via EF Core |
| Dependency injection | `Microsoft.Extensions.Hosting` |
| Tray icon / icon extraction | WinForms `NotifyIcon`, Windows Shell (`IShellItemImageFactory`) |
| Installer | [Inno Setup 6](https://jrsoftware.org/isinfo.php) |

## Project layout

```
GameHub.sln
├─ GameHub.Core/        models, scanning, launching, icons, settings, backup, SteamGridDB client, update checker (no WPF)
├─ GameHub.Data/        EF Core DbContext, repository, migrations
├─ GameHub.App/         WPF app (views, view models, converters, tray, updater glue)
├─ GameHub.Core.Tests/  xUnit tests
├─ installer/setup.iss  Inno Setup script
├─ scripts/build-release.ps1   tests -> publish -> installer -> SHA-256
└─ tools/make-icon.ps1  generates the app icon
```

## Development

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet build GameHub.sln
dotnet test GameHub.Core.Tests/GameHub.Core.Tests.csproj
dotnet run --project GameHub.App/GameHub.App.csproj
```

Set the environment variable `GAMEHUB_DATA_DIR` to run against an isolated data folder instead of `%LocalAppData%\GameHub`, which is handy for testing without touching your real library.

## Releasing a new version

1. Bump `<Version>` in `GameHub.App/GameHub.App.csproj`.
2. Run the build script (needs Inno Setup: `winget install JRSoftware.InnoSetup`):

   ```powershell
   powershell -File scripts\build-release.ps1
   ```

   It runs the tests, publishes a self-contained single-file build, compiles `dist\GameHubSetup-<version>.exe` and prints its SHA-256.
3. Create a GitHub Release tagged `v<version>`, attach the installer and add a line `SHA-256: <hash>` to the notes (GitHub also records a digest for uploaded assets, which the updater uses when present).

The installer doubles as the update program: the in-app updater downloads this same file from the release.

## Known limitations

- **Play time** can be under-counted for games whose main exe is only a launcher that hands off to a child process and exits. The *last played* time is always accurate because it is recorded at launch.
- Importing from Steam / Epic / GOG libraries is not supported yet.
- The installer UI is English (a Simplified Chinese language file is not bundled with Inno Setup; see the comment in `installer/setup.iss`).

## Roadmap

Done: Start Menu import, tray minimize, drag-to-group and group reordering, toast notifications, SteamGridDB covers, favorites and the floating window, sharp icons, in-app updater.

Next:
- [ ] Import installed games from Steam / Epic / GOG / Ubisoft / Battle.net
- [ ] Accurate play time by tracking the child-process tree
- [ ] Rating and screenshots on the details page
- [ ] Sync the library across devices

Later:
- [ ] Gamepad-friendly "Big Picture" mode
- [ ] Play statistics dashboard
- [ ] Plugin-style library importers

Issues and pull requests are welcome.
