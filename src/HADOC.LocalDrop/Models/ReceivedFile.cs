namespace HADOC.LocalDrop.Models;

public sealed record ReceivedFile(string Id, string Name, string Path, long Size, DateTime ReceivedAt)
{
    public string SizeLabel => SharedFile.FormatSize(Size);
    public string DisplayText => Name + "  ·  " + SizeLabel + "  ·  " + ReceivedAt.ToString("HH:mm");
}