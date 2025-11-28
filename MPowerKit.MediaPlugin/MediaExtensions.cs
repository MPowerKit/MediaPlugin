using System.Globalization;
using System.Text.RegularExpressions;

namespace MPowerKit.MediaPlugin;

public static partial class MediaExtensions
{
    private const string IllegalCharacters = "[|\\?*<\":>/']";

    [GeneratedRegex(IllegalCharacters)]
    private static partial Regex IllegalCharactersRegex();

    public static void VerifyCaptureRequest(this CaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Path.IsPathRooted(request.DesiredDirectory))
            throw new ArgumentException("options.Directory must be a relative path", nameof(request));

        if (!string.IsNullOrWhiteSpace(request.DesiredName))
            request.DesiredName = IllegalCharactersRegex().Replace(request.DesiredName, string.Empty).Replace(@"\", string.Empty);

        if (!string.IsNullOrWhiteSpace(request.DesiredDirectory))
            request.DesiredDirectory = IllegalCharactersRegex().Replace(request.DesiredDirectory, string.Empty).Replace(@"\", string.Empty);
    }

    public static string GetUniquePath(string folder, string name, bool isPhoto)
    {
        var ext = Path.GetExtension(name);
        if (string.IsNullOrWhiteSpace(ext)) ext = isPhoto ? ".jpg" : ".mp4";

        name = Path.GetFileNameWithoutExtension(name);

        var nameExt = name + ext;
        var i = 1;
        while (File.Exists(Path.Combine(folder, nameExt)))
            nameExt = name + "_" + i++ + ext;

        return Path.Combine(folder, nameExt);
    }

    public static string GetDesiredPath(string? subdir, string? name, bool isPhoto)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            name = isPhoto ? $"IMG_{timestamp}.jpg" : $"VID_{timestamp}.mp4";
        }

        var path = string.IsNullOrWhiteSpace(subdir)
            ? FileSystem.Current.AppDataDirectory
            : Path.Combine(FileSystem.Current.AppDataDirectory, subdir);

        return GetUniquePath(path, name, isPhoto);
    }
}