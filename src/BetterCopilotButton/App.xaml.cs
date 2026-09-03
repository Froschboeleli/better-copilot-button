using System.Threading;
using System.Windows;
using BetterCopilotButton.Models;
using BetterCopilotButton.Services;

namespace BetterCopilotButton;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\BetterCopilotButton.SingleInstance";
    private const string ShowEventName = @"Local\BetterCopilotButton.ShowSettings";

    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _showEvent;
    private Thread? _showListener;
    private TrayIconService? _tray;
    private CopilotKeyInterceptor? _interceptor;
    private MainWindow? _settingsWindow;
    private bool _isQuitting;
    private bool _shownTrayHint;

    public AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var created);
        _ownsMutex = created;
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

        if (!created)
        {
            try
            {
                _showEvent.Set();
            }
            catch (Exception)
            {
                // The first instance may already be exiting.
            }

            Shutdown();
            return;
        }

        base.OnStartup(e);

        var args = e.Args ?? [];
        if (args.Any(a => string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            SilentUninstall();
            Shutdown();
            return;
        }

        Settings = SettingsStore.Load();
        Settings.StartAtLogin = StartupRegistration.IsEnabled();

        _interceptor = new CopilotKeyInterceptor();
        _interceptor.CopilotPressed += OnCopilotPressed;

        _tray = new TrayIconService();
        _tray.OpenSettingsRequested += ShowSettings;
        _tray.ToggleRequested += ToggleRemapFromTray;
        _tray.QuitRequested += Quit;
        _tray.SetRemapEnabled(Settings.RemapEnabled);

        ApplyRemapState(Settings.RemapEnabled, showErrors: false);

        _showListener = new Thread(ListenForShowSignal)
        {
            IsBackground = true,
            Name = "BetterCopilotButton.ShowListener"
        };
        _showListener.Start();

        var startHidden = args.Any(a =>
            string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "/tray", StringComparison.OrdinalIgnoreCase));

        if (!startHidden)
        {
            ShowSettings();
        }
    }

    public void ShowSettings()
    {
        Dispatcher.BeginInvoke(() =>
        {
            _settingsWindow ??= new MainWindow();
            _settingsWindow.Show();
            _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            _settingsWindow.RefreshFromSettings();
        });
    }

    public void HideSettingsToTray()
    {
        if (_settingsWindow is null)
        {
            return;
        }

        _settingsWindow.Hide();
        if (!_shownTrayHint)
        {
            _shownTrayHint = true;
            _tray?.ShowInstalledBalloon();
        }
    }

    public void SaveAndApply(AppSettings settings, bool showErrors)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
        SettingsStore.Save(settings);

        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe))
            {
                StartupRegistration.SetEnabled(settings.StartAtLogin, exe);
            }
        }
        catch (Exception ex)
        {
            if (showErrors)
            {
                MessageBox.Show(
                    "Could not update the start-at-login setting.\n\n" + ex.Message,
                    "Better Copilot Button",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        ApplyRemapState(settings.RemapEnabled, showErrors);
        _tray?.SetRemapEnabled(settings.RemapEnabled);
        _settingsWindow?.RefreshFromSettings();
    }

    public void ToggleRemapFromTray()
    {
        Settings.RemapEnabled = !Settings.RemapEnabled;
        SaveAndApply(Settings, showErrors: true);
    }

    public void Quit()
    {
        _isQuitting = true;
        _interceptor?.Dispose();
        _tray?.Dispose();
        _settingsWindow?.ForceClose();
        Shutdown();
    }

    public void RunUninstallInteractive()
    {
        var result = MessageBox.Show(
            "Uninstall Better Copilot Button for this user?\n\n" +
            "The Copilot key will go back to normal as soon as this app exits. " +
            "Start menu and login items will be removed. Settings in AppData will be deleted.",
            "Uninstall Better Copilot Button",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _interceptor?.Stop();
            InstallService.Uninstall(deleteSettings: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Uninstall hit a problem:\n\n" + ex.Message,
                "Better Copilot Button",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        Quit();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _isQuitting = true;
        _showEvent?.Set();
        _interceptor?.Dispose();
        _tray?.Dispose();
        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Already released.
            }
        }

        _mutex?.Dispose();
        _showEvent?.Dispose();
        base.OnExit(e);
    }

    private void ApplyRemapState(bool enabled, bool showErrors)
    {
        if (_interceptor is null)
        {
            return;
        }

        try
        {
            if (enabled)
            {
                _interceptor.Start();
            }
            else
            {
                _interceptor.Stop();
            }
        }
        catch (Exception ex)
        {
            Settings.RemapEnabled = false;
            SettingsStore.Save(Settings);
            _tray?.SetRemapEnabled(false);
            if (showErrors)
            {
                MessageBox.Show(
                    "Could not enable the Copilot key remap.\n\n" + ex.Message,
                    "Better Copilot Button",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void OnCopilotPressed()
    {
        if (!Settings.RemapEnabled)
        {
            return;
        }

        try
        {
            AppLauncher.LaunchOrFocus(Settings);
        }
        catch (Exception ex)
        {
            Dispatcher.BeginInvoke(() =>
            {
                MessageBox.Show(
                    "The Copilot key was pressed, but the target app could not be opened.\n\n" + ex.Message,
                    "Better Copilot Button",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                ShowSettings();
            });
        }
    }

    private void ListenForShowSignal()
    {
        while (!_isQuitting)
        {
            try
            {
                if (_showEvent is null || !_showEvent.WaitOne(TimeSpan.FromMilliseconds(500)))
                {
                    continue;
                }

                if (!_isQuitting)
                {
                    ShowSettings();
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private static void SilentUninstall()
    {
        try
        {
            InstallService.Uninstall(deleteSettings: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Uninstall hit a problem:\n\n" + ex.Message,
                "Better Copilot Button",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
