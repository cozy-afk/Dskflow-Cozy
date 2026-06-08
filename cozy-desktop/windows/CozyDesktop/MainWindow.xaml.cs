using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CozyDesktop.Services;

namespace CozyDesktop;

public partial class MainWindow : Window
{
    private readonly DeskflowController _controller = new();

    // ---- visual layout grid ----
    private const double CellW = 116, CellH = 92, Pad = 12, Gap = 10;
    private readonly List<Tile> _tiles = new();
    private Tile? _selected;
    private Tile? _dragging;
    private Point _dragOffset;

    public MainWindow()
    {
        InitializeComponent();

        _controller.Log += OnLog;
        _controller.RunningChanged += OnRunningChanged;

        IpText.Text = $"This PC: {GetLanIp()}";

        SelNameBox.TextChanged += (_, _) =>
        {
            if (_selected is { IsPc: false } t) { t.Name = SelNameBox.Text; t.Label.Text = t.Name; }
        };

        // Seed: PC in the middle, tablet to its right, Mac to its left (drag to taste).
        CreateTile("This PC", isPc: true, col: 2, row: 1);
        CreateTile("android-tablet", isPc: false, col: 3, row: 1);
        CreateTile("macbook", isPc: false, col: 1, row: 1);

        Loaded += (_, _) => RefreshEngine();
        Activated += (_, _) => RefreshEngine();
        LayoutCanvas.MouseLeftButtonDown += (_, _) => Select(null);
    }

    // ---- engine presence ----
    private void RefreshEngine()
    {
        bool ok = _controller.IsDeskflowInstalled();
        EngineCard.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
        StartButton.IsEnabled = ok;
        if (!ok) StatusDetail.Text = "Finish the one-time setup below, then press Start.";
    }

    // ---- tiles ----
    private void CreateTile(string name, bool isPc, int col, int row)
    {
        var tile = new Tile { Name = name, IsPc = isPc, Col = col, Row = row };

        var monitors = MonitorCount();
        var label = new TextBlock
        {
            Text = isPc ? (monitors > 1 ? $"This PC\n{monitors} displays" : "This PC") : name,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
        };
        // Live status dot: PC is always "on"; devices start grey and turn green on connect.
        var dot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Margin = new Thickness(0, 6, 6, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Fill = isPc ? (Brush)FindResource("Accent2") : Brushes.Gray,
            ToolTip = isPc ? "This PC (server)" : "Not connected",
        };
        var inner = new Grid();
        inner.Children.Add(label);
        inner.Children.Add(dot);
        var border = new Border
        {
            Width = CellW - Gap,
            Height = CellH - Gap,
            CornerRadius = new CornerRadius(8),
            Background = isPc ? (Brush)FindResource("Accent") : (Brush)FindResource("Panel2"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(isPc ? 0 : 1),
            Cursor = Cursors.SizeAll,
            Child = inner,
        };
        tile.Ui = border;
        tile.Label = label;
        tile.Status = dot;

        border.MouseLeftButtonDown += (s, e) => BeginDrag(tile, e);
        border.MouseMove += (s, e) => DragMove(tile, e);
        border.MouseLeftButtonUp += (s, e) => EndDrag(tile);

        LayoutCanvas.Children.Add(border);
        _tiles.Add(tile);
        PositionTile(tile);
    }

    private void PositionTile(Tile t)
    {
        Canvas.SetLeft(t.Ui, t.Col * CellW + Pad);
        Canvas.SetTop(t.Ui, t.Row * CellH + Pad);
    }

    private void BeginDrag(Tile t, MouseButtonEventArgs e)
    {
        Select(t);
        _dragging = t;
        var p = e.GetPosition(LayoutCanvas);
        _dragOffset = new Point(p.X - Canvas.GetLeft(t.Ui), p.Y - Canvas.GetTop(t.Ui));
        t.Ui.CaptureMouse();
        Panel.SetZIndex(t.Ui, 10);
        e.Handled = true;
    }

    private void DragMove(Tile t, MouseEventArgs e)
    {
        if (_dragging != t) return;
        var p = e.GetPosition(LayoutCanvas);
        Canvas.SetLeft(t.Ui, p.X - _dragOffset.X);
        Canvas.SetTop(t.Ui, p.Y - _dragOffset.Y);
    }

    private void EndDrag(Tile t)
    {
        if (_dragging != t) return;
        _dragging = null;
        t.Ui.ReleaseMouseCapture();
        Panel.SetZIndex(t.Ui, 0);

        // Snap to nearest cell, clamped to the canvas, reverting if occupied.
        int maxCol = Math.Max(0, (int)((LayoutCanvas.ActualWidth - Pad) / CellW) - 1);
        int maxRow = Math.Max(0, (int)((LayoutCanvas.ActualHeight - Pad) / CellH) - 1);
        int col = Math.Clamp((int)Math.Round((Canvas.GetLeft(t.Ui) - Pad) / CellW), 0, maxCol);
        int row = Math.Clamp((int)Math.Round((Canvas.GetTop(t.Ui) - Pad) / CellH), 0, maxRow);

        bool occupied = _tiles.Any(o => o != t && o.Col == col && o.Row == row);
        if (!occupied) { t.Col = col; t.Row = row; }
        PositionTile(t); // either commit or snap back
    }

    private void Select(Tile? t)
    {
        _selected = t;
        foreach (var x in _tiles)
            x.Ui.BorderThickness = new Thickness(x == t ? 2 : (x.IsPc ? 0 : 1));
        foreach (var x in _tiles)
            x.Ui.BorderBrush = x == t ? (Brush)FindResource("Accent2") : (Brush)FindResource("Line");

        if (t is { IsPc: false })
        {
            SelectionBar.Visibility = Visibility.Visible;
            SelNameBox.Text = t.Name;
        }
        else SelectionBar.Visibility = Visibility.Collapsed;
    }

    private void AddDevice_Click(object sender, RoutedEventArgs e)
    {
        // first free cell, scanning rows then cols
        for (int row = 0; row < 6; row++)
            for (int col = 0; col < 8; col++)
                if (!_tiles.Any(t => t.Col == col && t.Row == row))
                {
                    CreateTile("new-device", false, col, row);
                    Select(_tiles[^1]);
                    return;
                }
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is { IsPc: false } t)
        {
            LayoutCanvas.Children.Remove(t.Ui);
            _tiles.Remove(t);
            Select(null);
        }
    }

