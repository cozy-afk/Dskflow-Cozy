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
    private readonly List<DeviceRow> _rows = new();

    private static readonly string[] Sides = { "right", "left", "up", "down" };
    private static string SideLabel(string s) => s switch
    {
        "right" => "▶ Right", "left" => "◀ Left", "up" => "▲ Above", "down" => "▼ Below", _ => s
    };

    public MainWindow()
    {
        InitializeComponent();

        _controller.Log += OnLog;
        _controller.RunningChanged += OnRunningChanged;

        IpText.Text = $"This PC: {GetLanIp()}";

        // Seed with the two common devices so multi-device is obvious out of the box.
        AddDeviceRow("android-tablet", "right");
        AddDeviceRow("macbook", "left");

        Loaded += (_, _) => RefreshEngine();
        Activated += (_, _) => RefreshEngine();
    }

    // ---- engine presence ----
    private void RefreshEngine()
    {
        bool ok = _controller.IsDeskflowInstalled();
        EngineCard.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
        StartButton.IsEnabled = ok;
        if (!ok) StatusDetail.Text = "Finish the one-time setup below, then press Start.";
    }

    // ---- device rows ----
    private void AddDevice_Click(object sender, RoutedEventArgs e)
    {
        // Pick the first edge not already taken.
        var used = _rows.Select(r => r.Side).ToHashSet();
        var side = Sides.FirstOrDefault(s => !used.Contains(s)) ?? "right";
        AddDeviceRow(side == "right" && used.Contains("right") ? "right" : "new-device", side);
    }

    private void AddDeviceRow(string name, string side)
    {
        var row = new DeviceRow { Side = side };

        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameBox = new TextBox { Text = name, Margin = new Thickness(0, 0, 8, 0) };
        Grid.SetColumn(nameBox, 0);
        row.NameBox = nameBox;

        var sideBtn = new Button
        {
            Content = SideLabel(side),
            Style = (Style)FindResource("GhostButton"),
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 96,
        };
        sideBtn.Click += (_, _) =>
        {
            int i = Array.IndexOf(Sides, row.Side);
            row.Side = Sides[(i + 1) % Sides.Length];
            sideBtn.Content = SideLabel(row.Side);
        };
        Grid.SetColumn(sideBtn, 1);

        var removeBtn = new Button
        {
            Content = "✕",
            Style = (Style)FindResource("GhostButton"),
            MinWidth = 36,
        };
        removeBtn.Click += (_, _) =>
        {
            _rows.Remove(row);
            DeviceListPanel.Children.Remove(grid);
        };
        Grid.SetColumn(removeBtn, 2);

        grid.Children.Add(nameBox);
        grid.Children.Add(sideBtn);
        grid.Children.Add(removeBtn);

        DeviceListPanel.Children.Add(grid);
        _rows.Add(row);
    }

    private List<Device> CollectDevices()
    {
        var list = new List<Device>();
        var usedSides = new HashSet<string>();
        foreach (var r in _rows)
        {
            var name = DeskflowController.SanitizeName(r.NameBox.Text);
            if (string.IsNullOrWhiteSpace(r.NameBox.Text)) continue;
            // If two devices share a side, nudge the later one so links stay valid.
            var side = r.Side;
            if (!usedSides.Add(side))
            {
                side = Sides.FirstOrDefault(s => !usedSides.Contains(s)) ?? side;
                usedSides.Add(side);
            }
            list.Add(new Device(name, side));
        }
        return list;
    }

    // ---- start / stop ----
    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_controller.IsRunning) { _controller.Stop(); return; }

        var devices = CollectDevices();
        if (devices.Count == 0) { OnLog("Add at least one device first."); return; }
        int port = int.TryParse(PortBox.Text, out var p) ? p : 24800;
        _controller.Start(devices, port, TlsCheck.IsChecked == true);
    }

    private void OnRunningChanged(bool running)
    {
        Dispatcher.Invoke(() =>
        {
            StatusDot.Fill = running ? (Brush)FindResource("Accent2") : (Brush)FindResource("Muted");
            StatusText.Text = running ? "Sharing" : "Not sharing";
            StartButton.Content = running ? "Stop Sharing" : "Start Sharing";
            StatusDetail.Text = running
                ? "Move your mouse toward each device's edge to reach it. Connect each device to this PC."
                : "Press Start to share this PC's keyboard and mouse with all your devices.";
        });
    }

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
        catch (Exception ex) { OnLog($"Setup could not start: {ex.Message}"); }
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
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
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

    /// <summary>Tracks one device row's controls + selected side.</summary>
    private sealed class DeviceRow
    {
        public TextBox NameBox = null!;
        public string Side = "right";
    }
}
