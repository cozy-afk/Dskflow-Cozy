using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CozyDesktop.Services;

namespace CozyDesktop;

public partial class MainWindow : Window
{
    private readonly DeskflowController _controller = new();
    private string _side = "right";
    private Button[] _sideButtons = Array.Empty<Button>();

    public MainWindow()
    {
        InitializeComponent();

        _controller.Log += OnLog;
        _controller.RunningChanged += OnRunningChanged;

        _sideButtons = new[] { SideLeft, SideRight, SideUp, SideDown };
        SelectSide("right");

        IpText.Text = $"This PC: {GetLanIp()}";
        Loaded += (_, _) => RefreshEngine();
        Activated += (_, _) => RefreshEngine();
    }

    // ---- engine presence ----
    private void RefreshEngine()
    {
        bool ok = _controller.IsDeskflowInstalled();
        EngineCard.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
        StartButton.IsEnabled = ok;
        if (!ok)
        {
            StatusDetail.Text = "Finish the one-time setup below, then press Start.";
        }
    }

    // ---- side picker ----
    private void Side_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string side) SelectSide(side);
    }

    private void SelectSide(string side)
    {
        _side = side;
        foreach (var b in _sideButtons)
        {
            bool sel = (string)b.Tag == side;
            b.Background = sel ? (Brush)FindResource("Accent") : Brushes.Transparent;
            b.Foreground = sel ? Brushes.White : (Brush)FindResource("Ink");
        }
    }

    // ---- start / stop ----
    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_controller.IsRunning)
        {
            _controller.Stop();
            return;
        }
        var name = string.IsNullOrWhiteSpace(ClientNameBox.Text) ? "android-tablet" : ClientNameBox.Text.Trim();
        int port = int.TryParse(PortBox.Text, out var p) ? p : 24800;
        _controller.Start(name, _side, port, TlsCheck.IsChecked == true);
    }

    private void OnRunningChanged(bool running)
    {
        Dispatcher.Invoke(() =>
        {
            StatusDot.Fill = running ? (Brush)FindResource("Accent2") : (Brush)FindResource("Muted");
            StatusText.Text = running ? "Sharing" : "Not sharing";
            StartButton.Content = running ? "Stop Sharing" : "Start Sharing";
            StatusDetail.Text = running
                ? $"Move your mouse {OppositeWord(_side)} to reach the tablet. Connect the tablet to this PC."
                : "Press Start to share this PC's keyboard and mouse.";
        });
    }

    private static string OppositeWord(string side) => side switch
    {
        "left" => "to the left",
        "right" => "to the right",
        "up" => "upward",
        "down" => "downward",
        _ => "across"
    };

    // ---- engine install ----
    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            OnLog("Starting Cozy engine setup (you may see a permission prompt)…");
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command \"winget install --id Deskflow.Deskflow " +
                            "--accept-package-agreements --accept-source-agreements\"",
                UseShellExecute = true,
                Verb = "runas",
            };
            Process.Start(psi);
            OnLog("When setup finishes, this window will detect the engine automatically.");
        }
        catch (Exception ex)
        {
            OnLog($"Setup could not start: {ex.Message}");
        }
    }

    // ---- open the visual layout editor ----
    private void OpenEditor_Click(object sender, RoutedEventArgs e)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "cozy-layout-editor", "index.html"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "cozy-layout-editor", "index.html")),
        };
        var local = candidates.FirstOrDefault(File.Exists);
        var target = local ?? "https://github.com/cozy-afk/Dskflow-Cozy/blob/master/cozy-layout-editor/index.html";
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { OnLog($"Could not open editor: {ex.Message}"); }
    }

    // ---- logging ----
    private void OnLog(string line)
    {
        Dispatcher.Invoke(() =>
        {
            LogBox.AppendText(line + Environment.NewLine);
            LogBox.ScrollToEnd();
        });
    }

    private static string GetLanIp()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ua.Address))
                    {
                        var s = ua.Address.ToString();
                        if (!s.StartsWith("169.")) return s;
                    }
                }
            }
        }
        catch { /* ignore */ }
        return "unknown";
    }

    protected override void OnClosed(EventArgs e)
    {
        _controller.Stop();
        base.OnClosed(e);
    }
}
