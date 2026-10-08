# Screenshots Hanger

A native Windows clothesline for screenshots. Move the mouse to the top edge of a monitor to reveal the rope, then drag a screenshot into a chat, a folder, or a website that accepts file uploads.

## Install

Run `artifacts/ScreenshotsHanger-Setup-1.0.2-x64.exe`. It installs for the current user without administrator access and includes the .NET runtime. Windows 10 version 2004 or later, or Windows 11, x64. Uninstall through Windows Settings → Apps.

The installer is unsigned. Windows may show an unknown-publisher / SmartScreen prompt. Code signing requires a publisher certificate; none is included in this project.

The standalone executable is `artifacts/publish/ScreenshotsHanger.exe`. It also works without installation. Its screenshot library still lives in Local AppData.

## Use

- Launch Screenshots Hanger. The first launch opens settings; afterward it runs in the system tray.
- Take a snip with **Win + Shift + S**, use **Print Screen**, or take a full screenshot with **Win + Print Screen**.
- Hover at the **middle of the top edge** of any monitor. The rope slides down and retracts after the pointer leaves.
- Settings → **Reveal the hanger when hovering** offers **Top middle (default)**, **Top-left corner**, **Top-right corner**, and **Anywhere at the top**. The default uses the middle half of each monitor's top edge, leaving both corners free for app controls. Corner options use the leftmost or rightmost 10%. The reveal delay applies to all options. Click **Save preferences** to apply; the choice persists after restart. Existing preferences automatically adopt the new middle default on upgrade. Automatic reveal after a capture has its own separate toggle.
- Drag a thumbnail to a supported file drop target. The app offers a real PNG file plus a bitmap format. Applications decide which formats and drop locations they accept.
- Use the arrows or mouse wheel for older images. Settings lets you show **1–12** screenshots at a time; up to **100** are retained.
- Right-click a thumbnail to copy it, open it, locate its PNG, or remove it.
- Click **Clear** in the glass toolbar to immediately empty the entire hanger, including older pages. It is disabled when the hanger is empty. Original screenshots outside the app library are kept.
- **Win + Alt + H** toggles the hanger, when that shortcut is available. Left-click the tray icon for the same action. Right-click the tray icon for settings, pause, and quit.
- Optional sign-in startup is available in the installer and settings.

Clipboard capture also collects other copied images: Windows does not reliably identify whether a clipboard bitmap came from a screenshot. Pre-existing clipboard contents are ignored when the app starts. Folder capture watches Windows' configured Screenshots folder and the usual Pictures / OneDrive Pictures locations. A custom Snipping Tool save folder works through clipboard capture; it is not scanned separately.

Images and preferences are stored under `%LOCALAPPDATA%\ScreenshotsHanger`. No network requests, accounts, or telemetry are used by the app. Removing an image hides it immediately and preserves the app's PNG for 24 hours so an in-progress file drop can finish. Expired files are removed at startup, on subsequent captures, and hourly while running. Your original screenshots are never deleted. Uninstall preserves your library and preferences; remove that Local AppData folder yourself if you want to erase them.

## Build

Stack: C# / .NET 10, WPF, Win32 clipboard notifications and window APIs, Windows OLE drag-and-drop, and a WinForms tray icon. Inno Setup packages the self-contained x64 executable. No third-party runtime NuGet packages.

Install the .NET 10 SDK and Inno Setup 6, or put their tools in `.tools/dotnet` and `.tools/inno`. Then run:

```powershell
./scripts/build.ps1
```

If PowerShell blocks scripts, use `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1` from the project root. The bypass applies only to that process.

The toolbar uses live alpha transparency, layered reflections, translucent buttons, and a pointer-following highlight. It does not capture the desktop or apply native backdrop blur.

Use `-SkipInstaller` to publish and test only. The script generates the icon, publishes the executable, runs the smoke suite, and compiles the installer. Runtime packs may need downloading on the first build. The compiler's current licensing terms apply to commercial builds.

## Verification

```powershell
./artifacts/publish/ScreenshotsHanger.exe --smoke-test ./artifacts/qa
```

The test uses isolated data inside the supplied directory and leaves your clipboard and screenshot library untouched. `result.txt` reports the result. PNG renders show the five-item rope, twelve-item rope, narrow layout, empty state, and settings.

Automated coverage: settings bounds/save/load/corrupt JSON recovery; PNG save/reload; pixel deduplication; real file-drop payloads; removal persistence and delayed cleanup; the 100-image cap; Windows clipboard-listener registration; real filesystem notifications, file-lock retries, deduplication, and pause behavior; WPF rendering; the toolbar Clear button across multiple pages, empty-state disabling, retained PNG files, and cleared-history persistence after restart. Glass previews are rendered on light and dark sample backgrounds.

Reveal-region coverage includes all four options, exact horizontal/vertical boundaries, negative and offset monitor coordinates, different monitor sizes, preference persistence, legacy-settings migration, and invalid-value fallback. Physical mixed-DPI monitor testing remains a manual check.

Installer verification is available with `scripts/test-installer.ps1`: silent installation into a project test folder, SHA256 equality of the installed and published executable, Windows uninstall registration, and clean uninstallation. This test refuses to run over an existing registered installation.

Manual checks still needed on the destination machine: actual clipboard capture from each screenshot tool, drag-and-drop into the particular chat/browser apps you use, mixed-DPI multiple monitors, fullscreen apps, and autostart after sign-in. Dragging into an elevated application may be blocked by Windows integrity-level rules. The app should normally run without elevation.

An optional local browser drop target is in `tests/drop-target.html`. It displays the received file's name, type, size, and preview without uploading anything.

## Implementation references

- [Windows shell file-drop format](https://learn.microsoft.com/en-us/dotnet/api/system.windows.dataformats.filedrop?view=windowsdesktop-10.0)
- [Windows known folder identifiers](https://learn.microsoft.com/en-us/windows/win32/shell/knownfolderid)
- [Inno Setup](https://jrsoftware.org/isdl.php)
