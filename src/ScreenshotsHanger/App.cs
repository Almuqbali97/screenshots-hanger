using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ScreenshotsHanger;

internal sealed class App : Application
{
    private readonly string dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenshotsHanger");
    private Settings settings = null!;
    private ScreenshotStore store = null!;
    private HangerWindow hanger = null!;
    private CaptureService capture = null!;
    private SettingsWindow? settingsWindow;
    private Forms.NotifyIcon? tray;
    private EventWaitHandle? showEvent;
    private RegisteredWaitHandle? showWait;
    private DateTime lastError;
    private DispatcherTimer? cleanup;
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--smoke-test")) return SmokeTest.Run(args);
        using var mutex = new Mutex(true, @"Local\ScreenshotsHanger.App.v1", out bool first);
        if (!first)
        {
            try { using var signal = EventWaitHandle.OpenExisting(@"Local\ScreenshotsHanger.Show.v1"); signal.Set(); } catch (WaitHandleCannotBeOpenedException) { }
            return 0;
        }
        try { return new App().Run(); }
        catch (Exception e) { MessageBox.Show("Screenshots Hanger couldn't start.\n\n" + e.Message, "Screenshots Hanger", MessageBoxButton.OK, MessageBoxImage.Error); return 1; }
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Directory.CreateDirectory(dataRoot);
        settings = Settings.Load(Path.Combine(dataRoot, "settings.json"));
        store = new ScreenshotStore(Path.Combine(dataRoot, "Images"));
        cleanup = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
        cleanup.Tick += (_, _) => { try { store.CleanupRemoved(); } catch (Exception ex) { Report(ex.Message); } }; cleanup.Start();
        hanger = new HangerWindow(store, settings, OpenSettings, StartSnip); MainWindow = hanger;
        capture = new CaptureService(settings, store);
        hanger.Error += Report; capture.Error += Report;
        capture.ToggleRequested += hanger.Toggle;
        capture.Captured += () => { if (settings.RevealOnCapture && settingsWindow == null) hanger.Reveal(true); };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show hanger   Win+Alt+H", null, (_, _) => hanger.Reveal());
        menu.Items.Add("Take a snip   Win+Shift+S", null, (_, _) => StartSnip());
        menu.Items.Add("Settings", null, (_, _) => OpenSettings());
        var pause = new Forms.ToolStripMenuItem("Pause capture") { CheckOnClick = true };
        pause.CheckedChanged += (_, _) => { capture.Paused = pause.Checked; if (tray != null) tray.Text = pause.Checked ? "Screenshots Hanger · capture paused" : "Screenshots Hanger"; };
        menu.Items.Add(pause); menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("Quit Screenshots Hanger", null, (_, _) => Shutdown());
        tray = new Forms.NotifyIcon { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application, Text = "Screenshots Hanger", ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, args) => { if (args.Button == Forms.MouseButtons.Left) hanger.Toggle(); };
        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ScreenshotsHanger.Show.v1");
        showWait = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => Dispatcher.BeginInvoke(() => hanger.Reveal()), null, Timeout.Infinite, false);
        if (!capture.ClipboardRegistered) Report("Windows couldn't enable clipboard monitoring. Restart the app to try again.");
        DispatcherUnhandledException += (_, args) => { Report(args.Exception.Message); args.Handled = true; };
        if (!settings.WelcomeSeen && !e.Args.Contains("--background"))
        {
            settings.WelcomeSeen = true; settings.Save(Path.Combine(dataRoot, "settings.json")); OpenSettings();
        }
    }
    private void OpenSettings()
    {
        hanger.Retract(true);
        if (settingsWindow != null) { settingsWindow.Activate(); return; }
        settingsWindow = new SettingsWindow(settings, store, () =>
        {
            settings.Save(Path.Combine(dataRoot, "settings.json")); capture.RefreshFolders(); hanger.Refresh();
        }, StartSnip, capture.HotkeyRegistered);
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show(); settingsWindow.Activate();
    }
    private async void StartSnip()
    {
        hanger.Retract(true);
        await Task.Delay(180);
        try { Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true }); }
        catch (Exception e) { Report("Use Win + Shift + S to take a screenshot. " + e.Message); }
    }
    private void Report(string message)
    {
        try { File.AppendAllText(Path.Combine(dataRoot, "app.log"), $"{DateTime.Now:O} {message}\n"); } catch (IOException) { }
        if ((DateTime.UtcNow - lastError).TotalSeconds < 10) return;
        lastError = DateTime.UtcNow;
        tray?.ShowBalloonTip(4000, "Screenshots Hanger", message, Forms.ToolTipIcon.Warning);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        cleanup?.Stop(); showWait?.Unregister(null); showEvent?.Dispose(); capture?.Dispose();
        if (tray != null) { tray.Visible = false; tray.Dispose(); }
        base.OnExit(e);
    }
}
