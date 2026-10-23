using System.Diagnostics;
using System.Windows;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace GameHub.App.Tray;

/// <summary>System-tray icon that lives for the whole app session and can restore the hidden main window.</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Window _mainWindow;

    public TrayIconService(Window mainWindow, Action toggleFloatingWindow)
    {
        _mainWindow = mainWindow;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (_, _) => RestoreMainWindow());
        menu.Items.Add("显示/隐藏收藏悬浮窗", null, (_, _) => toggleFloatingWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出 GameHub", null, (_, _) => Application.Current.Shutdown());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "GameHub",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) RestoreMainWindow();
        };
    }

    public void MinimizeToTray()
    {
        _mainWindow.Hide();
        _notifyIcon.ShowBalloonTip(1500, "GameHub", "已最小化到系统托盘，单击图标可恢复。", Forms.ToolTipIcon.None);
    }

    public void RestoreMainWindow()
    {
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private static Drawing.Icon LoadAppIcon()
    {
        try
        {
            var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exe))
            {
                var icon = Drawing.Icon.ExtractAssociatedIcon(exe);
                if (icon is not null) return icon;
            }
        }
        catch
        {
            // Fall through to the generic application icon.
        }

        return Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
