using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace BetterCopilotButton.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _toggleItem;

    public TrayIconService()
    {
        _toggleItem = new Forms.ToolStripMenuItem("Enable remap");
        _toggleItem.Click += (_, _) => ToggleRequested?.Invoke();

        var menu = new Forms.ContextMenuStrip();
        var open = new Forms.ToolStripMenuItem("Open settings");
        open.Click += (_, _) => OpenSettingsRequested?.Invoke();
        var quit = new Forms.ToolStripMenuItem("Quit");
        quit.Click += (_, _) => QuitRequested?.Invoke();
        menu.Items.Add(open);
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(quit);

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "Better Copilot Button",
            Icon = LoadIcon(),
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke();
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                OpenSettingsRequested?.Invoke();
            }
        };
    }

    public event Action? OpenSettingsRequested;

    public event Action? ToggleRequested;

    public event Action? QuitRequested;

    public void SetRemapEnabled(bool enabled)
    {
        _toggleItem.Text = enabled ? "Disable remap" : "Enable remap";
        _notifyIcon.Text = enabled
            ? "Better Copilot Button (remap on)"
            : "Better Copilot Button (remap off)";
    }

    public void ShowInstalledBalloon()
    {
        _notifyIcon.ShowBalloonTip(
            4000,
            "Better Copilot Button",
            "Still running in the tray. Right-click the icon to disable remap or quit.",
            Forms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _toggleItem.Dispose();
    }

    private static Icon LoadIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe))
            {
                var associated = Icon.ExtractAssociatedIcon(exe);
                if (associated is not null)
                {
                    return associated;
                }
            }
        }
        catch (Exception)
        {
            // Fall through to the packaged icon.
        }

        var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
        if (streamInfo is not null)
        {
            return new Icon(streamInfo.Stream);
        }

        return SystemIcons.Application;
    }
}
