using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HADOC.LocalDrop.Models;
using HADOC.LocalDrop.Services;
using Microsoft.Win32;
using QRCoder;

namespace HADOC.LocalDrop;

public partial class MainWindow : Window
{
    private readonly LocalDropServer _server = new();
    private string? _currentUrl;
    private bool _isClosing;

    public ObservableCollection<ChatItem> ChatMessages { get; } = new();
    public ObservableCollection<SharedFile> SharedFiles { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadRouterWifiSettings();

        _server.StatusChanged += message =>
            Dispatcher.Invoke(() => StatusText.Text = message);

        _server.DeviceConnected += name =>
            Dispatcher.Invoke(() =>
            {
                ConnectedDeviceText.Text = name;
                ConnectionStatusText.Text = "CONNECTED";
                ConnectPanel.Visibility = Visibility.Collapsed;
                WorkspacePanel.Visibility = Visibility.Visible;
                StatusText.Text = "Connected to " + name;
            });

        _server.DeviceDisconnected += () =>
            Dispatcher.Invoke(() =>
            {
                WorkspacePanel.Visibility = Visibility.Collapsed;
                ConnectPanel.Visibility = Visibility.Visible;
                ConnectedDeviceText.Text = "Mobile phone";
                ConnectionStatusText.Text = "OFFLINE";
                StatusText.Text = "Phone disconnected · Scan the QR code to reconnect.";
            });

        _server.ChatReceived += message =>
            Dispatcher.Invoke(() =>
            {
                var item = new ChatItem(message, isMine: false);
                ChatMessages.Add(item);
                ChatList.ScrollIntoView(item);
            });

        _server.FileReceived += file =>
            Dispatcher.Invoke(() => AddReceivedFileToConversation(file));

        _server.TransferProgressChanged += progress =>
            Dispatcher.Invoke(() => UpdateTransferInConversation(progress));
    }

