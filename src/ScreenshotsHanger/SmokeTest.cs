using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenshotsHanger;

internal static class SmokeTest
{
    // Runs in an isolated data directory. Never reads/writes the user's clipboard or library.
    public static int Run(string[] args)
    {
        string output = Path.GetFullPath(args.SkipWhile(a => a != "--smoke-test").Skip(1).FirstOrDefault() ?? "artifacts/qa");
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int exit = 0;
        app.Startup += async (_, _) =>
        {
            try { await Verify(output); File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: settings validation/persistence/recovery; reveal area defaults, legacy migration, all four choices, boundaries and monitor offsets; PNG storage/reload; pixel deduplication; drag file payload; removal/reload/cleanup; history limit; native clipboard listener; actual file watcher capture/retry/pause; overlay (5/12/narrow) and settings rendering; glass toolbar on light/dark backgrounds; toolbar clear across pages, one notification, retained PNGs, restart persistence, disabled empty state.\n"); }
            catch (Exception e) { exit = 1; File.WriteAllText(Path.Combine(output, "result.txt"), e.ToString()); }
            finally { app.Shutdown(exit); }
        };
        app.Run(); return exit;
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Verify(string output)
    {
        var run = Path.Combine(output, "test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(run);
        var config = Path.Combine(run, "settings.json");
        var settings = new Settings { VisibleCount = 99, HoverDelayMs = -1 };
        settings.Save(config); settings = Settings.Load(config);
        Assert(settings.VisibleCount == 12 && settings.HoverDelayMs == 100, "Settings bounds failed");
        File.WriteAllText(config, "{not valid json"); Assert(Settings.Load(config).VisibleCount == 5, "Settings recovery failed");
        File.WriteAllText(config, "{\"VisibleCount\":7,\"HoverDelayMs\":500}");
        var legacy = Settings.Load(config);
        Assert(legacy.RevealArea == HoverRevealArea.TopMiddle && legacy.VisibleCount == 7 && legacy.HoverDelayMs == 500, "Legacy settings didn't adopt top-middle while retaining preferences");
        foreach (var choice in Enum.GetValues<HoverRevealArea>())
        {
            legacy.RevealArea = choice; legacy.Save(config);
            Assert(Settings.Load(config).RevealArea == choice, "Reveal area wasn't persisted: " + choice);
        }
        legacy.RevealArea = (HoverRevealArea)99; legacy.Validate();
        Assert(legacy.RevealArea == HoverRevealArea.TopMiddle, "Invalid reveal area didn't fall back to top-middle");
        VerifyRevealRegions();
        settings.VisibleCount = 5; settings.CaptureClipboard = false; settings.WatchScreenshotFolder = false;
        var store = new ScreenshotStore(Path.Combine(run, "Images"));
        var sample = Sample(0); var first = store.Add(sample)!;
        Assert(first != null && File.Exists(first.Path), "PNG wasn't saved");
        Assert(store.Add(sample) == null && store.Shots.Count == 1, "Duplicate image wasn't filtered");
        var drag = ScreenshotStore.DragData(first!);
        Assert(drag.GetDataPresent(DataFormats.FileDrop), "File-drop format missing");
        Assert(((string[])drag.GetData(DataFormats.FileDrop)!)[0] == first!.Path, "File-drop path mismatch");
        Assert(drag.GetDataPresent(DataFormats.Bitmap), "Bitmap fallback missing");
        var reloaded = new ScreenshotStore(store.DirectoryPath);
        Assert(reloaded.Shots.Count == 1 && reloaded.Shots[0].Hash == first.Hash, "Image reload changed pixels");
        reloaded.Remove(reloaded.Shots[0]);
        Assert(File.Exists(first.Path), "Removed drag source wasn't preserved for delayed drop consumers");
        Assert(new ScreenshotStore(store.DirectoryPath).Shots.Count == 0, "Removed image reappeared on restart");
        File.SetLastWriteTimeUtc(first.Path + ".removed", DateTime.UtcNow.AddDays(-2)); reloaded.CleanupRemoved();
        Assert(!File.Exists(first.Path) && !File.Exists(first.Path + ".removed"), "Expired image wasn't cleaned up");
        // Use tiny distinct images to verify retention without a large test data footprint.
        var history = new ScreenshotStore(Path.Combine(run, "Retention"));
        for (int i = 0; i < 102; i++) history.Add(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { (byte)i, 20, 30, 255 }, 4));
        Assert(history.Shots.Count == 100 && new ScreenshotStore(history.DirectoryPath).Shots.Count == 100, "History cap failed");
        var gallery = new ScreenshotStore(Path.Combine(run, "Gallery"));
        for (int i = 0; i < 5; i++) gallery.Add(Sample(i));
        var inbox = Path.Combine(run, "Incoming"); Directory.CreateDirectory(inbox);
        var collected = new ScreenshotStore(Path.Combine(run, "Collected"));
        settings.WatchScreenshotFolder = true;
        using var capture = new CaptureService(settings, collected, new[] { inbox });
        Assert(capture.ClipboardRegistered, "Windows clipboard listener registration failed");
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(sample));
        using (var lockedFile = File.Open(Path.Combine(inbox, "incoming.png"), FileMode.Create, FileAccess.Write, FileShare.None))
        { png.Save(lockedFile); await Task.Delay(600); }
        for (int retry = 0; retry < 30 && collected.Shots.Count == 0; retry++) await Task.Delay(100);
        Assert(collected.Shots.Count == 1, "File watcher failed to capture image after write lock released");
        File.Copy(Path.Combine(inbox, "incoming.png"), Path.Combine(inbox, "duplicate.png")); await Task.Delay(700);
        Assert(collected.Shots.Count == 1, "Watcher duplicate wasn't filtered");
        capture.Paused = true;
        var pausedPng = new PngBitmapEncoder(); pausedPng.Frames.Add(BitmapFrame.Create(Sample(1)));
        using (var file = File.Create(Path.Combine(inbox, "paused.png"))) pausedPng.Save(file);
        await Task.Delay(700); Assert(collected.Shots.Count == 1, "Paused capture imported an image");
        var hanger = new HangerWindow(gallery, settings, () => { }, () => { });
        hanger.Reveal(); await Task.Delay(400);
        Render(hanger, Path.Combine(output, "hanger.png"));
        var glass = FindElement<Grid>(hanger, "GlassToolbarSurface")!;
        RenderGlass(hanger, glass, Path.Combine(output, "toolbar-light.png"), true);
        RenderGlass(hanger, glass, Path.Combine(output, "toolbar-dark.png"), false);
        hanger.Hide();
        for (int i = 0; i < 7; i++) gallery.Add(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { (byte)(80 + i * 10), 170, 140, 255 }, 4));
        settings.VisibleCount = 12; hanger.Refresh(); hanger.Reveal(); await Task.Delay(350);
        Render(hanger, Path.Combine(output, "hanger-12.png"));
        hanger.Width = 800; hanger.UpdateLayout(); Render(hanger, Path.Combine(output, "hanger-narrow.png")); hanger.Close();
        settings.VisibleCount = 5;
        var preferences = new SettingsWindow(settings, gallery, () => { }, () => { }, capture.HotkeyRegistered);
        preferences.Show(); await Task.Delay(150);
        var areaSelector = FindElement<ComboBox>(preferences, "RevealAreaSelector")!;
        Assert(areaSelector.Items.Count == 4 && areaSelector.SelectedIndex == 0, "Settings must expose four reveal areas and select top-middle by default");
        areaSelector.SelectedIndex = 2;
        Assert(settings.RevealArea == HoverRevealArea.TopMiddle, "Changing an unsaved reveal choice mutated settings");
        areaSelector.SelectedIndex = 0;
        Render(preferences, Path.Combine(output, "settings.png")); preferences.Close();
        var empty = new HangerWindow(gallery, settings, () => { }, () => { });
        empty.Reveal(); await Task.Delay(350);
        var paths = gallery.Shots.Select(s => s.Path).ToArray();
        int notifications = 0; gallery.Changed += () => notifications++;
        var clear = FindElement<Button>(empty, "ClearHangerButton")!;
        Assert(clear.IsEnabled && paths.Length > settings.VisibleCount, "Clear test needs screenshots on older pages");
        clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(gallery.Shots.Count == 0 && notifications == 1, "Toolbar clear didn't empty every page with a single refresh");
        Assert(paths.All(File.Exists), "Quick clear deleted PNGs before the file-drop grace period");
        Assert(new ScreenshotStore(gallery.DirectoryPath).Shots.Count == 0, "Cleared screenshots reappeared on restart");
        Assert(FindElement<Button>(empty, "ClearHangerButton")?.IsEnabled == false, "Clear wasn't disabled on an empty hanger");
        Render(empty, Path.Combine(output, "empty.png")); empty.Close();
    }
    private static void VerifyRevealRegions()
    {
        var primary = new System.Drawing.Rectangle(0, 0, 2000, 1200);
        var points = new[] { 0, 199, 200, 499, 500, 1000, 1499, 1500, 1799, 1800, 1999 };
        var expected = new[] {
            new[] { false, false, false, false, true, true, true, false, false, false, false },
            new[] { true, true, false, false, false, false, false, false, false, false, false },
            new[] { false, false, false, false, false, false, false, false, false, true, true },
            new[] { true, true, true, true, true, true, true, true, true, true, true }
        };
        foreach (var choice in Enum.GetValues<HoverRevealArea>())
        {
            for (int i = 0; i < points.Length; i++)
                Assert(HoverRegion.Contains(choice, primary, points[i], 0) == expected[(int)choice][i], $"Wrong {choice} boundary at {points[i]}");
            foreach (var outside in new[] { (-1, 0), (2000, 0), (1000, -1), (1000, 3), (0, 1200) })
                Assert(!HoverRegion.Contains(choice, primary, outside.Item1, outside.Item2), "Activated outside monitor top edge");
        }
        foreach (var monitor in new[] {
            new System.Drawing.Rectangle(-3840, -2160, 3840, 2160),
            new System.Drawing.Rectangle(2000, 0, 2560, 1440),
            new System.Drawing.Rectangle(0, 1200, 1080, 1920)
        })
        {
            Assert(HoverRegion.Contains(HoverRevealArea.TopMiddle, monitor, monitor.Left + monitor.Width / 2, monitor.Top + 2), "Middle failed with monitor offset/DPI-sized bounds");
            Assert(!HoverRegion.Contains(HoverRevealArea.TopMiddle, monitor, monitor.Left, monitor.Top), "Middle activated the left corner");
            Assert(!HoverRegion.Contains(HoverRevealArea.TopMiddle, monitor, monitor.Right - 1, monitor.Top), "Middle activated the right corner");
            Assert(HoverRegion.Contains(HoverRevealArea.TopLeftCorner, monitor, monitor.Left, monitor.Top), "Left corner failed with monitor offset");
            Assert(HoverRegion.Contains(HoverRevealArea.TopRightCorner, monitor, monitor.Right - 1, monitor.Top), "Right corner failed with monitor offset");
            Assert(!HoverRegion.Contains(HoverRevealArea.TopLeftCorner, monitor, monitor.Left + monitor.Width / 2, monitor.Top), "Left corner activated the middle");
            Assert(!HoverRegion.Contains(HoverRevealArea.TopRightCorner, monitor, monitor.Left + monitor.Width / 2, monitor.Top), "Right corner activated the middle");
        }
    }
    private static T? FindElement<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        if (parent is T element && element.Name == name) return element;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var found = FindElement<T>(VisualTreeHelper.GetChild(parent, i), name);
            if (found != null) return found;
        }
        return null;
    }
    private static void RenderGlass(Window window, FrameworkElement glass, string path, bool light)
    {
        window.UpdateLayout();
        var overlay = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        overlay.Render(window);
        var position = glass.TranslatePoint(new Point(0, 0), window);
        var crop = new CroppedBitmap(overlay, new Int32Rect((int)position.X, (int)position.Y, (int)Math.Ceiling(glass.ActualWidth), (int)Math.Ceiling(glass.ActualHeight)));
        int width = (int)Math.Ceiling(glass.ActualWidth) + 64;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Ui.Brush(light ? "#DEE6ED" : "#122431"), null, new Rect(0, 0, width, 130));
            dc.DrawEllipse(Ui.Brush(light ? "#BDA2CC" : "#386B8A"), null, new Point(width * .3, 25), 125, 90);
            dc.DrawEllipse(Ui.Brush(light ? "#9FC7BB" : "#285E50"), null, new Point(width * .77, 125), 175, 95);
            dc.DrawImage(crop, new Rect(32, 39, glass.ActualWidth, glass.ActualHeight));
        }
        var bitmap = new RenderTargetBitmap(width, 130, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); encoder.Save(file);
    }
    private static void Render(Window window, string path)
    {
        window.UpdateLayout();
        var target = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); target.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target)); using var file = File.Create(path); encoder.Save(file);
    }
    private static BitmapSource Sample(int index)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var backgrounds = new[] { "#F5F8F0", "#FFF4E5", "#E9E8FF", "#EAF4F6", "#242F2A" };
            var ink = index == 4 ? Brushes.White : Ui.Brush("#253B31");
            dc.DrawRectangle(Ui.Brush(backgrounds[index]), null, new Rect(0, 0, 700, 430));
            DrawText(dc, "SAMPLE  /  " + new[] { "ANALYTICS", "DESIGN NOTES", "INSPIRATION", "PROJECT PLAN", "IDEAS" }[index], 31, 30, 15, ink);
            DrawText(dc, new[] { "A good week.", "Make room for ideas.", "Something worth keeping.", "The next small thing.", "Less juggling. More doing." }[index], 30, 75, 35, ink);
            if (index is 0 or 3)
            {
                DrawText(dc, index == 0 ? "$48.2K" : "84% complete", 32, 131, 29, ink);
                var pen = new Pen(Ui.Brush("#47A879"), 6);
                var points = new[] { new Point(40, 349), new Point(145, 315), new Point(250, 328), new Point(355, 257), new Point(460, 268), new Point(565, 194), new Point(650, 177) };
                for (int i = 1; i < points.Length; i++) dc.DrawLine(pen, points[i - 1], points[i]);
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    dc.DrawRoundedRectangle(Ui.Brush(new[] { "#DBBB81", "#AFC5B6", "#B8A7D9" }[i]), null, new Rect(33 + i * 220, 172, 195, 173), 17, 17);
                    DrawText(dc, new[] { "Collect", "Connect", "Create" }[i], 55 + i * 220, 285, 22, Ui.Brush("#24392D"));
                }
            }
            DrawText(dc, "Screenshots Hanger   ·   Preview image", 32, 393, 13, ink);
        }
        var bitmap = new RenderTargetBitmap(700, 430, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    private static void DrawText(DrawingContext dc, string value, double x, double y, double size, Brush brush) => dc.DrawText(new FormattedText(value, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, 1), new Point(x, y));
}
