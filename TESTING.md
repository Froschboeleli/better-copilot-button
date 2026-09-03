# Testing Better Copilot Button

You need a Windows 10/11 x64 PC. A Copilot hardware key is required for the key test. The rest of the app can be checked on any Windows PC.

## 1. Install and open settings

1. Unzip `BetterCopilotButton-win-x64.zip` (from a Release or `.\scripts\build.ps1`).
2. Run `BetterCopilotButton.exe`.
3. Confirm the settings window opens and **Calculator** is selected as the default.

## 2. Target app and Test launch

1. Click **Test launch**. Windows Calculator should open.
2. Click **Test launch** again. Calculator should come to the front (focus), not spawn a pile of extra windows.
3. Select **Notepad**, then Test launch.
4. Select **Custom app**, **Browse** to `C:\Windows\System32\notepad.exe`, Test launch.
5. Confirm the path is shown under the presets.

## 3. Enable remap (Copilot key)

1. Select **Calculator** again.
2. Check **Enable**.
3. Confirm the status text says the Copilot key opens Calculator.
4. Close the settings window. A tray balloon should say the app is still running.
5. Press the **Copilot** key.
6. Expected: Calculator opens or focuses. Copilot should not open.

If Copilot still opens, see [Limitations](README.md#limitations). Use another remapper or OEM software as a comparison. You can also confirm the chord with PowerToys Keyboard Manager: the Copilot key should appear as Win+Shift+F23.

## 4. Disable remap

1. Tray icon > **Disable remap**, or uncheck **Enable**.
2. Press the Copilot key.
3. Expected: Windows default Copilot (or Search) behavior is back. This app does not launch Calculator.

## 5. Start at login

1. Check **Start at login**.
2. Sign out and back in, or reboot.
3. Confirm a tray icon appears without a settings window (`--tray`).
4. Press the Copilot key if remap is still enabled.

Registry check (optional): `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `BetterCopilotButton`.

## 6. Settings persist

1. Pick Notepad, enable remap, enable start at login.
2. Quit from the tray, start the exe again.
3. Expected: the same choices are restored from `%AppData%\BetterCopilotButton\settings.json`.

## 7. Uninstall restores the key

1. Click **Uninstall** and confirm.
2. Confirm the process exits, the tray icon is gone, and Start menu / login items are removed.
3. Press the Copilot key. Expected: normal Windows Copilot behavior (the hook is gone).

Alternatively run `uninstall.ps1` from the zip.

## 8. Real Win and Shift keys

With remap enabled:

1. Tap the **Windows** key. Start menu should still open.
2. Press **Win+E**. Explorer should open.
3. Press **Win+Shift+S** (Snipping Tool) if your edition has it.

The interceptor only swallows the chord when F23 follows Win+Shift within about 50 ms.

## Build smoke (maintainers)

On `windows-latest` or a local Windows SDK machine:

```powershell
dotnet publish src\BetterCopilotButton\BetterCopilotButton.csproj -c Release -r win-x64 --self-contained true
```

The GitHub Action `.github/workflows/build.yml` must stay green on `main`.
