; GameHub installer script (Inno Setup 6). Normally driven by scripts\build-release.ps1, which:
;   1. publishes a self-contained single-file build to publish\GameHub.App
;   2. compiles this script:  ISCC.exe /DMyAppVersion=1.2.0 installer\setup.iss
; Re-running a newer GameHubSetup-<version>.exe over an existing install upgrades it in place.

#ifndef MyAppVersion
  #define MyAppVersion "1.2.0"
#endif
#ifndef MyAppId
  #define MyAppId "{{B6F2D9B2-6B1E-4B6C-9B0E-8B5F7B6C9B21}"
#endif
#ifndef MyAppName
  #define MyAppName "GameHub"
#endif
#define MyAppPublisher "GameHub"
#define MyAppExeName "GameHub.App.exe"
#define MyPublishDir "..\publish\GameHub.App"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=GameHubSetup-{#MyAppVersion}
SetupIconFile=..\GameHub.App\Assets\GameHub.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
; Simplified Chinese isn't bundled with Inno Setup by default. To add it, download
; ChineseSimplified.isl from https://jrsoftware.org/files/istrans/ into the
; "Languages" folder next to ISCC.exe, then uncomment the line below.
; Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"

[InstallDelete]
; Version 1.0 put its shortcuts in a "GameHub" folder inside the Start Menu; now it is a single shortcut.
Type: filesandordirs; Name: "{autoprograms}\{#MyAppName}"

[Files]
Source: "{#MyPublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent
; Used by the in-app updater, which runs this installer silently with /RELAUNCH.
Filename: "{app}\{#MyAppExeName}"; Flags: nowait runasoriginaluser; Check: ShouldRelaunch

[Code]
function ShouldRelaunch: Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), '/RELAUNCH') = 0 then
      Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  UserDataDir: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    UserDataDir := ExpandConstant('{localappdata}\GameHub');
    if DirExists(UserDataDir) then
    begin
      if MsgBox('是否同时删除游戏库数据（数据库、封面图、图标缓存）？' + #13#10 +
                '位置：' + UserDataDir + #13#10#13#10 +
                '选择"否"可以保留数据，以后重新安装 GameHub 时继续使用。',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      begin
        DelTree(UserDataDir, True, True, True);
      end;
    end;
  end;
end;
