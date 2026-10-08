using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotsHanger;

internal sealed class SettingsWindow : Window
{
    public SettingsWindow(Settings settings, ScreenshotStore store, Action save, Action snip, bool hotkeyAvailable)
    {
        Title = "Screenshots Hanger · Settings"; Width = 570; Height = 760; MinWidth = 490; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = Ui.Brush("#15211D"); Foreground = Ui.Ink;
        FontFamily = new FontFamily("Segoe UI");
        var root = new StackPanel { Margin = new Thickness(30, 25, 30, 24) };
        var frame = new Grid(); frame.RowDefinitions.Add(new RowDefinition()); frame.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        frame.Children.Add(new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Content = frame;
        var eyebrow = Ui.Text("A LITTLE LESS WINDOW JUGGLING", 10, Ui.Mint, FontWeights.SemiBold); eyebrow.Margin = new Thickness(0, 0, 0, 10); root.Children.Add(eyebrow);
        root.Children.Add(Ui.Text("Screenshots Hanger", 30, Ui.Ink, FontWeights.SemiBold));
        var intro = Ui.Text("Snip it. Hang it. Drop it anywhere.", 15, Ui.Muted); intro.Margin = new Thickness(0, 6, 0, 24); root.Children.Add(intro);

        var how = new StackPanel();
        how.Children.Add(Ui.Text("Your next screenshot has a place to land.", 15, Ui.Ink, FontWeights.SemiBold));
        AddParagraph(how, "01   Take a screenshot with Win + Shift + S.\n02   Hover at your chosen area along the top edge.\n03   Drag a thumbnail into a chat, folder, or file upload box.");
        var snipButton = Ui.Button("Try a snip  ↗", () => { Close(); snip(); }, true); snipButton.HorizontalAlignment = HorizontalAlignment.Left; how.Children.Add(snipButton);
        root.Children.Add(Card(how));

        var display = new StackPanel();
        var value = Ui.Text($"{settings.VisibleCount} screenshots on the rope", 15, Ui.Ink, FontWeights.SemiBold); display.Children.Add(value);
        var slider = new Slider { Minimum = 1, Maximum = 12, TickFrequency = 1, IsSnapToTickEnabled = true, Value = settings.VisibleCount, Margin = new Thickness(0, 15, 0, 6), AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft };
        slider.ValueChanged += (_, _) => value.Text = $"{(int)slider.Value} screenshot{(slider.Value == 1 ? "" : "s")} on the rope";
        display.Children.Add(slider);
        AddParagraph(display, "Older screenshots stay available with the arrows or mouse wheel.");
        var areaLabel = Ui.Text("Reveal the hanger when hovering", 13, Ui.Ink, FontWeights.SemiBold);
        areaLabel.Margin = new Thickness(0, 12, 0, 8); display.Children.Add(areaLabel);
        var area = new ComboBox
        {
            Name = "RevealAreaSelector", MinHeight = 32, Padding = new Thickness(8, 5, 8, 5),
            ItemsSource = new[] { "Top middle (default)", "Top-left corner", "Top-right corner", "Anywhere at the top" },
            SelectedIndex = (int)settings.RevealArea
        };
        System.Windows.Automation.AutomationProperties.SetName(area, "Hanger reveal area");
        var areaHint = Ui.Text("", 12, Ui.Muted); areaHint.TextWrapping = TextWrapping.Wrap; areaHint.LineHeight = 19;
        areaHint.Margin = new Thickness(0, 7, 0, 10);
        var descriptions = new[] {
            "The middle half of the top edge. Both corners stay free for app controls.",
            "The leftmost 10% of the top edge on each monitor.",
            "The rightmost 10% of the top edge on each monitor.",
            "The entire top edge, including both corners."
        };
        areaHint.Text = descriptions[area.SelectedIndex];
        area.SelectionChanged += (_, _) => { if (area.SelectedIndex >= 0) areaHint.Text = descriptions[area.SelectedIndex]; };
        display.Children.Add(area); display.Children.Add(areaHint);
        var delayRow = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        var delayLabel = Ui.Text("Top-edge reveal delay", 13, Ui.Muted); delayRow.Children.Add(delayLabel);
        var delay = new ComboBox { Width = 125, HorizontalAlignment = HorizontalAlignment.Right, ItemsSource = new[] { "Quick · 100 ms", "Normal · 220 ms", "Relaxed · 500 ms" }, SelectedIndex = settings.HoverDelayMs <= 100 ? 0 : settings.HoverDelayMs >= 500 ? 2 : 1 };
        delayRow.Children.Add(delay); display.Children.Add(delayRow); root.Children.Add(Card(display));

        var capture = new StackPanel();
        capture.Children.Add(Ui.Text("Make it yours", 15, Ui.Ink, FontWeights.SemiBold));
        var clipboard = Check(capture, "Collect clipboard images", "Includes Snipping Tool, Print Screen, and other copied images.", settings.CaptureClipboard);
        var folder = Check(capture, "Watch the Screenshots folder", "Also catches Win + Print Screen in Pictures / Screenshots.", settings.WatchScreenshotFolder);
        var reveal = Check(capture, "Show the rope after a capture", "A quick peek when a new screenshot arrives.", settings.RevealOnCapture);
        var startup = Check(capture, "Start when I sign in to Windows", "Keep the hanger ready in the system tray.", StartupRegistration.Enabled);
        root.Children.Add(Card(capture));

        var storage = new StackPanel();
        storage.Children.Add(Ui.Text("On your computer. Always.", 15, Ui.Ink, FontWeights.SemiBold));
        AddParagraph(storage, $"Keeps the latest {ScreenshotStore.HistoryLimit} images locally. No account, uploads, or analytics. Removed images are cleaned up after 24 hours when the app is active.");
        var actions = new WrapPanel();
        actions.Children.Add(Ui.Button("Open image folder", () => Process.Start(new ProcessStartInfo(store.DirectoryPath) { UseShellExecute = true })));
        actions.Children.Add(Ui.Button("Add images…", () =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff", Multiselect = true };
            if (dialog.ShowDialog(this) != true) return;
            foreach (var file in dialog.FileNames) try { store.Import(file); } catch (Exception e) { MessageBox.Show(this, e.Message, "Couldn't add image"); }
        }));
        actions.Children.Add(Ui.Button("Clear hanger", () =>
        {
            if (MessageBox.Show(this, "Remove all screenshots from the hanger? Original screenshots outside the app's image folder are kept.", "Clear hanger", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                try { store.Clear(); } catch (Exception e) { MessageBox.Show(this, e.Message, "Couldn't clear hanger"); }
        }));
        storage.Children.Add(actions); root.Children.Add(Card(storage));

        var footer = Ui.Text(hotkeyAvailable ? "Win + Alt + H  to toggle  ·  Right-click a thumbnail for more" : "Use the tray icon to toggle · Win + Alt + H is used by another app", 11, Ui.Muted);
        footer.Margin = new Thickness(0, 4, 0, 12); root.Children.Add(footer);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Ui.Button("Cancel", Close));
        buttons.Children.Add(Ui.Button("Save preferences", () =>
        {
            try
            {
                StartupRegistration.Set(startup.IsChecked == true);
                settings.VisibleCount = (int)slider.Value; settings.HoverDelayMs = new[] { 100, 220, 500 }[delay.SelectedIndex];
                settings.RevealArea = (HoverRevealArea)area.SelectedIndex;
                settings.CaptureClipboard = clipboard.IsChecked == true; settings.WatchScreenshotFolder = folder.IsChecked == true; settings.RevealOnCapture = reveal.IsChecked == true;
                save(); Close();
            }
            catch (Exception e) { MessageBox.Show(this, "Couldn't save preferences. " + e.Message, "Screenshots Hanger"); }
        }, true));
        var saveBar = new Border { Child = buttons, Padding = new Thickness(24, 12, 24, 16), Background = Ui.Brush("#15211D"), BorderBrush = Ui.Brush("#34433B"), BorderThickness = new Thickness(0, 1, 0, 0) };
        Grid.SetRow(saveBar, 1); frame.Children.Add(saveBar);
    }
    private static Border Card(UIElement child) => new() { Child = child, Background = Ui.Surface, CornerRadius = new CornerRadius(14), Padding = new Thickness(19, 17, 19, 17), Margin = new Thickness(0, 0, 0, 13), BorderBrush = Ui.Brush("#34433B"), BorderThickness = new Thickness(1) };
    private static void AddParagraph(Panel panel, string text)
    {
        var block = Ui.Text(text, 12, Ui.Muted); block.TextWrapping = TextWrapping.Wrap; block.LineHeight = 21; block.Margin = new Thickness(0, 8, 0, 6); panel.Children.Add(block);
    }
    private static CheckBox Check(Panel panel, string title, string description, bool selected)
    {
        var content = new StackPanel(); content.Children.Add(Ui.Text(title, 13));
        var help = Ui.Text(description, 11, Ui.Muted); help.Margin = new Thickness(0, 3, 0, 0); help.TextWrapping = TextWrapping.Wrap; content.Children.Add(help);
        var check = new CheckBox { Content = content, IsChecked = selected, Margin = new Thickness(0, 15, 0, 0), VerticalContentAlignment = VerticalAlignment.Center, Foreground = Ui.Ink };
        panel.Children.Add(check); return check;
    }
}
