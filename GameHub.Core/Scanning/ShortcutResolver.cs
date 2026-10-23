using System.Runtime.InteropServices;

namespace GameHub.Core.Scanning;

/// <summary>Resolves a .lnk shortcut to its target path via the WScript.Shell automation object.</summary>
public static class ShortcutResolver
{
    public static string? ResolveTarget(string lnkPath)
    {
        object? shell = null;
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null) return null;

            shell = Activator.CreateInstance(type);
            if (shell is null) return null;

            dynamic shortcut = ((dynamic)shell).CreateShortcut(lnkPath);
            string? target = shortcut.TargetPath;
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (shell is not null && Marshal.IsComObject(shell))
                Marshal.ReleaseComObject(shell);
        }
    }
}
