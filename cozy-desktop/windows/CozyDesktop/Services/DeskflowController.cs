using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CozyDesktop.Services;

/// <summary>
/// An auxiliary device (Mac, tablet…), which edge it sits on, and which screen
/// it attaches to. RelativeTo is the screen name it borders; empty means the PC.
/// Attaching to another device enables chaining (device-beyond-device), so the
/// number of devices is unlimited.
/// </summary>
public record Device(string Name, string Side, string RelativeTo = "");

/// <summary>
/// Hides deskflow entirely. Cozy presents its own UI; this class finds the
/// deskflow server binary, writes its config from a friendly layout, and
/// runs/stops/monitors it as a background process. The user never sees deskflow.
/// </summary>
public sealed class DeskflowController
{
    public event Action<string>? Log;
    public event Action<bool>? RunningChanged;

    private Process? _proc;

    public enum EngineRole { None, Server, Client }
    public EngineRole Role { get; private set; } = EngineRole.None;

    public bool IsRunning => _proc is { HasExited: false };

    /// <summary>Search common install locations for the deskflow core engine.</summary>
    public string? FindServerExe()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        };
        // deskflow 1.26+ ships a single core engine driven by `server`/`client` subcommands.
        // Older fallbacks kept for safety.
        var names = new[] { "deskflow-core.exe", "deskflow-server.exe", "synergys.exe" };
        foreach (var root in roots)
        {
            var dir = Path.Combine(root, "Deskflow");
            if (!Directory.Exists(dir)) continue;
            foreach (var name in names)
            {
                try
                {
                    var hit = Directory.GetFiles(dir, name, SearchOption.AllDirectories).FirstOrDefault();
                    if (hit != null) return hit;
                }
                catch { /* ignore unreadable dirs */ }
            }
        }
        return null;
    }

    public bool IsDeskflowInstalled() => FindServerExe() != null;

    /// <summary>
    /// deskflow 1.26 needs TWO files: a Qt-INI *settings* file (mode/port/name/tls +
    /// a pointer to the screen config) and the classic screens/links *.conf*. This
    /// writes both into %LOCALAPPDATA%\Cozy and returns the settings file path,
    /// which is passed to the engine via `server -s <settings>`.
    ///
    /// Verified working against deskflow-core.exe 1.26.0 (server binds :24800).
    /// </summary>
    public string WriteConfig(string pcName, IReadOnlyList<Device> clients, int port, bool tls)
    {
        static string Opposite(string side) => side switch
        {
            "left" => "right",
            "right" => "left",
            "up" => "down",
            "down" => "up",
            _ => "right"
        };

        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cozy");
        Directory.CreateDirectory(dir);

        // --- screens / links config (classic .conf) ---
        // PC sits in the middle; each device hangs off one of its edges. The cursor
        // can cross to ALL of them, since deskflow supports many screens at once.
        var conf = new StringBuilder();
        conf.AppendLine("section: screens");
        conf.AppendLine($"\t{pcName}:");
        foreach (var c in clients)
            conf.AppendLine($"\t{c.Name}:");
        conf.AppendLine("end");

        // Aggregate links per screen. Each device borders RelativeTo (PC by default,
        // or another device for chaining); we add the forward and return edges.
        var links = new Dictionary<string, List<(string side, string neighbor)>>();
        void AddLink(string from, string side, string to)
        {
            if (!links.TryGetValue(from, out var list)) { list = new(); links[from] = list; }
            if (list.Any(x => x.side == side)) return; // one neighbour per edge (chain for more)
            list.Add((side, to));
        }
        foreach (var c in clients)
        {
            var rel = string.IsNullOrEmpty(c.RelativeTo) ? pcName : c.RelativeTo;
            AddLink(rel, c.Side, c.Name);              // neighbour's edge -> device
            AddLink(c.Name, Opposite(c.Side), rel);    // device's return edge -> neighbour
        }
        conf.AppendLine("section: links");
        foreach (var kv in links)
        {
            conf.AppendLine($"\t{kv.Key}:");
            foreach (var (side, neighbor) in kv.Value)
                conf.AppendLine($"\t\t{side} = {neighbor}");
        }
        conf.AppendLine("end");

        var confPath = Path.Combine(dir, "cozy-server.conf");
        File.WriteAllText(confPath, conf.ToString());

        // --- Qt-INI settings file (paths use forward slashes) ---
        var confPathFwd = confPath.Replace('\\', '/');
        var ini = new StringBuilder();
        ini.AppendLine("[core]");
        ini.AppendLine("coreMode=server");
        ini.AppendLine($"port={port}");
        ini.AppendLine($"computerName={pcName}");
        ini.AppendLine("[security]");
        ini.AppendLine($"tlsEnabled={(tls ? "true" : "false")}");
        ini.AppendLine("[server]");
        ini.AppendLine("externalConfig=true");
        ini.AppendLine($"externalConfigFile={confPathFwd}");
        var settingsPath = Path.Combine(dir, "cozy-settings.conf");
        File.WriteAllText(settingsPath, ini.ToString());

        return settingsPath;
    }

    /// <summary>deskflow screen names must be lowercase, no spaces.</summary>
    public static string SanitizeName(string name)
    {
        var s = new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray());
        return string.IsNullOrWhiteSpace(s) ? "pc" : s;
    }

    /// <summary>This PC's screen name in the deskflow config.</summary>
    public string PcName => SanitizeName(Environment.MachineName);

    /// <summary>
    /// deskflow needs a TLS cert when encryption is on, and only its GUI generates
    /// one. Cozy makes its own self-signed PEM (PKCS8 key + cert, CN=Deskflow — the
    /// format deskflow writes) in a user-writable spot, so encryption works headless.
    /// Verified: the engine loads this and completes the TLS handshake.
    /// </summary>
    public string EnsureCertificate()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cozy");
        Directory.CreateDirectory(dir);
        var pem = Path.Combine(dir, "cozy.pem");
        if (File.Exists(pem) && new FileInfo(pem).Length > 0) return pem;

        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Deskflow", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
        var sb = new StringBuilder();
        sb.AppendLine(rsa.ExportPkcs8PrivateKeyPem());
        sb.AppendLine(cert.ExportCertificatePem());
        File.WriteAllText(pem, sb.ToString());
        return pem;
    }

    /// <summary>
    /// Write config from an explicit link graph (used by the visual layout canvas).
    /// <paramref name="deviceNames"/> excludes the PC; <paramref name="links"/> are
    /// (fromScreen, side, toScreen) edges using the real screen names (PcName for the PC).
    /// </summary>
    public string WriteConfigGraph(IReadOnlyList<string> deviceNames,
                                   IReadOnlyList<(string from, string side, string to)> links,
                                   int port, bool tls)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cozy");
        Directory.CreateDirectory(dir);

        var conf = new StringBuilder();
        conf.AppendLine("section: screens");
        conf.AppendLine($"\t{PcName}:");
        foreach (var n in deviceNames)
            conf.AppendLine($"\t{n}:");
        conf.AppendLine("end");

        // group links per screen, one neighbour per edge
        var byScreen = new Dictionary<string, List<(string side, string to)>>();
        foreach (var (from, side, to) in links)
        {
            if (!byScreen.TryGetValue(from, out var l)) { l = new(); byScreen[from] = l; }
            if (l.Any(x => x.side == side)) continue;
            l.Add((side, to));
        }
        conf.AppendLine("section: links");
        foreach (var kv in byScreen)
        {
            conf.AppendLine($"\t{kv.Key}:");
            foreach (var (side, to) in kv.Value)
                conf.AppendLine($"\t\t{side} = {to}");
        }
        conf.AppendLine("end");

        var confPath = Path.Combine(dir, "cozy-server.conf");
        File.WriteAllText(confPath, conf.ToString());

        var ini = new StringBuilder();
        ini.AppendLine("[core]");
        ini.AppendLine("coreMode=server");
        ini.AppendLine($"port={port}");
        ini.AppendLine($"computerName={PcName}");
        ini.AppendLine("[security]");
        ini.AppendLine($"tlsEnabled={(tls ? "true" : "false")}");
        if (tls) ini.AppendLine($"certificate={EnsureCertificate().Replace('\\', '/')}");
        ini.AppendLine("[server]");
        ini.AppendLine("externalConfig=true");
        ini.AppendLine($"externalConfigFile={confPath.Replace('\\', '/')}");
        var settingsPath = Path.Combine(dir, "cozy-settings.conf");
        File.WriteAllText(settingsPath, ini.ToString());
        return settingsPath;
    }

    /// <summary>Start the server from a visual-layout link graph.</summary>
    public void StartGraph(IReadOnlyList<string> deviceNames,
                           IReadOnlyList<(string from, string side, string to)> links,
                           int port, bool tls)
    {
        if (IsRunning) return;
        if (FindServerExe() == null) { Log?.Invoke("deskflow engine not found. Click 'Set up Cozy' first."); return; }
        if (deviceNames.Count == 0) { Log?.Invoke("Add at least one device to share with."); return; }

        var settings = WriteConfigGraph(deviceNames, links, port, tls);
        Role = EngineRole.Server;
        LaunchEngine($"server -s \"{settings}\" --new-instance",
                     $"Sharing on port {port} with: {string.Join(", ", deviceNames)}.");
    }

    /// <summary>
    /// Run this machine as a CLIENT of another controller (used by multi-controller
    /// handoff: when another desktop is driving, we become a receiver).
    /// </summary>
    public void StartClient(string serverIp, int port, string screenName, bool tls)
    {
        if (IsRunning) return;
        if (FindServerExe() == null) { Log?.Invoke("deskflow engine not found."); return; }

        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cozy");
        Directory.CreateDirectory(dir);
        var ini = new StringBuilder();
        ini.AppendLine("[core]");
        ini.AppendLine("coreMode=client");
        ini.AppendLine($"computerName={SanitizeName(screenName)}");
        ini.AppendLine($"port={port}");
        ini.AppendLine("[client]");
        ini.AppendLine($"remoteHost={serverIp}");
        ini.AppendLine("[security]");
        ini.AppendLine($"tlsEnabled={(tls ? "true" : "false")}");
        var settings = Path.Combine(dir, "cozy-client-settings.conf");
        File.WriteAllText(settings, ini.ToString());

        Role = EngineRole.Client;
        LaunchEngine($"client -s \"{settings}\" --new-instance",
                     $"Receiving from controller at {serverIp}:{port} as \"{SanitizeName(screenName)}\".");
    }

    /// <summary>Launch deskflow-core with the given arguments (server or client).</summary>
    private void LaunchEngine(string args, string summary)
    {
        var exe = FindServerExe()!;
        Log?.Invoke($"Launching engine: {Path.GetFileName(exe)} {args}");

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) Log?.Invoke(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) Log?.Invoke(e.Data); };
        p.Exited += (_, _) => { Log?.Invoke("Engine stopped."); RunningChanged?.Invoke(false); };
        try
        {
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            _proc = p;
            RunningChanged?.Invoke(true);
            Log?.Invoke(summary);
        }
        catch (Exception ex) { Log?.Invoke($"Failed to start engine: {ex.Message}"); }
    }

    /// <summary>Start the deskflow server in the background, sharing with all devices.</summary>
    public void Start(IReadOnlyList<Device> clients, int port, bool tls)
    {
        if (IsRunning) return;

        var exe = FindServerExe();
        if (exe == null)
        {
            Log?.Invoke("deskflow engine not found. Click 'Set up Cozy' first.");
            return;
        }
        if (clients.Count == 0)
        {
            Log?.Invoke("Add at least one device to share with.");
            return;
        }

        var pcName = SanitizeName(Environment.MachineName);
        var settings = WriteConfig(pcName, clients, port, tls);

        // deskflow 1.26: `server -s <settings> --new-instance`. The settings file
        // carries port/name/tls and points at the screens config. Verified working.
        var args = $"server -s \"{settings}\" --new-instance";

        Log?.Invoke($"Launching engine:\n{Path.GetFileName(exe)} {args}");

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) Log?.Invoke(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) Log?.Invoke(e.Data); };
        p.Exited += (_, _) =>
        {
            Log?.Invoke("Engine stopped.");
            RunningChanged?.Invoke(false);
        };

        try
        {
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            _proc = p;
            RunningChanged?.Invoke(true);
            var names = string.Join(", ", clients.Select(c => $"\"{c.Name}\" ({c.Side})"));
            Log?.Invoke($"Sharing started on port {port}. Connect these devices: {names}.");
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Failed to start engine: {ex.Message}");
        }
    }

    public void Stop()
    {
        try
        {
            if (_proc is { HasExited: false })
            {
                _proc.Kill(entireProcessTree: true);
                _proc.WaitForExit(3000);
            }
        }
        catch { /* ignore */ }
        finally
        {
            _proc = null;
            Role = EngineRole.None;
            RunningChanged?.Invoke(false);
        }
    }
}
