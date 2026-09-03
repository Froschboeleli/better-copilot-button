using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace BetterCopilotButton.Services;

/// <summary>
/// Intercepts the Copilot hardware key, which most keyboards send as
/// Left Windows + Left Shift + F23. A short state machine swallows that
/// synthetic chord without breaking real Win and Shift keys.
/// </summary>
public sealed class CopilotKeyInterceptor : IDisposable
{
    private const int ChordWindowMs = 50;
    private const int HotkeyId = 0x0BCB;
    private const int ConsumeWindowMs = 400;

    private enum Phase
    {
        Idle,
        SawWin,
        SawWinShift,
        ConsumeChord
    }

    private readonly Dispatcher _dispatcher;
    private readonly NativeMethods.LowLevelKeyboardProc _hookProc;
    private readonly DispatcherTimer _replayTimer;
    private IntPtr _hook;
    private MessageWindow? _hotkeyWindow;
    private Phase _phase = Phase.Idle;
    private DateTime _lastLaunchUtc = DateTime.MinValue;
    private bool _started;

    public CopilotKeyInterceptor()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _hookProc = HookCallback;
        _replayTimer = new DispatcherTimer(DispatcherPriority.Send, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(ChordWindowMs)
        };
        _replayTimer.Tick += OnReplayTimerTick;
    }

    public event Action? CopilotPressed;

    public bool IsRunning => _started;

    public bool HookInstalled => _hook != IntPtr.Zero;

    public bool HotkeyInstalled { get; private set; }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            _hookProc,
            NativeMethods.GetModuleHandle(null),
            0);

        try
        {
            _hotkeyWindow = new MessageWindow(OnHotkey);
            HotkeyInstalled = NativeMethods.RegisterHotKey(
                _hotkeyWindow.Handle,
                HotkeyId,
                NativeMethods.ModWin | NativeMethods.ModShift | NativeMethods.ModNorepeat,
                NativeMethods.VkF23);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Hotkey registration failed: " + ex.Message);
            HotkeyInstalled = false;
        }

        if (_hook == IntPtr.Zero && !HotkeyInstalled)
        {
            throw new InvalidOperationException(
                "Could not listen for the Copilot key. Another tool may already own that shortcut.");
        }

        _started = true;
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _replayTimer.Stop();
        _phase = Phase.Idle;

        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        if (_hotkeyWindow is not null)
        {
            if (HotkeyInstalled)
            {
                NativeMethods.UnregisterHotKey(_hotkeyWindow.Handle, HotkeyId);
            }

            _hotkeyWindow.Dispose();
            _hotkeyWindow = null;
        }

        HotkeyInstalled = false;
        _started = false;
    }

    public void Dispose()
    {
        Stop();
        _replayTimer.Tick -= OnReplayTimerTick;
    }

    private void OnHotkey()
    {
        RaiseCopilot();
    }

    private void RaiseCopilot()
    {
        var now = DateTime.UtcNow;
        if (now - _lastLaunchUtc < TimeSpan.FromMilliseconds(ConsumeWindowMs))
        {
            return;
        }

        _lastLaunchUtc = now;
        _dispatcher.BeginInvoke(new Action(() => CopilotPressed?.Invoke()), DispatcherPriority.Normal);
    }

    private void OnReplayTimerTick(object? sender, EventArgs e)
    {
        _replayTimer.Stop();
        ReplaySuppressedKeys();
    }

    private void ReplaySuppressedKeys()
    {
        if (_phase == Phase.SawWin)
        {
            SendKey(NativeMethods.VkLwin, down: true, extended: true);
        }
        else if (_phase == Phase.SawWinShift)
        {
            SendKey(NativeMethods.VkLwin, down: true, extended: true);
            SendKey(NativeMethods.VkLshift, down: true, extended: false);
        }

        _phase = Phase.Idle;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || lParam == IntPtr.Zero)
        {
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        var info = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
        if ((info.Flags & NativeMethods.LlkfInjected) != 0)
        {
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var isDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
        var isUp = message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;
        var vk = (int)info.VkCode;

        if (isDown && HandleKeyDown(vk))
        {
            return (IntPtr)1;
        }

        if (isUp && HandleKeyUp(vk))
        {
            return (IntPtr)1;
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private bool HandleKeyDown(int vk)
    {
        if (_phase == Phase.ConsumeChord)
        {
            return vk is NativeMethods.VkF23 or NativeMethods.VkLshift or NativeMethods.VkLwin;
        }

        if (_phase == Phase.Idle && vk == NativeMethods.VkLwin)
        {
            _phase = Phase.SawWin;
            RestartTimer();
            return true;
        }

        if (_phase == Phase.SawWin && vk == NativeMethods.VkLshift)
        {
            _phase = Phase.SawWinShift;
            RestartTimer();
            return true;
        }

        if (_phase == Phase.SawWinShift && vk == NativeMethods.VkF23)
        {
            _replayTimer.Stop();
            _phase = Phase.ConsumeChord;
            RaiseCopilot();
            return true;
        }

        if (_phase is Phase.SawWin or Phase.SawWinShift)
        {
            _replayTimer.Stop();
            ReplaySuppressedKeys();
            return false;
        }

        return false;
    }

    private bool HandleKeyUp(int vk)
    {
        if (_phase == Phase.ConsumeChord)
        {
            if (vk is NativeMethods.VkF23 or NativeMethods.VkLshift)
            {
                return true;
            }

            if (vk == NativeMethods.VkLwin)
            {
                _phase = Phase.Idle;
                return true;
            }
        }

        if (_phase == Phase.SawWin && vk == NativeMethods.VkLwin)
        {
            _replayTimer.Stop();
            SendKey(NativeMethods.VkLwin, down: true, extended: true);
            SendKey(NativeMethods.VkLwin, down: false, extended: true);
            _phase = Phase.Idle;
            return true;
        }

        if (_phase == Phase.SawWinShift && vk is NativeMethods.VkLshift or NativeMethods.VkLwin)
        {
            _replayTimer.Stop();
            ReplaySuppressedKeys();
            return false;
        }

        return false;
    }

    private void RestartTimer()
    {
        _replayTimer.Stop();
        _replayTimer.Start();
    }

    private static void SendKey(int vk, bool down, bool extended)
    {
        var flags = 0u;
        if (!down)
        {
            flags |= NativeMethods.KeyeventfKeyup;
        }

        if (extended)
        {
            flags |= NativeMethods.KeyeventfExtendedkey;
        }

        var inputs = new[]
        {
            new NativeMethods.Input
            {
                Type = NativeMethods.InputKeyboard,
                Data = new NativeMethods.InputUnion
                {
                    Ki = new NativeMethods.KeyboardInput
                    {
                        Vk = (ushort)vk,
                        Scan = 0,
                        Flags = flags,
                        Time = 0,
                        ExtraInfo = UIntPtr.Zero
                    }
                }
            }
        };

        _ = NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.Input>());
    }

    private sealed class MessageWindow : System.Windows.Forms.NativeWindow, IDisposable
    {
        private readonly Action _onHotkey;

        public MessageWindow(Action onHotkey)
        {
            _onHotkey = onHotkey;
            CreateHandle(new System.Windows.Forms.CreateParams
            {
                Caption = "BetterCopilotButton.Hotkey",
                Parent = new IntPtr(NativeMethods.HwndMessage)
            });
        }

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if (m.Msg == NativeMethods.WmHotkey)
            {
                _onHotkey();
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            DestroyHandle();
        }
    }
}
