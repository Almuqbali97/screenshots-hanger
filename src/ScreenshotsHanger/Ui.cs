using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotsHanger;

internal static class Ui
{
    public static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    public static readonly Brush Ink = Brush("#F1F4F2"), Muted = Brush("#9EAFA8"), Mint = Brush("#A8E6C5"), Surface = Brush("#202C28");
    public static TextBlock Text(string text, double size = 14, Brush? color = null, FontWeight? weight = null) => new()
    { Text = text, FontSize = size, Foreground = color ?? Ink, FontWeight = weight ?? FontWeights.Normal, FontFamily = new FontFamily("Segoe UI"), VerticalAlignment = VerticalAlignment.Center };
    public static Button Button(string label, Action action, bool accent = false)
    {
        var button = new Button { Content = label, Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(3), Cursor = System.Windows.Input.Cursors.Hand,
            Foreground = accent ? Brush("#15291F") : Ink, Background = accent ? Mint : Brush("#34443D"), BorderThickness = new Thickness(0), FontSize = 13, FontWeight = FontWeights.SemiBold };
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        border.AppendChild(content);
        button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        button.Click += (_, _) => action();
        button.MouseEnter += (_, _) => button.Opacity = .83;
        button.MouseLeave += (_, _) => button.Opacity = 1;
        button.IsEnabledChanged += (_, _) => button.Opacity = button.IsEnabled ? 1 : .35;
        return button;
    }

    public static Button GlassButton(string label, Action action, bool accent = false)
    {
        var button = new Button
        {
            Content = label, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(3),
            Cursor = System.Windows.Input.Cursors.Hand, Foreground = Ink,
            FontSize = 13, FontWeight = FontWeights.SemiBold, MinWidth = 32
        };
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush(accent ? "#5377DDB0" : "#12FFFFFF")));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush(accent ? "#7899E6C3" : "#30FFFFFF")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(.7)));
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, Brush(accent ? "#8077DDB0" : "#35FFFFFF")));
        hover.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#86FFFFFF")));
        style.Triggers.Add(hover);
        var pressed = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#507AA596")));
        style.Triggers.Add(pressed);
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Control.BorderBrushProperty, Mint)); style.Triggers.Add(focus);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .3)); style.Triggers.Add(disabled);
        button.Style = style;
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
        foreach (var property in new[] { Border.BackgroundProperty, Border.BorderBrushProperty, Border.BorderThicknessProperty, Border.PaddingProperty })
            border.SetBinding(property, new System.Windows.Data.Binding(property.Name) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        button.Click += (_, _) => action();
        return button;
    }

    // Alpha-blended glass: the desktop stays live beneath the layered WPF window.
    public static Grid GlassToolbar(UIElement content)
    {
        var glass = new Grid { Name = "GlassToolbarSurface" };
        var tint = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(.25, 1),
            GradientStops = new GradientStopCollection {
                new GradientStop(Brush("#AA56656E").Color, 0),
                new GradientStop(Brush("#85344149").Color, .48),
                new GradientStop(Brush("#AC25363E").Color, 1)
            }
        };
        var rim = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(.2, 1),
            GradientStops = new GradientStopCollection {
                new GradientStop(Brush("#CCFFFFFF").Color, 0),
                new GradientStop(Brush("#28FFFFFF").Color, .45),
                new GradientStop(Brush("#65D9F1FF").Color, 1)
            }
        };
        glass.Children.Add(new Border { Background = tint, BorderBrush = rim, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(23),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 20, ShadowDepth = 5, Opacity = .24, Color = Colors.Black } });
        var reflection = new LinearGradientBrush(Brush("#25FFFFFF").Color, Colors.Transparent, 90);
        glass.Children.Add(new Border { Background = reflection, CornerRadius = new CornerRadius(22, 22, 8, 8), Height = 23, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(1), IsHitTestVisible = false });
        var light = new RadialGradientBrush(Brush("#30FFFFFF").Color, Colors.Transparent) { RadiusX = .38, RadiusY = 1.3, Center = new Point(.3, 0), GradientOrigin = new Point(.3, 0) };
        glass.Children.Add(new Border { Background = light, CornerRadius = new CornerRadius(22), Margin = new Thickness(1), IsHitTestVisible = false });
        glass.Children.Add(new Border { BorderBrush = Brush("#15FFFFFF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(21), Margin = new Thickness(2), IsHitTestVisible = false });
        glass.Children.Add(new Border { Child = content, Padding = new Thickness(10, 5, 10, 5) });
        glass.MouseMove += (_, e) =>
        {
            var position = e.GetPosition(glass);
            light.Center = light.GradientOrigin = new Point(position.X / Math.Max(1, glass.ActualWidth), position.Y / Math.Max(1, glass.ActualHeight));
        };
        glass.MouseLeave += (_, _) => light.Center = light.GradientOrigin = new Point(.3, 0);
        return glass;
    }
}
