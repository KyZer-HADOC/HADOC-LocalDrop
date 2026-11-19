namespace HADOC.LocalDrop.Models;

public sealed record ChatMessage(string Sender, string Text, DateTimeOffset SentAt)
{
    public string DisplayText => $"{SentAt.ToLocalTime():HH:mm}  {Sender}: {Text}";
}
