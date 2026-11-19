using System.ComponentModel;
using System.IO;
using HADOC.LocalDrop.Models;

namespace HADOC.LocalDrop;

public sealed class ChatItem : INotifyPropertyChanged
{
    private string? _filePath;
    private string _fileName = "";
    private long _fileSize;
    private double _progress;
    private string _transferStatus = "";

    public ChatItem(ChatMessage message, bool isMine)
    {
        Sender = message.Sender;
        Text = message.Text;
        SentAt = message.SentAt;
        IsMine = isMine;
        IsFile = false;
        FileId = "";
    }

    public ChatItem(string fileId, string sender, string fileName, long fileSize,
        string? filePath, bool isMine, string transferStatus)
    {
        FileId = fileId;
        Sender = sender;
        SentAt = DateTimeOffset.Now;
        IsMine = isMine;
        IsFile = true;
        _fileName = fileName;
        _fileSize = fileSize;
        _filePath = filePath;
        _transferStatus = transferStatus;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Sender { get; }
    public string Text { get; } = "";
    public DateTimeOffset SentAt { get; }
    public bool IsMine { get; }
    public bool IsFile { get; }
    public string FileId { get; }

    public string FileName
    {
        get => _fileName;
        set { if (_fileName == value) return; _fileName = value; Changed(); }
    }

    public long FileSize
    {
        get => _fileSize;
        set { if (_fileSize == value) return; _fileSize = value; Changed(); Changed(nameof(FileSizeLabel)); }
    }

    public string FileSizeLabel => SharedFile.FormatSize(FileSize);

    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath == value) return;
            _filePath = value;
            Changed();
            Changed(nameof(CanOpenFile));
        }
    }

    public bool CanOpenFile => !string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath);

    public double Progress
    {
        get => _progress;
        set { if (Math.Abs(_progress - value) < 0.01) return; _progress = value; Changed(); }
    }

    public string TransferStatus
    {
        get => _transferStatus;
        set { if (_transferStatus == value) return; _transferStatus = value; Changed(); }
    }

    private void Changed([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}