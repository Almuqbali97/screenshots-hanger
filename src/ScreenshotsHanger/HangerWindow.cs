using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ScreenshotsHanger;

internal sealed class HangerWindow : Window
{
    private readonly ScreenshotStore store;
    private readonly Settings settings;
    private readonly Action openSettings;
    private readonly Action snip;
    private readonly Canvas canvas = new() { ClipToBounds = false };
    private readonly TranslateTransform slide = new();
    private readonly DispatcherTimer timer;
    private DateTime edgeSince = DateTime.MinValue, awaySince = DateTime.MinValue, keepUntil;
    private string? edgeMonitor;
    private bool hiding, dragging, edgeArmed = true;
    private int menusOpen, page;
    private Forms.Screen? screen;
    public event Action<string>? Error;
    public HangerWindow(ScreenshotStore store, Settings settings, Action openSettings, Action snip)
    {
        this.store = store; this.settings = settings; this.openSettings = openSettings; this.snip = snip;
        Title = "Screenshots Hanger"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        Width = 1200; Height = 302; Content = canvas; canvas.RenderTransform = slide;
        SourceInitialized += (_, _) => Native.ConfigureOverlay(this);
        SizeChanged += (_, _) => Render();
        store.Changed += StoreChanged;
        MouseWheel += (_, e) => { ChangePage(e.Delta < 0 ? 1 : -1); e.Handled = true; };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Retract(); };
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        timer.Tick += Tick; timer.Start();
        Closed += (_, _) => { timer.Stop(); store.Changed -= StoreChanged; };
    }
    private void StoreChanged() { page = Math.Clamp(page, 0, MaxPage); Render(); }
    private int MaxPage => Math.Max(0, (store.Shots.Count - 1) / settings.VisibleCount);
    private void ChangePage(int delta) { page = Math.Clamp(page + delta, 0, MaxPage); Render(); keepUntil = DateTime.UtcNow.AddSeconds(1); }
    public void Refresh() { page = 0; edgeSince = DateTime.MinValue; edgeMonitor = null; edgeArmed = true; Render(); }
    public void Reveal(bool newest = false)
    {
        if (newest) page = 0;
        Native.GetCursorPos(out var point);
        screen = Forms.Screen.FromPoint(new System.Drawing.Point(point.X, point.Y));
        var bounds = screen.Bounds;
        double scale = Native.ScaleAt(bounds.Left + bounds.Width / 2, bounds.Top + 10);
        Width = bounds.Width / scale; Height = 302;
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        Native.SetWindowPos(hwnd, new IntPtr(-1), bounds.Left, bounds.Top, bounds.Width, (int)Math.Ceiling(Height * scale), 0x0010);
        hiding = false; edgeArmed = false; awaySince = DateTime.MinValue;
        keepUntil = DateTime.UtcNow.AddSeconds(newest ? 2.5 : 1);
        Render();
        if (!IsVisible)
        {
            Show();
            Native.SetWindowPos(hwnd, new IntPtr(-1), bounds.Left, bounds.Top, bounds.Width, (int)Math.Ceiling(Height * scale), 0x0010);
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-Height, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
        else slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(140)));
    }
    public void Toggle() { if (IsVisible) Retract(); else Reveal(); }
    public void Retract(bool immediate = false)
    {
        if (!IsVisible || dragging || menusOpen > 0) return;
        edgeArmed = false;
        if (immediate) { Hide(); return; }
        if (hiding) return;
        hiding = true;
        var animation = new DoubleAnimation(-Height, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
        animation.Completed += (_, _) => { if (hiding) { Hide(); hiding = false; } };
        slide.BeginAnimation(TranslateTransform.YProperty, animation);
    }
    private void Tick(object? sender, EventArgs args)
    {
        if (!Native.GetCursorPos(out var point)) return;
        var active = Forms.Screen.FromPoint(new System.Drawing.Point(point.X, point.Y));
        bool atEdge = HoverRegion.Contains(settings.RevealArea, active.Bounds, point.X, point.Y);
        if (!atEdge) { edgeArmed = true; edgeSince = DateTime.MinValue; edgeMonitor = null; }
        if (!IsVisible)
        {
            if (atEdge && edgeArmed)
            {
                if (edgeSince == DateTime.MinValue || edgeMonitor != active.DeviceName)
                {
                    edgeSince = DateTime.UtcNow;
                    edgeMonitor = active.DeviceName;
                }
                if ((DateTime.UtcNow - edgeSince).TotalMilliseconds >= settings.HoverDelayMs) Reveal();
            }
            return;
        }
        if (dragging || menusOpen > 0 || DateTime.UtcNow < keepUntil) return;
        bool inside = false;
        try
        {
            var local = PointFromScreen(new Point(point.X, point.Y));
            inside = local.X >= 0 && local.X < ActualWidth && local.Y >= 0 && local.Y < ActualHeight;
        }
        catch (InvalidOperationException) { }
        if (inside) awaySince = DateTime.MinValue;
        else
        {
            if (awaySince == DateTime.MinValue) awaySince = DateTime.UtcNow;
            if ((DateTime.UtcNow - awaySince).TotalMilliseconds > 550) Retract();
        }
    }
    private void Render()
    {
        canvas.Children.Clear();
        double width = Width;
        var shadowRope = Rope(width, Ui.Brush("#50000000"), 4); Canvas.SetTop(shadowRope, 2); canvas.Children.Add(shadowRope);
        canvas.Children.Add(Rope(width, Ui.Brush("#A2947C"), 2.6));
        var highlight = Rope(width, Ui.Brush("#E9DECA"), .8); Canvas.SetTop(highlight, -1); canvas.Children.Add(highlight);
        var visible = store.Shots.Skip(page * settings.VisibleCount).Take(settings.VisibleCount).ToArray();
        double cardWidth = Math.Min(194, (width - 76) / settings.VisibleCount - 18);
        cardWidth = Math.Max(40, cardWidth);
        if (visible.Length == 0) DrawEmpty(width);
        double total = visible.Length * (cardWidth + 18) - 18;
        for (int i = 0; i < visible.Length; i++)
        {
            double x = (width - total) / 2 + i * (cardWidth + 18);
            double position = (x + cardWidth / 2) / width;
            double ropeY = 29 + 20 * 4 * position * (1 - position);
            DrawCard(visible[i], x, ropeY, cardWidth, i);
        }
        DrawToolbar(width);
    }
    private static System.Windows.Shapes.Path Rope(double width, Brush stroke, double thickness)
    {
        var figure = new PathFigure { StartPoint = new Point(-10, 29), IsClosed = false };
        figure.Segments.Add(new BezierSegment(new Point(width * .3, 56), new Point(width * .7, 56), new Point(width + 10, 29), true));
        return new System.Windows.Shapes.Path { Data = new PathGeometry(new[] { figure }), Stroke = stroke, StrokeThickness = thickness, IsHitTestVisible = false };
    }
    private void DrawCard(Shot shot, double x, double y, double width, int index)
    {
        double tilt = new[] { -2d, 1.4, -1, 2, -.6 }[index % 5];
        double clipHeight = Math.Clamp(width * .19, 20, 36);
        var panel = new Grid { Width = width, Height = Math.Min(175, width * .78 + 28), RenderTransformOrigin = new Point(.5, 0), RenderTransform = new RotateTransform(tilt), Cursor = Cursors.Hand,
            ToolTip = $"{shot.CapturedAt:ddd, d MMM · HH:mm}\nDrag into a chat or upload box · Right-click for more" };
        var paper = new Border { Background = Ui.Brush("#FCFAF5"), CornerRadius = new CornerRadius(width < 90 ? 6 : 11), Padding = width < 90 ? new Thickness(4) : new Thickness(7, 8, 7, 5), Margin = new Thickness(0, clipHeight * .42, 0, 0),
            Effect = new DropShadowEffect { BlurRadius = 15, ShadowDepth = 5, Opacity = .28, Color = Colors.Black } };
        var layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition()); layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(width < 90 ? 0 : 23) });
        var imageFrame = new Border { Background = Ui.Brush("#EDEFE9"), CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = new Image { Source = shot.Thumbnail, Stretch = Stretch.Uniform } };
        layout.Children.Add(imageFrame);
        var caption = Ui.Text(shot.CapturedAt.ToString("HH:mm") + "  ·  PNG", 10, Ui.Brush("#758078")); caption.Visibility = width < 90 ? Visibility.Collapsed : Visibility.Visible; caption.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(caption, 1); layout.Children.Add(caption);
        paper.Child = layout; panel.Children.Add(paper);
        var clip = new Border { Width = width < 90 ? 9 : 13, Height = clipHeight, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(3), Background = new LinearGradientBrush(Ui.Brush("#EFCE98").Color, Ui.Brush("#B28E58").Color, 0), BorderBrush = Ui.Brush("#8C714A"), BorderThickness = new Thickness(.7),
            Effect = new DropShadowEffect { BlurRadius = 3, ShadowDepth = 1.5, Opacity = .3 } };
        var groove = new Border { Height = 2, Background = Ui.Brush("#9A7D52"), Margin = new Thickness(2, 0, 2, 0) }; clip.Child = groove; panel.Children.Add(clip);
        var menu = new ContextMenu();
        AddMenu(menu, "Copy image", () => Clipboard.SetDataObject(ScreenshotStore.DragData(shot), true));
        AddMenu(menu, "Open image", () => Process.Start(new ProcessStartInfo(shot.Path) { UseShellExecute = true }));
        AddMenu(menu, "Show in folder", () => Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + shot.Path + "\"") { UseShellExecute = true }));
        menu.Items.Add(new Separator()); AddMenu(menu, "Remove from hanger", () => store.Remove(shot));
        menu.Opened += (_, _) => menusOpen++; menu.Closed += (_, _) => { menusOpen = Math.Max(0, menusOpen - 1); keepUntil = DateTime.UtcNow.AddMilliseconds(500); };
        panel.ContextMenu = menu;
        Point start = default; bool pressed = false;
        panel.MouseLeftButtonDown += (_, e) => { start = e.GetPosition(panel); pressed = true; };
        panel.MouseLeftButtonUp += (_, _) => pressed = false;
        panel.MouseMove += (_, e) =>
        {
            if (!pressed || e.LeftButton != MouseButtonState.Pressed || dragging) return;
            var current = e.GetPosition(panel);
            if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            pressed = false; dragging = true;
            try { DragDrop.DoDragDrop(panel, ScreenshotStore.DragData(shot), DragDropEffects.Copy); }
            catch (Exception ex) { Error?.Invoke("Couldn't drag this screenshot: " + ex.Message); }
            finally { dragging = false; keepUntil = DateTime.UtcNow; Retract(); }
        };
        panel.MouseEnter += (_, _) => ((RotateTransform)panel.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(160)));
        panel.MouseLeave += (_, _) => { pressed = false; ((RotateTransform)panel.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(tilt, TimeSpan.FromMilliseconds(180))); };
        Canvas.SetLeft(panel, x); Canvas.SetTop(panel, y - 15); canvas.Children.Add(panel);
    }
    private void AddMenu(ContextMenu menu, string header, Action action)
    {
        var item = new MenuItem { Header = header }; item.Click += (_, _) => { try { action(); } catch (Exception e) { Error?.Invoke(e.Message); } }; menu.Items.Add(item);
    }
    private void DrawEmpty(double width)
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text("A little space for your screenshots.", 20, Ui.Ink, FontWeights.SemiBold));
        var hint = Ui.Text("Take a snip with Win + Shift + S. It will hang right here.", 13, Ui.Muted); hint.Margin = new Thickness(0, 9, 0, 14); stack.Children.Add(hint);
        var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(Ui.Button("Take your first snip  ↗", snip, true)); row.Children.Add(Ui.Button("Settings", openSettings)); stack.Children.Add(row);
        var card = new Border { Width = Math.Min(480, width - 40), Padding = new Thickness(24, 20, 24, 20), CornerRadius = new CornerRadius(16), Background = Ui.Surface, Child = stack,
            Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 7, Opacity = .2 } };
        Canvas.SetLeft(card, (width - card.Width) / 2); Canvas.SetTop(card, 62); canvas.Children.Add(card);
    }
    private void DrawToolbar(double width)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var dot = new Ellipse { Fill = Ui.Mint, Width = 7, Height = 7, Margin = new Thickness(7, 0, 9, 0) }; row.Children.Add(dot);
        var name = Ui.Text("SCREENSHOTS HANGER", 10, Ui.Ink, FontWeights.SemiBold); name.Margin = new Thickness(0, 0, 13, 0); row.Children.Add(name);
        var prev = Ui.GlassButton("‹", () => ChangePage(-1)); prev.ToolTip = "Newer screenshots"; prev.IsEnabled = page > 0; row.Children.Add(prev);
        var range = Ui.Text(store.Shots.Count == 0 ? "Ready" : $"{page * settings.VisibleCount + 1}–{Math.Min((page + 1) * settings.VisibleCount, store.Shots.Count)} / {store.Shots.Count}", 11, Ui.Brush("#E1EAEE")); range.Margin = new Thickness(8, 0, 8, 0); row.Children.Add(range);
        var next = Ui.GlassButton("›", () => ChangePage(1)); next.ToolTip = "Older screenshots · or scroll"; next.IsEnabled = page < MaxPage; row.Children.Add(next);
        row.Children.Add(Ui.GlassButton("+ Snip", snip, true));
        var clear = Ui.GlassButton("Clear", ClearHanger); clear.Name = "ClearHangerButton";
        clear.ToolTip = "Clear all screenshots from the hanger, including older pages";
        System.Windows.Automation.AutomationProperties.SetName(clear, "Clear hanger");
        clear.IsEnabled = store.Shots.Count > 0; row.Children.Add(clear);
        var preferences = Ui.GlassButton("⚙", openSettings); preferences.ToolTip = "Settings"; row.Children.Add(preferences);
        var hide = Ui.GlassButton("↑", () => Retract()); hide.ToolTip = "Hide hanger"; row.Children.Add(hide);
        var glass = Ui.GlassToolbar(row);
        glass.Measure(new Size(double.PositiveInfinity, 60)); Canvas.SetLeft(glass, Math.Max(0, (width - glass.DesiredSize.Width) / 2)); Canvas.SetTop(glass, 237); canvas.Children.Add(glass);
    }
    private void ClearHanger()
    {
        try
        {
            store.Clear();
            page = 0;
            keepUntil = DateTime.UtcNow.AddSeconds(1.5);
        }
        catch (Exception e) { Error?.Invoke("Couldn't clear the hanger: " + e.Message); }
    }
}
