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

    /// <summary>Search common install locations for the deskflow server executable.</summary>
    public string? FindServerExe()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        };
        // deskflow has shipped the server under a few names across versions.
        var names = new[] { "deskflow-server.exe", "deskflow-core.exe", "deskflow.exe", "synergys.exe" };
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
    /// Write a deskflow .conf with this PC as the server screen and the tablet on
    /// the chosen side. Returns the config file path.
    /// </summary>
    public string WriteConfig(string pcName, string clientName, string side)
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

        var sb = new StringBuilder();
        sb.AppendLine("section: screens");
        sb.AppendLine($"\t{pcName}:");
        sb.AppendLine($"\t{clientName}:");
        sb.AppendLine("end");
        sb.AppendLine();
        sb.AppendLine("section: links");
        sb.AppendLine($"\t{pcName}:");
        sb.AppendLine($"\t\t{side} = {clientName}");
        sb.AppendLine($"\t{clientName}:");
        sb.AppendLine($"\t\t{opposite} = {pcName}");
        sb.AppendLine("end");
        sb.AppendLine();
        sb.AppendLine("section: options");
        sb.AppendLine("\tkeystroke(Alt+Tab) = switchInDirection(right)"); // harmless example option
        sb.AppendLine("end");

        var dir = Path.Combine(Path.GetTempPath(), "cozy");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "cozy.conf");
        File.WriteAllText(path, sb.ToString());
        return path;
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

        var pcName = Environment.MachineName;
        var config = WriteConfig(pcName, clientName, side);

        // Foreground (-f) so we can monitor it; no system tray; bind all interfaces.
        // TLS is toggled via deskflow's own setting; we pass the address + config here.
        var args = $"-f --no-tray --name \"{pcName}\" --address \":{port}\" -c \"{config}\"";

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
            Log?.Invoke($"Sharing started. Tablet should connect to this PC on port {port} as \"{clientName}\".");
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