    // ---- build link graph from tile adjacency ----
    private (List<string> devices, List<(string from, string side, string to)> links) BuildGraph()
    {
        string SN(Tile t) => t.IsPc ? _controller.PcName : DeskflowController.SanitizeName(t.Name);
        var links = new List<(string, string, string)>();
        foreach (var a in _tiles)
            foreach (var b in _tiles)
            {
                if (a == b) continue;
                if (a.Row == b.Row && b.Col == a.Col + 1) { links.Add((SN(a), "right", SN(b))); links.Add((SN(b), "left", SN(a))); }
                if (a.Col == b.Col && b.Row == a.Row + 1) { links.Add((SN(a), "down", SN(b))); links.Add((SN(b), "up", SN(a))); }
            }
        var devices = _tiles.Where(t => !t.IsPc).Select(SN).Distinct().ToList();
        return (devices, links);
    }

    // ---- start / stop ----
    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_controller.IsRunning) { _controller.Stop(); return; }
        var (devices, links) = BuildGraph();
        if (devices.Count == 0) { OnLog("Add at least one device first."); return; }
        int port = int.TryParse(PortBox.Text, out var p) ? p : 24800;
        ResetDeviceStatuses(); // grey until each device actually connects
        _controller.StartGraph(devices, links, port, TlsCheck.IsChecked == true);
    }

    private void OnRunningChanged(bool running)
    {
        Dispatcher.Invoke(() =>
        {
            StatusDot.Fill = running ? (Brush)FindResource("Accent2") : (Brush)FindResource("Muted");
            StatusText.Text = running ? "Sharing" : "Not sharing";
            StartButton.Content = running ? "Stop Sharing" : "Start Sharing";
            StatusDetail.Text = running
                ? "Waiting for devices to connect. A tile turns green when its device connects."
                : "Press Start to share this PC's keyboard and mouse with all your devices.";
            if (!running) ResetDeviceStatuses();
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

    // ---- logging ----
    private void OnLog(string line)
    {
        // Live connection status straight from the engine log.
        var c = Regex.Match(line, "client \"(.+?)\" has connected");
        if (c.Success) SetClientStatus(c.Groups[1].Value, true);
        var d = Regex.Match(line, "client \"(.+?)\" has disconnected");
        if (d.Success) SetClientStatus(d.Groups[1].Value, false);

        Dispatcher.Invoke(() =>
        {
            LogBox.AppendText(line + Environment.NewLine);
            LogBox.ScrollToEnd();
        });
    }

    private void SetClientStatus(string screenName, bool connected)
    {
        Dispatcher.Invoke(() =>
        {
            foreach (var t in _tiles.Where(t => !t.IsPc && t.Status != null))
            {
                if (DeskflowController.SanitizeName(t.Name) == screenName)
                {
                    t.Status!.Fill = connected ? (Brush)FindResource("Accent2") : Brushes.Gray;
                    t.Status!.ToolTip = connected ? "Connected" : "Not connected";
                }
            }
        });
    }

    private void ResetDeviceStatuses()
    {
        foreach (var t in _tiles.Where(t => !t.IsPc && t.Status != null))
        {
            t.Status!.Fill = Brushes.Gray;
            t.Status!.ToolTip = "Not connected";
        }
    }

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
    private static int MonitorCount() => Math.Max(1, GetSystemMetrics(80)); // SM_CMONITORS

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

    /// <summary>A draggable device tile on the layout canvas.</summary>
    private sealed class Tile
    {
        public string Name = "";
        public bool IsPc;
        public int Col, Row;
        public Border Ui = null!;
        public TextBlock Label = null!;
        public Ellipse? Status;
    }
}
