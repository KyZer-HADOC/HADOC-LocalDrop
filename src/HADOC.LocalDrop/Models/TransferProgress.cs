namespace HADOC.LocalDrop.Models;

public sealed record TransferProgress(
    string Id,
    string Direction,
    string Name,
    long Loaded,
    long Total,
    double BytesPerSecond,
    string State)
{
    public double Percent => Total <= 0 ? 0 : Math.Clamp(Loaded * 100d / Total, 0d, 100d);
}