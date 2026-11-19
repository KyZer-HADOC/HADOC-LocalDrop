using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

    public ObservableCollection<SharedFile> SharedFiles { get; } = new();
    public ObservableCollection<ReceivedFile> ReceivedFiles { get; } = new();
    public ObservableCollection<ChatMessage> ChatMessages { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _server.StatusChanged += message =>
            Dispatcher.Invoke(() => StatusText.Text = message);

        _server.DeviceConnected += name =>
            Dispatcher.Invoke(() =>
            {
                ConnectedDeviceText.Text = name;
                ConnectionStatusText.Text = "LIVE";
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
            });

        _server.ChatReceived += message =>
            Dispatcher.Invoke(() =>
            {
                ChatMessages.Add(message);
                ChatList.ScrollIntoView(message);
            });

        _server.FileReceived += file =>
            Dispatcher.Invoke(() =>
            {
                ReceivedFiles.Insert(0, file);
                StatusText.Text = "Received " + file.Name + " · " + SharedFile.FormatSize(file.Size);
            });

        try
        {
            foreach (var file in _server.GetReceivedFiles())
                ReceivedFiles.Add(file);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Receive folder unavailable: " + ex.Message;
        }
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
                StartStopButton.Background = AccentBrush("#E54852");
            }
            else
            {
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
        UrlText.Text = "Choose the network shared with your phone.";
        _currentUrl = null;
        ConnectedDeviceText.Text = "Mobile phone";
        ConnectionStatusText.Text = "OFFLINE";
    }

    private void AddressBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_server.IsRunning && AddressBox.SelectedItem is string)
            RefreshQrCode();
    }

    private void RefreshQrCode()
    {
        if (AddressBox.SelectedItem is not string address || !_server.IsRunning)
            return;

        _currentUrl = _server.CreatePairUrl(address);
        UrlText.Text = _currentUrl;

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(_currentUrl, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(9);

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
        var dialog = new OpenFileDialog
        {
            Title = "Select files to send to your phone",
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
                SharedFiles.Add(_server.AddSharedFile(path));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not add file",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        StatusText.Text = SharedFiles.Count + " file(s) ready for your phone.";
    }

    private void RemoveSharedFile_Click(object sender, RoutedEventArgs e)
    {
        if (SharedFilesList.SelectedItem is not SharedFile file)
            return;

        _server.RemoveSharedFile(file.Id);
        SharedFiles.Remove(file);
    }

    private async void SendChat_Click(object sender, RoutedEventArgs e) =>
        await SendChatAsync();

    private async void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
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

        if (!_server.IsRunning || !_server.IsPhoneConnected)
        {
            StatusText.Text = "Your phone is reconnecting. Please wait.";
            return;
        }

        var message = new ChatMessage("PC", text, DateTimeOffset.Now);
        ChatMessages.Add(message);
        ChatList.ScrollIntoView(message);
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

    private void OpenReceiveFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = _server.GetReceivedFolder();
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
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
        try
        {
            await _server.DisposeAsync();
        }
        catch
        {
            // The server is being stopped as the app closes.
        }
    }
}