    private async void StartStop_Click(object sender, RoutedEventArgs e)
    {
        StartStopButton.IsEnabled = false;
        try
        {
            if (_server.IsRunning)
            {
                await _server.StopAsync();
                ResetPairingView();
                StartStopButton.Content = "Start LocalDrop";
                StartStopButton.Background = AccentBrush("#F0525D");
            }
            else
            {
                var routerSsid = RouterSsidBox.Text.Trim();
                var routerPassword = RouterPasswordBox.Password;
                if (string.IsNullOrWhiteSpace(routerSsid))
                    throw new InvalidOperationException("Enter the router Wi-Fi name (SSID).");
                if (routerPassword.Length < 8 || routerPassword.Length > 63)
                    throw new InvalidOperationException("Enter the router's WPA/WPA2 password (8–63 characters).");
                SaveRouterWifiSettings(routerSsid, routerPassword);

                await _server.StartAsync();
                var addresses = _server.GetLocalAddresses();
                if (addresses.Count == 0)
                {
                    await _server.StopAsync();
                    throw new InvalidOperationException(
                        "No active Wi-Fi or Ethernet IPv4 address was found. Connect this PC to the same Wi-Fi or hotspot as your phone.");
                }

                AddressBox.ItemsSource = addresses;
                AddressBox.SelectedIndex = 0;
                RefreshQrCode();
                StartStopButton.Content = "Stop LocalDrop";
                StartStopButton.Background = AccentBrush("#A8323A");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "HADOC LocalDrop",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusText.Text = "Could not start LocalDrop";
        }
        finally
        {
            StartStopButton.IsEnabled = true;
        }
    }

    private static Brush AccentBrush(string hex) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

    private void ResetPairingView()
    {
        WorkspacePanel.Visibility = Visibility.Collapsed;
        ConnectPanel.Visibility = Visibility.Visible;
        QrImage.Source = null;
        QrPlaceholder.Visibility = Visibility.Visible;
        AddressBox.ItemsSource = null;
        UrlText.Text = "Choose the Wi-Fi or hotspot address for your phone.";
        _currentUrl = null;
        ConnectedDeviceText.Text = "Mobile phone";
        ConnectionStatusText.Text = "OFFLINE";
        ChatMessages.Clear();
        SharedFiles.Clear();
        StatusText.Text = "Server stopped";
    }

    private void AddressBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_server.IsRunning && AddressBox.SelectedItem is string)
            RefreshQrCode();
    }

    private void RouterWifi_Changed(object sender, RoutedEventArgs e)
    {
        if (_server.IsRunning && AddressBox.SelectedItem is string)
            RefreshQrCode();
    }

    private void LoadRouterWifiSettings()
    {
        try
        {
            var path = GetRouterSettingsPath();
            if (!File.Exists(path)) return;

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var ssid = root.GetProperty("ssid").GetString() ?? "";
            var protectedPassword = Convert.FromBase64String(root.GetProperty("protectedPassword").GetString() ?? "");
            var passwordBytes = ProtectedData.Unprotect(
                protectedPassword, optionalEntropy: null, DataProtectionScope.CurrentUser);
            try
            {
                RouterSsidBox.Text = ssid;
                RouterPasswordBox.Password = Encoding.UTF8.GetString(passwordBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(passwordBytes);
                CryptographicOperations.ZeroMemory(protectedPassword);
            }
        }
        catch
        {
            RouterSsidBox.Text = "";
            RouterPasswordBox.Password = "";
        }
    }

    private static string GetRouterSettingsPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HADOC", "LocalDrop", "router-wifi.json");

    private static void SaveRouterWifiSettings(string ssid, string password)
    {
        var folder = Path.GetDirectoryName(GetRouterSettingsPath())!;
        Directory.CreateDirectory(folder);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] protectedPassword;
        try
        {
            protectedPassword = ProtectedData.Protect(
                passwordBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }

        try
        {
            var settings = JsonSerializer.Serialize(new
            {
                ssid,
                protectedPassword = Convert.ToBase64String(protectedPassword)
            });
            File.WriteAllText(GetRouterSettingsPath(), settings);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedPassword);
        }
    }

    private void RefreshQrCode()
    {
        if (AddressBox.SelectedItem is not string address || !_server.IsRunning)
            return;

        var ssid = RouterSsidBox.Text.Trim();
        var password = RouterPasswordBox.Password;
        if (string.IsNullOrWhiteSpace(ssid) || password.Length < 8 || password.Length > 63)
        {
            _currentUrl = null;
            QrImage.Source = null;
            QrPlaceholder.Visibility = Visibility.Visible;
            QrPlaceholder.Children.OfType<TextBlock>().Last().Text =
                "Enter the router Wi-Fi name and password to generate the pairing QR.";
            UrlText.Text = "Router Wi-Fi details are required for automatic phone connection.";
            return;
        }

        _currentUrl = _server.CreatePairUrl(address, ssid, password);
        UrlText.Text = _currentUrl;

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(_currentUrl, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(10);

        var bitmap = new BitmapImage();
        using var stream = new MemoryStream(png);
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();

        QrImage.Source = bitmap;
        QrPlaceholder.Visibility = Visibility.Collapsed;
        StatusText.Text = "Waiting for your phone to scan the QR code...";
    }

    private void CopyLink_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentUrl))
        {
            MessageBox.Show(this, "Start LocalDrop first.", "HADOC LocalDrop",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Clipboard.SetText(_currentUrl);
        StatusText.Text = "Connection link copied. Keep it private.";
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        if (!_server.IsPaired)
        {
            StatusText.Text = "Connect your phone before sharing files.";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Select files to share with your phone",
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        foreach (var path in dialog.FileNames)
        {
            if (SharedFiles.Any(file =>
                string.Equals(file.Path, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            try
            {
                var file = _server.AddSharedFile(path);
                SharedFiles.Add(file);
                var item = new ChatItem(
                    file.Id, "PC", file.Name, file.Size, file.Path,
                    isMine: true, transferStatus: "Shared with phone · Waiting for download");
                ChatMessages.Add(item);
                ChatList.ScrollIntoView(item);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not add file",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        StatusText.Text = "Files added to the conversation · Phone can download them.";
    }

    private async void SendChat_Click(object sender, RoutedEventArgs e) =>
        await SendChatAsync();

    private async void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        // Enter inserts a new line. Ctrl+Enter sends the message.
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            await SendChatAsync();
        }
    }

    private async Task SendChatAsync()
    {
        var text = ChatInput.Text.Trim();
        if (text.Length == 0)
            return;

        if (!_server.IsPhoneConnected)
        {
            StatusText.Text = "Your phone is not live yet. Wait for it to reconnect.";
            return;
        }

        var message = new ChatMessage("PC", text, DateTimeOffset.Now);
        var item = new ChatItem(message, isMine: true);
        ChatMessages.Add(item);
        ChatList.ScrollIntoView(item);
        ChatInput.Clear();

        try
        {
            await _server.SendDesktopChatAsync(text);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Message could not be sent: " + ex.Message;
        }
    }

    private void AddReceivedFileToConversation(ReceivedFile file)
    {
        var item = ChatMessages.FirstOrDefault(entry => entry.FileId == file.Id);
        if (item is null)
        {
            item = new ChatItem(
                file.Id, "PHONE", file.Name, file.Size, file.Path,
                isMine: false, transferStatus: "Received on this PC");
            ChatMessages.Add(item);
        }
        else
        {
            item.FileName = file.Name;
            item.FileSize = file.Size;
            item.FilePath = file.Path;
            item.Progress = 100;
            item.TransferStatus = "Received on this PC";
        }

        item.Progress = 100;
        item.FilePath = file.Path;
        item.TransferStatus = "Received on this PC · " + file.SizeLabel;
        ChatList.ScrollIntoView(item);
        StatusText.Text = "Received " + file.Name + " · " + file.SizeLabel;
    }

    private void UpdateTransferInConversation(TransferProgress progress)
    {
        if (string.IsNullOrWhiteSpace(progress.Id))
            return;

        var isPhoneToPc = string.Equals(progress.Direction, "phone-to-pc",
            StringComparison.OrdinalIgnoreCase);

        var item = ChatMessages.FirstOrDefault(entry => entry.FileId == progress.Id);
        if (item is null)
        {
            item = new ChatItem(
                progress.Id,
                isPhoneToPc ? "PHONE" : "PC",
                string.IsNullOrWhiteSpace(progress.Name) ? "File" : progress.Name,
                progress.Total,
                null,
                isMine: !isPhoneToPc,
                transferStatus: isPhoneToPc ? "Receiving from phone" : "Sending to phone");
            ChatMessages.Add(item);
        }

        if (!string.IsNullOrWhiteSpace(progress.Name))
            item.FileName = progress.Name;
        if (progress.Total > 0)
            item.FileSize = progress.Total;

        item.Progress = progress.Percent;
        var rate = progress.BytesPerSecond > 0
            ? " · " + FormatRate(progress.BytesPerSecond)
            : "";

        item.TransferStatus = progress.State switch
        {
            "completed" when isPhoneToPc => "Received on this PC · " + SharedFile.FormatSize(progress.Total),
            "completed" => "Downloaded to phone · " + SharedFile.FormatSize(progress.Total),
            "failed" => "Transfer failed · Please retry",
            _ when isPhoneToPc => $"Receiving from phone · {progress.Percent:0}%{rate}",
            _ => $"Sending to phone · {progress.Percent:0}%{rate}"
        };

        if (progress.State == "completed")
            item.Progress = 100;

        ChatList.ScrollIntoView(item);
    }

    private static string FormatRate(double bytesPerSecond)
    {
        var mb = bytesPerSecond / (1024d * 1024d);
        return mb >= 1 ? $"{mb:0.0} MB/s" : $"{bytesPerSecond / 1024d:0} KB/s";
    }

    private void OpenChatFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string path ||
            string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open file",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenReceiveFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = _server.GetReceivedFolder();
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open receive folder",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _server.DisconnectPhoneAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Disconnect failed: " + ex.Message;
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing)
            return;

        _isClosing = true;
        try { await _server.DisposeAsync(); }
        catch { /* Stop the local server during shutdown. */ }
    }
}