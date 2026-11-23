namespace Larp.Core;

public static class ImageLoader
{
    private const long MaxBytes = 8 * 1024 * 1024;

    private static readonly Dictionary<string, string> Mime = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".gif"] = "image/gif",
        [".webp"] = "image/webp", [".svg"] = "image/svg+xml", [".ico"] = "image/x-icon", [".bmp"] = "image/bmp",
    };

    /// <summary>Read an image file and return a data: URL the webview can display.</summary>
    public static string ToDataUrl(string path)
    {
        var ext = Path.GetExtension(path);
        if (!Mime.TryGetValue(ext, out var mime)) throw new LarpException($"unsupported image type `{ext}`");
        var info = new FileInfo(path);
        if (!info.Exists) throw new LarpException($"image not found: {path}");
        if (info.Length > MaxBytes) throw new LarpException("image larger than 8 MB");
        return $"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
    }
}
