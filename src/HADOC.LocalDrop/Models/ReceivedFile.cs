namespace HADOC.LocalDrop.Models;

public sealed record ReceivedFile(string Name, string Path, long Size, DateTime ReceivedAt)
{
    public string DisplayText =>
        Name + "  ·  " + SharedFile.FormatSize(Size) + "  ·  " + ReceivedAt.ToString("HH:mm");
}
