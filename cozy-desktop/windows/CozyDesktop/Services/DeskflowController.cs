using System.Diagnostics;
using System.IO;
using System.Text;

namespace CozyDesktop.Services;

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
    public string WriteConfig(string pcName, string clientName, string side, int port, bool tls)
    {
        // deskflow link sides are left/right/up/down; opposite gets the return path.
        string opposite = side switch
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
        var conf = new StringBuilder();
        conf.AppendLine("section: screens");
        conf.AppendLine($"\t{pcName}:");
        conf.AppendLine($"\t{clientName}:");
        conf.AppendLine("end");
        conf.AppendLine("section: links");
        conf.AppendLine($"\t{pcName}:");
        conf.AppendLine($"\t\t{side} = {clientName}");
        conf.AppendLine($"\t{clientName}:");
        conf.AppendLine($"\t\t{opposite} = {pcName}");
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

    /// <summary>Start the deskflow server in the background.</summary>
    public void Start(string clientName, string side, int port, bool tls)
    {
        if (IsRunning) return;

        var exe = FindServerExe();
        if (exe == null)
        {
            Log?.Invoke("deskflow engine not found. Click 'Install engine' first.");
            return;
        }

        var pcName = SanitizeName(Environment.MachineName);
        var clientScreen = SanitizeName(clientName);
        var settings = WriteConfig(pcName, clientScreen, side, port, tls);

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
            Log?.Invoke($"Sharing started. Tablet should connect to this PC on port {port} as \"{clientScreen}\".");
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
            RunningChanged?.Invoke(false);
        }
    }
}
