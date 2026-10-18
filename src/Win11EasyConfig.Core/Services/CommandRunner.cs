using System.Diagnostics;
using System.Text;
using Win11EasyConfig.Core.Abstractions;

namespace Win11EasyConfig.Core.Services;

/// <summary>
/// Runs console commands without ever showing a window. Output is captured asynchronously so a
/// chatty command (DISM, netsh) cannot deadlock by filling the pipe buffer.
/// </summary>
public sealed class CommandRunner : ICommandRunner
{
    public Task<CommandResult> RunCmdAsync(string command, CancellationToken cancellationToken = default) =>
        RunAsync("cmd.exe", $"/d /c {command}", cancellationToken);

    public Task<CommandResult> RunPowerShellAsync(string command, CancellationToken cancellationToken = default) =>
        RunAsync("powershell.exe", $"-NoLogo -NoProfile -NonInteractive -Command \"{command}\"", cancellationToken);

    public void Launch(string fileName, string? arguments = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }

    private static async Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        if (!process.Start())
        {
            return new CommandResult(-1, string.Empty, $"Failed to start {fileName}.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return new CommandResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort only.
        }
    }
}
