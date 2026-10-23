namespace GameHub.Core.Scanning;

public class ScanOptions
{
    public int MaxDepth { get; set; } = 3;

    public List<string> BlacklistPatterns { get; set; } = new()
    {
        "unins*.exe",
        "setup*.exe",
        "install*.exe",
        "*redist*.exe",
        "vcredist*.exe",
        "dxsetup*.exe",
        "dotnetfx*.exe",
        "dotnet-*.exe",
        "directx*.exe",
        "crashpad*.exe",
        "crashreporthandler*.exe",
        "crashreportclient*.exe",
        "easyanticheat*.exe",
        "eac_*.exe",
        "battleye*.exe",
        "be_*.exe",
        "*updater*.exe",
        "*update*.exe",
        "*helper*.exe",
        "*service*.exe",
        "ue4prereq*.exe",
        "ueprereq*.exe",
        "physx*.exe",
        "config*.exe",
        "launcher-update*.exe",
        "vc_redist*.exe",
        "oalinst.exe",
        "dxwebsetup.exe"
    };
}
