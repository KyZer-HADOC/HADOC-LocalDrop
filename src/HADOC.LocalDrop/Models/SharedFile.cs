namespace HADOC.LocalDrop.Models;

public sealed record SharedFile(string Id, string Name, string Path, long Size)
{
    public string DisplayName => $"{Name}    ·    {FormatSize(Size)}";

    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }
}
