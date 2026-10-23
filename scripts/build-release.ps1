<#
.SYNOPSIS
  Builds a GameHub release: tests -> self-contained publish -> installer -> SHA-256.
.EXAMPLE
  powershell -File scripts\build-release.ps1            # version taken from GameHub.App.csproj
  powershell -File scripts\build-release.ps1 -SkipTests
.NOTES
  The produced dist\GameHubSetup-<version>.exe is also the "update program": running it over an
  existing install upgrades in place, and the in-app updater downloads and runs the same file from
  the matching GitHub Release (attach the .exe there and keep the SHA-256 line in the release notes).
#>
param(
    [string]$Version,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root

if (-not $Version) {
    $csproj = [xml](Get-Content (Join-Path $root 'GameHub.App\GameHub.App.csproj'))
    $Version = ($csproj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
}
if (-not $Version) { throw 'Could not determine the version; pass -Version x.y.z' }
Write-Host "== GameHub $Version ==" -ForegroundColor Cyan

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = 'C:\Program Files\dotnet\dotnet.exe' }

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 (ISCC.exe) not found. Install it with: winget install JRSoftware.InnoSetup' }

if (-not $SkipTests) {
    & $dotnet test GameHub.Core.Tests\GameHub.Core.Tests.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}

if (Test-Path publish\GameHub.App) { Remove-Item publish\GameHub.App -Recurse -Force }
& $dotnet publish GameHub.App\GameHub.App.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$Version -o publish\GameHub.App
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }

& $iscc "/DMyAppVersion=$Version" installer\setup.iss
if ($LASTEXITCODE -ne 0) { throw 'Installer compile failed' }

$installer = Join-Path $root "dist\GameHubSetup-$Version.exe"
$hash = (Get-FileHash $installer -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash *GameHubSetup-$Version.exe" | Set-Content "$installer.sha256" -Encoding ascii

Write-Host ''
Write-Host "Installer : $installer" -ForegroundColor Green
Write-Host "SHA-256   : $hash" -ForegroundColor Green
Write-Host ''
Write-Host 'To publish the update, create a GitHub Release tagged v' -NoNewline
Write-Host "$Version" -NoNewline
Write-Host ', attach the installer, and put this line in the notes:'
Write-Host "SHA-256: $hash"
