using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CozyDesktop.Services;

/// <summary>
/// A tiny control channel between Cozy desktops (PC ↔ Mac) for multi-controller
/// handoff. Each instance listens on a control port and also dials the peer. When
/// a machine's user becomes active, it broadcasts CLAIM; peers receiving it yield
/// and become clients of the claimer.
///
/// Line protocol (newline-terminated):  HELLO &lt;name&gt;   |   CLAIM &lt;name&gt;
/// </summary>
public sealed class PeerCoordinator : IDisposable
{
    public const int ControlPort = 24811;

    public event Action<string, string>? PeerClaimedControl; // (peerName, peerIp)
    public event Action<string>? Log;

    private TcpListener? _listener;
    private readonly List<TcpClient> _conns = new();
    private CancellationTokenSource? _cts;
    private string _myName = "";
    private string? _peerIp;

    public void Start(string myName, string? peerIp)
    {
        _myName = myName;
        _peerIp = string.IsNullOrWhiteSpace(peerIp) ? null : peerIp;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, ControlPort);
        try { _listener.Start(); } catch (Exception ex) { Log?.Invoke($"coordinator listen failed: {ex.Message}"); }
        _ = AcceptLoop(_cts.Token);
        _ = ConnectLoop(_cts.Token);
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try { AddConn(await _listener.AcceptTcpClientAsync(ct)); }
            catch { break; }
        }
    }

    // Keep a connection to the configured peer alive (retry every few seconds).
    private async Task ConnectLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            bool connected;
            lock (_conns) connected = _conns.Any(c => c.Connected);
            if (_peerIp != null && !connected)
            {
                try
                {
                    var c = new TcpClient();
                    await c.ConnectAsync(_peerIp, ControlPort, ct);
                    AddConn(c);
                }
                catch { /* peer not up yet */ }
            }
            try { await Task.Delay(3000, ct); } catch { break; }
        }
    }

    private void AddConn(TcpClient c)
    {
        lock (_conns) _conns.Add(c);
        SendLine(c, $"HELLO {_myName}");
        _ = ReadLoop(c);
    }

    private async Task ReadLoop(TcpClient c)
    {
        try
        {
            using var reader = new StreamReader(c.GetStream(), Encoding.UTF8);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null) Handle(line, c);
        }
        catch { /* dropped */ }
        finally { lock (_conns) _conns.Remove(c); }
    }

    private void Handle(string line, TcpClient c)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return;
        if (parts[0] == "CLAIM" && parts[1] != _myName)
        {
            var ip = (c.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? _peerIp ?? "";
            Log?.Invoke($"peer \"{parts[1]}\" claimed control");
            PeerClaimedControl?.Invoke(parts[1], ip);
        }
    }

    /// <summary>Tell peers that this machine is now the active controller.</summary>
    public void BroadcastClaim()
    {
        List<TcpClient> snapshot;
        lock (_conns) snapshot = _conns.ToList();
        foreach (var c in snapshot) SendLine(c, $"CLAIM {_myName}");
    }

    private void SendLine(TcpClient c, string s)
    {
        try { var b = Encoding.UTF8.GetBytes(s + "\n"); c.GetStream().Write(b, 0, b.Length); }
        catch { /* dropped */ }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
        lock (_conns) { foreach (var c in _conns) c.Dispose(); _conns.Clear(); }
    }
}
