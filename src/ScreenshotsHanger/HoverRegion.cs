using System.Drawing;

namespace ScreenshotsHanger;

internal static class HoverRegion
{
    // Use monitor-relative proportions so the regions work on any DPI and monitor layout.
    public static bool Contains(HoverRevealArea area, Rectangle monitor, int cursorX, int cursorY)
    {
        long x = (long)cursorX - monitor.Left;
        long y = (long)cursorY - monitor.Top;
        if (monitor.Width <= 0 || monitor.Height <= 0 || x < 0 || x >= monitor.Width || y < 0 || y >= monitor.Height || y > 2) return false;
        double position = x / (double)monitor.Width;
        return area switch
        {
            HoverRevealArea.TopLeftCorner => position < .1,
            HoverRevealArea.TopRightCorner => position >= .9,
            HoverRevealArea.EntireTopEdge => true,
            _ => position >= .25 && position < .75
        };
    }
}
