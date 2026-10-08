using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace ScreenshotsHanger;

public enum HoverRevealArea
{
    TopMiddle = 0,
    TopLeftCorner = 1,
    TopRightCorner = 2,
    EntireTopEdge = 3
}

public sealed class Settings
{
    public int VisibleCount { get; set; } = 5;
    public bool CaptureClipboard { get; set; } = true;
    public bool WatchScreenshotFolder { get; set; } = true;
    public bool RevealOnCapture { get; set; } = true;
    public int HoverDelayMs { get; set; } = 220;
    public HoverRevealArea RevealArea { get; set; } = HoverRevealArea.TopMiddle;
    public bool WelcomeSeen { get; set; }
    public void Validate()
    {
        VisibleCount = Math.Clamp(VisibleCount, 1, 12);
        HoverDelayMs = Math.Clamp(HoverDelayMs, 100, 1000);
        if (!Enum.IsDefined(RevealArea)) RevealArea = HoverRevealArea.TopMiddle;
    }
    public static Settings Load(string path)
    {
        try { var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new(); s.Validate(); return s; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
}

internal static class StartupRegistration
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled { get { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue("ScreenshotsHanger") is string; } }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled) key.SetValue("ScreenshotsHanger", "\"" + Environment.ProcessPath + "\" --background");
        else key.DeleteValue("ScreenshotsHanger", false);
    }
}
