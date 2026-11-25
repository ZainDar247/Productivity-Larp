using System.Security.Cryptography;

namespace Larp.Core;

public static class ImageLoader
{
    private const long MaxBytes = 8 * 1024 * 1024;
    public const string FolderName = "images";

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

    /// <summary>A config value may be an absolute path or one relative to the config folder (e.g. "images/ssms.png").</summary>
    public static string Resolve(string path, string configDir) => Path.GetFullPath(path, configDir);

    /// <summary>
    /// Copy an image into &lt;configDir&gt;/images and return the config-relative path ("images/name.ext").
    /// The same file is never copied twice, and a different file with the same name gets a numbered name.
    /// A file already inside the images folder is returned as-is.
    /// </summary>
    public static string Import(string source, string configDir)
    {
        var src = Resolve(source, configDir);
        var ext = Path.GetExtension(src);
        if (!Mime.ContainsKey(ext)) throw new LarpException($"unsupported image type `{ext}`");
        var info = new FileInfo(src);
        if (!info.Exists) throw new LarpException($"image not found: {src}");
        if (info.Length > MaxBytes) throw new LarpException("image larger than 8 MB");

        var dir = Path.GetFullPath(Path.Combine(configDir, FolderName));
        Directory.CreateDirectory(dir);
        var name = Path.GetFileName(src);
        if (string.Equals(Path.GetDirectoryName(src), dir, StringComparison.OrdinalIgnoreCase))
            return $"{FolderName}/{name}";

        var hash = SHA256.HashData(File.ReadAllBytes(src));
        var stem = Path.GetFileNameWithoutExtension(src);
        for (var n = 1; ; n++)
        {
            var candidate = Path.Combine(dir, n == 1 ? name : $"{stem}-{n}{ext}");
            if (!File.Exists(candidate))
            {
                File.Copy(src, candidate);
                return $"{FolderName}/{Path.GetFileName(candidate)}";
            }
            if (SHA256.HashData(File.ReadAllBytes(candidate)).AsSpan().SequenceEqual(hash))
                return $"{FolderName}/{Path.GetFileName(candidate)}";
        }
    }
}
