using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenshotsHanger;

public sealed record Shot(string Path, string Hash, DateTime CapturedAt, BitmapSource Thumbnail)
{
    public string Name => System.IO.Path.GetFileName(Path);
}

public sealed class ScreenshotStore
{
    public const int HistoryLimit = 100;
    public string DirectoryPath { get; }
    public List<Shot> Shots { get; } = new();
    public event Action? Changed;
    public ScreenshotStore(string directory)
    {
        DirectoryPath = directory;
        Directory.CreateDirectory(directory);
        CleanupRemoved();
        foreach (var file in Directory.EnumerateFiles(directory, "Screenshot-*.png").Where(f => !File.Exists(f + ".removed")).OrderByDescending(File.GetCreationTimeUtc))
        {
            try
            {
                var bitmap = LoadImage(file);
                Shots.Add(new Shot(file, Fingerprint(bitmap), File.GetCreationTime(file), Thumbnail(file)));
            }
            catch (Exception e) when (e is IOException or NotSupportedException or System.IO.FileFormatException or ArgumentException or UnauthorizedAccessException) { }
        }
        Trim();
    }
    public Shot? Add(BitmapSource source)
    {
        if (source.PixelWidth < 1 || source.PixelHeight < 1 || (long)source.PixelWidth * source.PixelHeight > 80_000_000)
            throw new ArgumentException("This image is too large. Please use an image under 80 megapixels.");
        var bitmap = Normalize(source);
        var hash = Fingerprint(bitmap);
        if (Shots.Any(s => s.Hash == hash)) return null;
        var now = DateTime.Now;
        var path = Path.Combine(DirectoryPath, $"Screenshot-{now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid().ToString("N")[..6]}.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        try
        {
            using (var stream = File.Create(path + ".tmp")) encoder.Save(stream);
            File.Move(path + ".tmp", path);
        }
        finally { if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp"); }
        var shot = new Shot(path, hash, now, Thumbnail(path));
        Shots.Insert(0, shot);
        Trim();
        Changed?.Invoke();
        return shot;
    }
    public Shot? Import(string path) => Add(LoadImage(path));
    public void Remove(Shot shot)
    {
        // Keep a grace period: apps receiving a file drop may read the PNG asynchronously.
        File.WriteAllText(shot.Path + ".removed", DateTime.UtcNow.ToString("O"));
        Shots.Remove(shot);
        Changed?.Invoke();
    }
    public void Clear()
    {
        bool changed = false;
        try
        {
            foreach (var shot in Shots.ToArray())
            {
                File.WriteAllText(shot.Path + ".removed", DateTime.UtcNow.ToString("O"));
                Shots.Remove(shot);
                changed = true;
            }
        }
        finally
        {
            // Refresh the rope once, including partial progress if a disk write fails.
            if (changed) Changed?.Invoke();
        }
    }
    private void Trim()
    {
        foreach (var shot in Shots.Where(s => File.Exists(s.Path + ".removed")).ToArray()) Shots.Remove(shot);
        foreach (var shot in Shots.Skip(HistoryLimit).ToArray()) Remove(shot);
        CleanupRemoved();
    }
    public void CleanupRemoved()
    {
        foreach (var marker in Directory.EnumerateFiles(DirectoryPath, "*.removed"))
        {
            if (File.GetLastWriteTimeUtc(marker) > DateTime.UtcNow.AddDays(-1)) continue;
            try { File.Delete(marker[..^8]); File.Delete(marker); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
    public static BitmapSource LoadImage(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        if ((long)frame.PixelWidth * frame.PixelHeight > 80_000_000) throw new ArgumentException("Image exceeds 80 megapixels.");
        frame.Freeze();
        return frame;
    }
    private static BitmapSource Thumbnail(string path)
    {
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(path); image.DecodePixelWidth = 420; image.EndInit(); image.Freeze();
        return image;
    }
    public static BitmapSource Normalize(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = checked(converted.PixelWidth * 4);
        var pixels = new byte[checked(stride * converted.PixelHeight)];
        converted.CopyPixels(pixels, stride, 0);
        bool anyAlpha = false;
        for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] != 0) { anyAlpha = true; break; }
        if (!anyAlpha) for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
        var result = BitmapSource.Create(converted.PixelWidth, converted.PixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze(); return result;
    }
    public static string Fingerprint(BitmapSource source)
    {
        var normalized = Normalize(source);
        int stride = checked(normalized.PixelWidth * 4);
        var pixels = new byte[checked(stride * normalized.PixelHeight + 8)];
        normalized.CopyPixels(new Int32Rect(0, 0, normalized.PixelWidth, normalized.PixelHeight), pixels, stride, 8);
        BitConverter.GetBytes(normalized.PixelWidth).CopyTo(pixels, 0);
        BitConverter.GetBytes(normalized.PixelHeight).CopyTo(pixels, 4);
        return Convert.ToHexString(SHA256.HashData(pixels));
    }
    public static DataObject DragData(Shot shot)
    {
        var data = new DataObject();
        data.SetData(DataFormats.FileDrop, new[] { shot.Path });
        data.SetData(DataFormats.Bitmap, LoadImage(shot.Path));
        return data;
    }
}
