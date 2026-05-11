using Microsoft.Win32;

namespace TermalogySkin.Services;

public sealed class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _valueName;

    public StartupManager(string valueName)
    {
        _valueName = valueName;
    }

    public bool IsEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        var value = runKey?.GetValue(_valueName) as string;
        return !string.IsNullOrWhiteSpace(value);
    }

    public string? GetCommandLine()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return runKey?.GetValue(_valueName) as string;
    }

    public void Enable(string executablePath, string startupArgument = "--startup")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var command = $"\"{executablePath}\" {startupArgument}";

        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        runKey?.SetValue(_valueName, command);
    }

    public void Disable()
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        runKey?.DeleteValue(_valueName, false);
    }
}
