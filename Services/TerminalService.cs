using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SumGravity.Services;

public class TerminalService
{
    private readonly ILogger<TerminalService> _logger;

    // Track active processes for kill support
    private readonly Dictionary<Guid, Process> _activeProcesses = new();

    public TerminalService(ILogger<TerminalService> logger)
    {
        _logger = logger;
    }

    // ── Stream command output ──────────────────────────────────
    public async IAsyncEnumerable<string> RunCommandStreamAsync(
        string command,
        string? workingDirectory = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var processId = Guid.NewGuid();

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -Command \"{EscapeForPs(command)}\"",
            WorkingDirectory = workingDirectory ?? @"c:\dev",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi };
        process.EnableRaisingEvents = true;

        // Channel to pipe both stdout + stderr in order
        var channel = System.Threading.Channels.Channel.CreateUnbounded<string>();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                channel.Writer.TryWrite(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                channel.Writer.TryWrite($"ERR: {e.Data}");
        };

        process.Exited += (_, _) => channel.Writer.TryComplete();

        try
        {
            process.Start();
            _activeProcesses[processId] = process;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await foreach (var line in channel.Reader.ReadAllAsync(ct))
            {
                yield return line;
            }

            await process.WaitForExitAsync(ct);
            yield return $"\n[Exit code: {process.ExitCode}]";
        }
        finally
        {
            _activeProcesses.Remove(processId);
            try { process.Kill(entireProcessTree: true); } catch { }
            process.Dispose();
        }
    }

    // ── Run a PowerShell script file ──────────────────────────
    public IAsyncEnumerable<string> RunScriptAsync(
        string scriptPath,
        string? workingDirectory = null,
        CancellationToken ct = default)
    {
        return RunCommandStreamAsync(
            $"& '{scriptPath}'",
            workingDirectory ?? Path.GetDirectoryName(scriptPath),
            ct);
    }

    // ── One-shot run (returns full output) ────────────────────
    public async Task<(string Output, int ExitCode)> RunAsync(
        string command,
        string? workingDirectory = null,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -Command \"{EscapeForPs(command)}\"",
            WorkingDirectory = workingDirectory ?? @"c:\dev",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var combined = string.IsNullOrEmpty(stderr)
            ? stdout
            : $"{stdout}\nSTDERR: {stderr}";

        return (combined, process.ExitCode);
    }

    private static string EscapeForPs(string command)
        => command.Replace("\"", "\\\"");
}
