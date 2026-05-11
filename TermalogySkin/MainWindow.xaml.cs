using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Input;
using TermalogySkin.Services;

namespace TermalogySkin;

public partial class MainWindow : Window
{
    private const string RepoOwner = "techistad";
    private const string RepoName = "termalogy";

    private readonly StringBuilder _screenBuffer = new();
    private readonly List<string> _history = [];
    private readonly List<WindowSummary> _lastWindowList = [];
    private readonly AliasStore _aliasStore = new("TermalogySkin", "aliases.json");
    private readonly GitHubStatsService _gitHubStatsService = new();
    private readonly Dictionary<string, string> _aliases;
    private readonly Dictionary<string, string> _appShortcuts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"] = "chrome.exe",
        ["edge"] = "msedge.exe",
        ["vscode"] = "code.exe",
        ["code"] = "code.exe",
        ["notepad"] = "notepad.exe",
        ["calc"] = "calc.exe",
        ["terminal"] = "wt.exe",
        ["powershell"] = "pwsh.exe"
    };

    private string _workingDirectory;
    private int _historyIndex;
    private bool _commandRunning;

    public MainWindow()
    {
        InitializeComponent();

        _workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _aliases = _aliasStore.Load();
        _historyIndex = _history.Count;

        Activated += (_, _) => FocusCommandInput();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateStatusBar();
        AppendLine("Termalogy Skin v0.1");
        AppendLine("terminal-first launcher for Windows");
        AppendLine("type 'help' for commands");
        AppendLine("panic hotkey: Ctrl+Alt+Backspace");
        AppendLine(string.Empty);
        FocusCommandInput();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var hasCtrlAlt = Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt);
        var isBackspace = e.Key == Key.Back || (e.Key == Key.System && e.SystemKey == Key.Back);
        if (hasCtrlAlt && isBackspace)
        {
            AppendLine("panic hotkey triggered, exiting skin...");
            Close();
            e.Handled = true;
        }
    }

    private async void CommandInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Up)
        {
            MoveHistory(-1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            MoveHistory(1);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;

        if (_commandRunning)
        {
            return;
        }

        var input = CommandInput.Text.Trim();
        CommandInput.Clear();

        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        _history.Add(input);
        _historyIndex = _history.Count;

        AppendLine($"> {input}");

        _commandRunning = true;
        try
        {
            await ExecuteCommandAsync(input);
        }
        catch (Exception ex)
        {
            AppendLine($"error: {ex.Message}");
        }
        finally
        {
            _commandRunning = false;
            UpdateStatusBar();
            FocusCommandInput();
        }
    }

    private async Task ExecuteCommandAsync(string rawInput, int aliasDepth = 0)
    {
        var tokens = Tokenize(rawInput);
        if (tokens.Count == 0)
        {
            return;
        }

        if (_aliases.TryGetValue(tokens[0], out var aliasValue))
        {
            if (aliasDepth > 4)
            {
                AppendLine("error: alias expansion depth exceeded");
                return;
            }

            var suffix = tokens.Count > 1 ? " " + string.Join(' ', tokens.Skip(1)) : string.Empty;
            var expanded = aliasValue + suffix;
            AppendLine($"alias> {expanded}");
            await ExecuteCommandAsync(expanded, aliasDepth + 1);
            return;
        }

        var command = tokens[0].ToLowerInvariant();
        var args = tokens.Skip(1).ToArray();

        switch (command)
        {
            case "help":
                ShowHelp();
                break;
            case "clear":
            case "cls":
                _screenBuffer.Clear();
                OutputBlock.Text = string.Empty;
                break;
            case "exit":
            case "quit":
                Close();
                break;
            case "pwd":
                AppendLine(_workingDirectory);
                break;
            case "cd":
                ChangeDirectory(args);
                break;
            case "ls":
            case "dir":
                ListDirectory(args);
                break;
            case "open":
                OpenTarget(args);
                break;
            case "run":
                await RunShellCommandAsync(args);
                break;
            case "top":
                ToggleTopMode(args);
                break;
            case "win":
                ControlWindows(args);
                break;
            case "alias":
                ManageAliases(args);
                break;
            case "time":
                AppendLine(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
                break;
            case "date":
                AppendLine(DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                break;
            case "version":
                ShowVersion();
                break;
            case "about":
                ShowAbout();
                break;
            case "stats":
                await ShowStatsAsync();
                break;
            default:
                AppendLine($"unknown command: {command}");
                AppendLine("tip: use 'open <app>' or 'run <cmd>'");
                break;
        }
    }

    private void ShowHelp()
    {
        AppendLine("commands:");
        AppendLine("  help                     show this help");
        AppendLine("  open <app|url|path>      launch gui app, file, folder, or url");
        AppendLine("  run <cmd>                run a shell command and print output");
        AppendLine("  pwd | cd <path>          get/set working directory");
        AppendLine("  ls [path]                list files and folders");
        AppendLine("  win list                 list visible windows");
        AppendLine("  win close <index>        close window from latest list");
        AppendLine("  alias list               list aliases");
        AppendLine("  alias add <n> <cmd>      create alias");
        AppendLine("  alias remove <n>         remove alias");
        AppendLine("  alias path               show alias file location");
        AppendLine("  version                  show app version");
        AppendLine("  stats                    show GitHub usage metrics");
        AppendLine("  about                    show project links");
        AppendLine("  top on|off               keep skin window always on top");
        AppendLine("  clear | cls              clear terminal output");
        AppendLine("  exit | quit              close skin");
    }

    private void ChangeDirectory(string[] args)
    {
        var nextPath = args.Length == 0
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : ResolvePath(args[0]);

        if (!Directory.Exists(nextPath))
        {
            AppendLine($"error: path not found: {nextPath}");
            return;
        }

        _workingDirectory = Path.GetFullPath(nextPath);
        AppendLine($"cwd: {_workingDirectory}");
        UpdateStatusBar();
    }

    private void ListDirectory(string[] args)
    {
        var target = args.Length == 0 ? _workingDirectory : ResolvePath(args[0]);

        if (!Directory.Exists(target))
        {
            AppendLine($"error: path not found: {target}");
            return;
        }

        AppendLine($"listing: {target}");

        var dirs = Directory.GetDirectories(target)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
        var files = Directory.GetFiles(target)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

        foreach (var dir in dirs)
        {
            AppendLine($"[d] {Path.GetFileName(dir)}");
        }

        foreach (var file in files)
        {
            var info = new FileInfo(file);
            AppendLine($"[f] {info.Name} ({info.Length} B)");
        }
    }

    private void OpenTarget(string[] args)
    {
        if (args.Length == 0)
        {
            AppendLine("usage: open <app|url|path>");
            return;
        }

        var target = string.Join(' ', args);
        var launchTarget = ResolveOpenTarget(target);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = launchTarget,
                WorkingDirectory = _workingDirectory,
                UseShellExecute = true
            });
            AppendLine($"opened: {target}");
        }
        catch
        {
            if (!launchTarget.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = launchTarget + ".exe",
                        WorkingDirectory = _workingDirectory,
                        UseShellExecute = true
                    });
                    AppendLine($"opened: {target}");
                    return;
                }
                catch
                {
                    // Falls through to final message.
                }
            }

            AppendLine($"error: could not open {target}");
        }
    }

    private async Task RunShellCommandAsync(string[] args)
    {
        if (args.Length == 0)
        {
            AppendLine("usage: run <cmd>");
            return;
        }

        var shellCommand = string.Join(' ', args);

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {shellCommand}",
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        if (!string.IsNullOrWhiteSpace(stdOut))
        {
            foreach (var line in stdOut.Split(["\r\n", "\n"], StringSplitOptions.None))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    AppendLine(line);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(stdErr))
        {
            foreach (var line in stdErr.Split(["\r\n", "\n"], StringSplitOptions.None))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    AppendLine($"err: {line}");
                }
            }
        }

        AppendLine($"exit code: {process.ExitCode}");
    }

    private void ToggleTopMode(string[] args)
    {
        if (args.Length == 0)
        {
            AppendLine($"top mode: {(Topmost ? "on" : "off")}");
            return;
        }

        var value = args[0].ToLowerInvariant();
        if (value is not ("on" or "off"))
        {
            AppendLine("usage: top on|off");
            return;
        }

        Topmost = value == "on";
        AppendLine($"top mode: {value}");
    }

    private void ControlWindows(string[] args)
    {
        if (args.Length == 0 || args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            _lastWindowList.Clear();
            _lastWindowList.AddRange(WindowController.ListVisibleWindows(Process.GetCurrentProcess().ProcessName));

            if (_lastWindowList.Count == 0)
            {
                AppendLine("no visible windows found");
                return;
            }

            for (var i = 0; i < _lastWindowList.Count; i++)
            {
                var window = _lastWindowList[i];
                AppendLine($"{i + 1}. [{window.ProcessName}] {window.Title}");
            }

            return;
        }

        if (args[0].Equals("close", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2 || !int.TryParse(args[1], out var index))
            {
                AppendLine("usage: win close <index>");
                return;
            }

            if (_lastWindowList.Count == 0)
            {
                AppendLine("window list is empty. run 'win list' first");
                return;
            }

            if (index < 1 || index > _lastWindowList.Count)
            {
                AppendLine($"error: index out of range (1-{_lastWindowList.Count})");
                return;
            }

            var target = _lastWindowList[index - 1];
            var closed = WindowController.CloseWindow(target.Handle);
            AppendLine(closed
                ? $"close signal sent: {target.Title}"
                : "error: failed to send close signal");
            return;
        }

        AppendLine("usage: win list | win close <index>");
    }

    private void ManageAliases(string[] args)
    {
        if (args.Length == 0 || args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            if (_aliases.Count == 0)
            {
                AppendLine("no aliases saved");
                return;
            }

            foreach (var (name, command) in _aliases.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                AppendLine($"{name} -> {command}");
            }

            return;
        }

        if (args[0].Equals("add", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 3)
            {
                AppendLine("usage: alias add <name> <command>");
                return;
            }

            var name = args[1].Trim();
            if (name.Contains(' '))
            {
                AppendLine("error: alias name cannot contain spaces");
                return;
            }

            var command = string.Join(' ', args.Skip(2));
            _aliases[name] = command;
            _aliasStore.Save(_aliases);
            AppendLine($"alias saved: {name} -> {command}");
            return;
        }

        if (args[0].Equals("remove", StringComparison.OrdinalIgnoreCase) || args[0].Equals("rm", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2)
            {
                AppendLine("usage: alias remove <name>");
                return;
            }

            if (_aliases.Remove(args[1]))
            {
                _aliasStore.Save(_aliases);
                AppendLine($"alias removed: {args[1]}");
            }
            else
            {
                AppendLine($"error: alias not found: {args[1]}");
            }

            return;
        }

        if (args[0].Equals("path", StringComparison.OrdinalIgnoreCase))
        {
            AppendLine(_aliasStore.PathOnDisk);
            return;
        }

        AppendLine("usage: alias list | alias add <n> <cmd> | alias remove <n> | alias path");
    }

    private string ResolvePath(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || inputPath == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        var expandedPath = inputPath.StartsWith('~')
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), inputPath[1..].TrimStart('\\', '/'))
            : inputPath;

        if (Path.IsPathRooted(expandedPath))
        {
            return Path.GetFullPath(expandedPath);
        }

        return Path.GetFullPath(Path.Combine(_workingDirectory, expandedPath));
    }

    private string ResolveOpenTarget(string rawTarget)
    {
        if (_appShortcuts.TryGetValue(rawTarget, out var mapped))
        {
            return mapped;
        }

        if (rawTarget.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            rawTarget.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return rawTarget;
        }

        var resolvedPath = ResolvePath(rawTarget);
        return File.Exists(resolvedPath) || Directory.Exists(resolvedPath)
            ? resolvedPath
            : rawTarget;
    }

    private void MoveHistory(int delta)
    {
        if (_history.Count == 0)
        {
            return;
        }

        _historyIndex = Math.Clamp(_historyIndex + delta, 0, _history.Count);

        CommandInput.Text = _historyIndex == _history.Count
            ? string.Empty
            : _history[_historyIndex];
        CommandInput.CaretIndex = CommandInput.Text.Length;
    }

    private static List<string> Tokenize(string input)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(input))
        {
            return result;
        }

        var token = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in input)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (token.Length > 0)
                {
                    result.Add(token.ToString());
                    token.Clear();
                }

                continue;
            }

            token.Append(ch);
        }

        if (token.Length > 0)
        {
            result.Add(token.ToString());
        }

        return result;
    }

    private void ShowVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "unknown";
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        AppendLine($"version: {version}");
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            AppendLine($"build: {infoVersion}");
        }
    }

    private void ShowAbout()
    {
        AppendLine("Termalogy Skin");
        AppendLine("repo: https://github.com/techistad/termalogy");
        AppendLine("releases: https://github.com/techistad/termalogy/releases");
        AppendLine("contributing: https://github.com/techistad/termalogy/blob/main/CONTRIBUTING.md");
    }

    private async Task ShowStatsAsync()
    {
        try
        {
            AppendLine("fetching repository stats...");
            var stats = await _gitHubStatsService.GetRepositoryStatsAsync(RepoOwner, RepoName);
            AppendLine($"stars: {stats.Stars}");
            AppendLine($"forks: {stats.Forks}");
            AppendLine($"watchers: {stats.Watchers}");
            AppendLine($"open issues: {stats.OpenIssues}");
            AppendLine($"release downloads (total): {stats.TotalReleaseDownloads}");
        }
        catch (Exception ex)
        {
            AppendLine($"error: failed to fetch stats ({ex.Message})");
        }
    }

    private void AppendLine(string line)
    {
        _screenBuffer.AppendLine(line);
        OutputBlock.Text = _screenBuffer.ToString();
        OutputScrollViewer.ScrollToEnd();
    }

    private void UpdateStatusBar()
    {
        CurrentDirectoryText.Text = _workingDirectory;
        HintText.Text = $"panic: Ctrl+Alt+Backspace | top: {(Topmost ? "on" : "off")}";
    }

    private void FocusCommandInput()
    {
        CommandInput.Focus();
        CommandInput.CaretIndex = CommandInput.Text.Length;
    }
}
