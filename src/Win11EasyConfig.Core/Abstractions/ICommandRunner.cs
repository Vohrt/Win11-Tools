namespace Win11EasyConfig.Core.Abstractions;

/// <summary>Result of running an external command.</summary>
public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public string Output => StandardOutput + StandardError;

    public bool Contains(string value) =>
        Output.Contains(value, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Runs console commands (cmd.exe / powershell.exe) and shell verbs.
/// </summary>
public interface ICommandRunner
{
    Task<CommandResult> RunCmdAsync(string command, CancellationToken cancellationToken = default);

    Task<CommandResult> RunPowerShellAsync(string command, CancellationToken cancellationToken = default);

    /// <summary>Launches a process without waiting (shell execute, e.g. control panel applets).</summary>
    void Launch(string fileName, string? arguments = null);
}
