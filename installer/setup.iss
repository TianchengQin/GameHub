; GameHub installer script (Inno Setup 6)
; Build the app first:
;   dotnet publish GameHub.App\GameHub.App.csproj -c Release -r win-x64 --self-contained true ^
;       -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\GameHub.App
; Then compile this script:
;   "C:\Users\<you>\AppData\Local\Programs\Inno Setup 6\ISCC.exe" installer\setup.iss

#define MyAppName "GameHub"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "GameHub"
#define MyAppExeName "GameHub.App.exe"
#define MyPublishDir "..\publish\GameHub.App"

[Setup]
AppId={{B6F2D9B2-6B1E-4B6C-9B0E-8B5F7B6C9B21}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=GameHubSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
; Simplified Chinese isn't bundled with Inno Setup by default. To add it, download
; ChineseSimplified.isl from https://jrsoftware.org/files/istrans/ into the
; "Languages" folder next to ISCC.exe, then uncomment the line below.
; Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
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
                mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(UserDataDir, True, True, True);
      end;
    end;
  end;
end;
