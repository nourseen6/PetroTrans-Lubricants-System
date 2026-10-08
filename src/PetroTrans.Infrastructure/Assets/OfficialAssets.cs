namespace PetroTrans.Infrastructure.Assets;

public static class OfficialAssets
{
    public const string FolderName = "ASSESTS";
    public const string ProductImagesSubfolder = "products list and pic";
    public const string RequestPath = "/official-assets";

    public static string? FindRoot()
    {
        var start = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        foreach (var origin in start)
        {
            var found = WalkForAssets(origin);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    public static IReadOnlyList<(string RelativePath, string FullPath)> ListProductImages()
    {
        var root = FindRoot();
        if (root is null)
        {
            return [];
        }

        var folder = Path.Combine(root, ProductImagesSubfolder);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
        return Directory.EnumerateFiles(folder)
            .Where(path => allowed.Contains(Path.GetExtension(path)))
            .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
            .Select(path =>
            {
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                return (relative, path);
            })
            .ToList();
    }

    public static string? ToPublicUrl(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var parts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return RequestPath + "/" + string.Join("/", parts.Select(Uri.EscapeDataString));
    }

    public static bool IsAllowedProductImage(string relativePath)
    {
        return ListProductImages().Any(item =>
            string.Equals(item.RelativePath, NormalizeRelative(relativePath), StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<(string RelativePath, string FullPath)> ListLogos()
    {
        var root = FindRoot();
        if (root is null)
        {
            return [];
        }

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
        return Directory.EnumerateFiles(root)
            .Where(path => allowed.Contains(Path.GetExtension(path)))
            .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
            .Select(path =>
            {
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                return (relative, path);
            })
            .ToList();
    }

    public static bool IsAllowedLogo(string relativePath)
    {
        return ListLogos().Any(item =>
            string.Equals(item.RelativePath, NormalizeRelative(relativePath), StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeRelative(string relativePath)
    {
        return relativePath.Replace('\\', '/').Trim().TrimStart('/');
    }

    private static string? WalkForAssets(string start)
    {
        DirectoryInfo? current = new(start);
        for (var i = 0; i < 8 && current is not null; i++)
        {
            var candidate = Path.Combine(current.FullName, FolderName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
