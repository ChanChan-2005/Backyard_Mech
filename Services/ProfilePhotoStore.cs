namespace Backyard_Mech.Services;

public static class ProfilePhotoStore
{
    private static readonly (string Extension, string ContentType)[] Types =
    [
        (".jpg", "image/jpeg"),
        (".png", "image/png"),
        (".webp", "image/webp")
    ];

    private static string Folder(string contentRoot) => Path.Combine(contentRoot, "App_Data", "ProfilePhotos");

    public static (string Path, string ContentType)? Find(string contentRoot, int userId)
    {
        foreach (var type in Types)
        {
            var path = Path.Combine(Folder(contentRoot), $"{userId}{type.Extension}");
            if (File.Exists(path)) return (path, type.ContentType);
        }
        return null;
    }

    public static async Task SaveAsync(string contentRoot, int userId, IFormFile photo, string extension)
    {
        var folder = Folder(contentRoot);
        Directory.CreateDirectory(folder);
        var temporaryPath = Path.Combine(folder, $"{userId}-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var target = File.Create(temporaryPath)) await photo.CopyToAsync(target);
            File.Move(temporaryPath, Path.Combine(folder, $"{userId}{extension}"), true);
            foreach (var type in Types.Where(type => type.Extension != extension))
            {
                var oldPath = Path.Combine(folder, $"{userId}{type.Extension}");
                if (File.Exists(oldPath)) File.Delete(oldPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public static void Remove(string contentRoot, int userId)
    {
        foreach (var type in Types)
        {
            var path = Path.Combine(Folder(contentRoot), $"{userId}{type.Extension}");
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
