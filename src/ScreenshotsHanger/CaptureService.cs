using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ScreenshotsHanger;

internal sealed class CaptureService : IDisposable
{
    private readonly Settings settings;
    private readonly ScreenshotStore store;
    private readonly HwndSource source;
    private readonly Dispatcher dispatcher;
    private readonly List<FileSystemWatcher> watchers = new();
    private readonly HashSet<string> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] screenshotDirectories;
    private uint sequence;
    private int clipboardGeneration;
    private bool disposed;
    public bool Paused { get; set; }
    public bool HotkeyRegistered { get; }
    public bool ClipboardRegistered { get; }
    public event Action? ToggleRequested;
    public event Action? Captured;
    public event Action<string>? Error;
    public CaptureService(Settings settings, ScreenshotStore store, string[]? screenshotDirectories = null)
    {
        this.settings = settings; this.store = store;
        this.screenshotDirectories = screenshotDirectories ?? new[] {
            Native.ScreenshotFolder(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots"),
            Path.Combine(Environment.GetEnvironmentVariable("OneDrive") ?? "", "Pictures", "Screenshots")
        }.Where(p => p != null && Path.IsPathRooted(p)).Select(p => p!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        dispatcher = Dispatcher.CurrentDispatcher;
        source = new HwndSource(new HwndSourceParameters("Screenshots Hanger clipboard") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0, WindowStyle = 0 });
        source.AddHook(WndProc);
        ClipboardRegistered = Native.AddClipboardFormatListener(source.Handle);
        HotkeyRegistered = Native.RegisterHotKey(source.Handle, 1, 0x0001 | 0x0008 | 0x4000, 0x48); // Win+Alt+H
        sequence = Native.GetClipboardSequenceNumber(); // don't collect pre-existing clipboard contents
        RefreshFolders();
    }
    public void RefreshFolders()
    {
        foreach (var watcher in watchers) watcher.Dispose();
        watchers.Clear();
        if (!settings.WatchScreenshotFolder) return;
        foreach (var folder in screenshotDirectories)
        {
            // Watch the parent when Windows hasn't created its screenshot directory yet.
            var watchPath = Directory.Exists(folder) ? folder : Path.GetDirectoryName(folder);
            if (watchPath == null || !Directory.Exists(watchPath)) continue;
            try
            {
                var watcher = new FileSystemWatcher(watchPath) { IncludeSubdirectories = !string.Equals(watchPath, folder, StringComparison.OrdinalIgnoreCase), NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite };
                watcher.Created += FileChanged; watcher.Changed += FileChanged; watcher.Renamed += FileRenamed;
                watcher.Error += (_, _) => dispatcher.BeginInvoke(() => Error?.Invoke("Screenshot folder monitoring was interrupted. Toggle folder capture in Settings to reconnect."));
                watcher.EnableRaisingEvents = true; watchers.Add(watcher);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Error?.Invoke(e.Message); }
        }
    }
    private void FileRenamed(object sender, RenamedEventArgs e) => FileChanged(sender, e);
    private void FileChanged(object sender, FileSystemEventArgs e)
    {
        if (!screenshotDirectories.Contains(Path.GetDirectoryName(e.FullPath), StringComparer.OrdinalIgnoreCase)) return;
        var extension = Path.GetExtension(e.FullPath).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".bmp")) return;
        dispatcher.BeginInvoke(() => ImportEventually(e.FullPath));
    }
    private async void ImportEventually(string path)
    {
        if (disposed || Paused || !settings.WatchScreenshotFolder || !pending.Add(path)) return;
        try
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                await Task.Delay(250 + attempt * 150);
                if (disposed || Paused || !settings.WatchScreenshotFolder) return;
                try
                {
                    using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)) { }
                    if (store.Import(path) != null) Captured?.Invoke();
                    return;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
                { if (attempt == 7) Error?.Invoke("Couldn't read a screenshot: " + e.Message); }
            }
        }
        finally { pending.Remove(path); }
    }
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled)
    {
        if (msg == 0x031D) { _ = ReadClipboardAsync(++clipboardGeneration); handled = true; }
        if (msg == 0x0312) { ToggleRequested?.Invoke(); handled = true; }
        return IntPtr.Zero;
    }
    private async Task ReadClipboardAsync(int generation)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            await Task.Delay(90 + attempt * 60);
            if (disposed || generation != clipboardGeneration || Paused || !settings.CaptureClipboard) return;
            var current = Native.GetClipboardSequenceNumber();
            if (current == sequence) return;
            try
            {
                if (Clipboard.ContainsImage())
                {
                    var bitmap = Clipboard.GetImage();
                    if (bitmap != null && store.Add(bitmap) != null) Captured?.Invoke();
                }
                sequence = current; return;
            }
            catch (Exception e) when (e is ExternalException or IOException or InvalidOperationException or ArgumentException or NotSupportedException)
            { if (attempt == 7) Error?.Invoke("Couldn't collect an image from the clipboard: " + e.Message); }
        }
    }
    public void Dispose()
    {
        disposed = true;
        foreach (var watcher in watchers) watcher.Dispose();
        Native.RemoveClipboardFormatListener(source.Handle);
        Native.UnregisterHotKey(source.Handle, 1);
        source.RemoveHook(WndProc); source.Dispose();
    }
}
