# Better Copilot Button

Replace the Windows Copilot hardware key with **any app you choose**.

On newer PCs and keyboards, the Copilot key opens Microsoft Copilot. This small Windows tray app lets you point that key at Calculator (the default), Notepad, Windows Terminal, File Explorer, Settings, or any `.exe` you browse to. If the target is already running, it is focused instead of launching a second copy.

No account. No ads. No telemetry. Settings stay in your AppData folder.

**Default target:** Windows Calculator (`calc.exe`).

![Settings window](docs/images/settings.svg)

<p align="center"><em>Settings window (illustration). The real app matches this layout.</em></p>

## Install in about a minute

You do not need Visual Studio.

1. Download **BetterCopilotButton-win-x64.zip** from the [latest Release](https://github.com/Froschboeleli/better-copilot-button/releases/latest).
2. Unzip it anywhere (Downloads is fine).
3. Double-click `BetterCopilotButton.exe`.
4. Leave **Calculator** selected, or pick another app / **Browse**.
5. Turn **Enable** on.
6. Press the Copilot key. Calculator (or your app) should open.

Optional: check **Start at login** so the remap is ready after you sign in. Optional: **Add to Start menu**, or run `install.ps1` from the unzipped folder.

Landing page: <https://froschboeleli.github.io/better-copilot-button>

If a Release is not published yet, build the zip on a Windows PC (see [Build from source](#build-from-source)) or wait for the GitHub Action on `main`.

## What you get

- Clean settings window to pick a target app
- System tray icon: Open settings, Enable/Disable remap, Quit
- Settings stored in `%AppData%\BetterCopilotButton\settings.json`
- Start at login (current user, no admin)
- Uninstall restores the Copilot key (the hook stops when the app exits)

## How the remap works

On most hardware the Copilot key does **not** send a unique virtual key. Microsoft defined it as this shortcut:

`Left Windows` + `Left Shift` + `F23`

Better Copilot Button listens for that chord with a user-level keyboard hook (and a `RegisterHotKey` backup). When the chord is confirmed, the app swallows it and launches or focuses your target. Real Win and Shift keys are replayed if F23 does not follow, so Start menu and Win+Shift shortcuts keep working.

This uses supported Win32 APIs (`SetWindowsHookEx`, `RegisterHotKey`). It does **not** write a scancode map and does **not** require Administrator.

Windows 11 also has **Settings > Bluetooth & devices > Keyboard > Customize Copilot key**. That official control only offers Copilot, Search, or a Store app. It cannot point the key at an arbitrary `.exe`. This app exists for that gap.

## Limitations

- Some laptops handle the Copilot key in **firmware** or a vendor driver. Those presses never become Win+Shift+F23, so this app cannot see them.
- A few devices only honor Microsoft's Settings remap (Copilot / Search / Store app).
- The remap is active only while Better Copilot Button is running. Use **Start at login** or keep it in the tray.
- Another remapper (PowerToys Keyboard Manager, AutoHotkey, OEM software) may grab the chord first.
- Windows 10/11 x64. Arm64 PCs are not in the published zip.

If Test launch works but the Copilot key still opens Copilot, your keyboard is likely in the firmware-limited group.

## Uninstall

The Copilot key goes back to normal as soon as this app is not running.

- In the app: **Uninstall**
- Or quit from the tray, then delete the unzipped folder
- Or run `uninstall.ps1`
- Or Windows Settings > Apps, if you used **Add to Start menu** / `install.ps1`

Uninstall removes the login item, Start menu shortcut, AppData settings, and the LocalAppData copy.

## Presets

| Preset | What launches |
| --- | --- |
| Calculator (default) | `calc.exe` |
| Notepad | `notepad.exe` |
| Windows Terminal | `wt.exe` |
| File Explorer | new Explorer window |
| Settings | Windows Settings |
| ChatGPT (optional) | desktop app if installed under LocalAppData |
| Custom | any `.exe` you browse to |

ChatGPT is just another optional preset, not the point of the product.

## Safety

- Runs as the current user (`asInvoker`). No UAC prompt for the remap.
- Does not install drivers or kernel components.
- Does not send usage data anywhere.
- Open source, MIT licensed. Read the code in `src/BetterCopilotButton`.

## Build from source

On Windows 10/11 with the [.NET 8 SDK](https://dotnet.microsoft.com/download):

```powershell
git clone https://github.com/Froschboeleli/better-copilot-button.git
cd better-copilot-button
.\scripts\build.ps1
```

The zip lands in `artifacts\BetterCopilotButton-win-x64.zip`.

Or open `BetterCopilotButton.sln` in Visual Studio 2022 and run the WPF project.

GitHub Actions builds that same zip on every push to `main` and attaches it to a GitHub Release when you push a tag such as `v1.0.0`.

## Docs site

The `/docs` folder is a static landing page for GitHub Pages. In the repo: **Settings > Pages > Deploy from a branch > `main` / `docs`**. After that, the site is <https://froschboeleli.github.io/better-copilot-button>.

## Verify it

See [TESTING.md](TESTING.md) for a short checklist (Test launch, Copilot key, disable, uninstall).

## License

[MIT](LICENSE)
