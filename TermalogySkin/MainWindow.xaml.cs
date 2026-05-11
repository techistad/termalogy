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
    private const string StartupArg = "--startup";

    private readonly StringBuilder _screenBuffer = new();
    private readonly List<string> _history = [];
    private readonly List<WindowSummary> _lastWindowList = [];
    private readonly AliasStore _aliasStore = new("TermalogySkin", "aliases.json");
    private readonly AppSettingsStore _settingsStore = new("TermalogySkin", "settings.json");
    private readonly StartupManager _startupManager = new("TermalogySkin");
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
        ["powershell"] = "pwsh.exe",
        ["cmd"] = "cmd.exe",
        ["explorer"] = "explorer.exe",
        ["taskmgr"] = "taskmgr.exe",
        ["paint"] = "mspaint.exe",
        ["firefox"] = "firefox.exe",
        ["brave"] = "brave.exe"
    };

    private readonly bool _isStartupLaunch;
    private AppSettings _settings;
    private string _workingDirectory;
    private int _historyIndex;
    private bool _commandRunning;
    private bool _taskbarHiddenBySkin;

    public MainWindow()
    {
        InitializeComponent();

        _workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _aliases = _aliasStore.Load();
        _settings = _settingsStore.Load();
        _historyIndex = _history.Count;
        _isStartupLaunch = Environment.GetCommandLineArgs()
            .Any(arg => arg.Equals(StartupArg, StringComparison.OrdinalIgnoreCase));

        Activated += (_, _) => FocusCommandInput();
        Closed += Window_Closed;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        UpdateStatusBar();
        AppendLine($"Termalogy Skin v{version}");
        AppendLine("terminal-first launcher for Windows");
        AppendLine("type 'help' for commands");
        AppendLine("panic hotkey: Ctrl+Alt+Backspace");

        if (_isStartupLaunch)
        {
            AppendLine("startup launch detected");
        }

        if (_settings.StealthMode)
        {
            SetStealthMode(true, persist: false, announce: true);
        }

        AppendLine(string.Empty);
        FocusCommandInput();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        if (_taskbarHiddenBySkin)
        {
            TaskbarManager.SetVisible(true);
            _taskbarHiddenBySkin = false;
        }
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
            case "apps":
                ManageAppShortcuts(args);
                break;
            case "run":
                await RunShellCommandAsync(args);
                break;
            case "top":
                ToggleTopMode(args);
                break;
            case "startup":
                ManageStartup(args);
                break;
            case "stealth":
                ManageStealthMode(args);
                break;
            case "taskbar":
                ManageTaskbar(args);
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
        AppendLine("  help                      show this help");
        AppendLine("  open <app|url|path>       launch gui app, file, folder, or url");
        AppendLine("  apps list                 list built-in launcher shortcuts");
        AppendLine("  apps find <query>         fuzzy-find launcher shortcuts");
        AppendLine("  run <cmd>                 run a shell command and print output");
        AppendLine("  pwd | cd <path>           get/set working directory");
        AppendLine("  ls [path]                 list files and folders");
        AppendLine("  win list                  list visible windows");
        AppendLine("  win close <index>         close window from latest list");
        AppendLine("  alias list                list aliases");
        AppendLine("  alias add <n> <cmd>       create alias");
        AppendLine("  alias remove <n>          remove alias");
        AppendLine("  alias path                show alias file location");
        AppendLine("  startup on|off|status     configure launch at sign-in");
        AppendLine("  stealth on|off|status     hide taskbar while skin is active");
        AppendLine("  taskbar hide|show|status  manual taskbar visibility");
        AppendLine("  top on|off                keep skin window always on top");
        AppendLine("  version                   show app version");
        AppendLine("  stats                     show GitHub usage metrics");
        AppendLine("  about                     show project links");
        AppendLine("  clear | cls               clear terminal output");
        AppendLine("  exit | quit               close skin");
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
        var resolution = ResolveOpenTarget(target);

        if (resolution.LaunchTarget is null)
        {
            if (resolution.Suggestions.Count > 0)
            {
                AppendLine($"no exact match for '{target}'. suggestions:");
                foreach (var suggestion in resolution.Suggestions)
                {
                    AppendLine($"  - {suggestion}");
                }

                return;
            }

            AppendLine($"error: could not resolve target: {target}");
            return;
        }

        var launchTarget = resolution.LaunchTarget;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = launchTarget,
                WorkingDirectory = _workingDirectory,
                UseShellExecute = true
            });
            AppendLine($"opened: {target}");

            if (!string.IsNullOrWhiteSpace(resolution.MatchedShortcut) &&
                !resolution.MatchedShortcut.Equals(target, StringComparison.OrdinalIgnoreCase))
            {
                AppendLine($"fuzzy> {target} -> {resolution.MatchedShortcut}");
            }
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
                    // Falls through to final error.
                }
            }

            if (resolution.Suggestions.Count > 0)
            {
                AppendLine("did you mean:");
                foreach (var suggestion in resolution.Suggestions)
                {
                    AppendLine($"  - {suggestion}");
                }
            }

            AppendLine($"error: could not open {target}");
        }
    }

    private void ManageAppShortcuts(string[] args)
    {
        if (args.Length == 0 || args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            AppendLine("launcher shortcuts:");
            foreach (var key in _appShortcuts.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
            {
                AppendLine($"  {key} -> {_appShortcuts[key]}");
            }

            return;
        }

        if (args[0].Equals("find", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2)
            {
                AppendLine("usage: apps find <query>");
                return;
            }

            var query = string.Join(' ', args.Skip(1));
            var matches = FuzzyMatcher.Rank(query, _appShortcuts.Keys, 8);
            if (matches.Count == 0)
            {
                AppendLine("no app shortcut matches found");
                return;
            }

            AppendLine($"matches for '{query}':");
            foreach (var match in matches)
            {
                AppendLine($"  {match} -> {_appShortcuts[match]}");
            }

            return;
        }

        AppendLine("usage: apps list | apps find <query>");
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

    private void ManageStartup(string[] args)
    {
        if (args.Length == 0 || args[0].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            var enabled = _startupManager.IsEnabled();
            AppendLine($"startup: {(enabled ? "on" : "off")}");
            var commandLine = _startupManager.GetCommandLine();
            if (!string.IsNullOrWhiteSpace(commandLine))
            {
                AppendLine($"run key command: {commandLine}");
            }

            return;
        }

        if (args[0].Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            var processPath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                AppendLine("error: unable to resolve process path for startup entry");
                return;
            }

            _startupManager.Enable(processPath, StartupArg);
            AppendLine("startup enabled");
            return;
        }

        if (args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            _startupManager.Disable();
            AppendLine("startup disabled");
            return;
        }

        AppendLine("usage: startup on|off|status");
    }

    private void ManageStealthMode(string[] args)
    {
        if (args.Length == 0 || args[0].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            AppendLine($"stealth mode: {(_settings.StealthMode ? "on" : "off")}");
            AppendLine($"taskbar visible: {(TaskbarManager.IsVisible() ? "yes" : "no")}");
            return;
        }

        if (args[0].Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            SetStealthMode(true, persist: true, announce: true);
            return;
        }

        if (args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            SetStealthMode(false, persist: true, announce: true);
            return;
        }

        AppendLine("usage: stealth on|off|status");
    }

    private void ManageTaskbar(string[] args)
    {
        if (args.Length == 0 || args[0].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            AppendLine($"taskbar visible: {(TaskbarManager.IsVisible() ? "yes" : "no")}");
            AppendLine($"managed by skin: {(_taskbarHiddenBySkin ? "yes" : "no")}");
            return;
        }

        if (args[0].Equals("hide", StringComparison.OrdinalIgnoreCase))
        {
            var hidden = HideTaskbarFromSkin();
            AppendLine(hidden ? "taskbar hidden" : "warning: could not hide taskbar");
            return;
        }

        if (args[0].Equals("show", StringComparison.OrdinalIgnoreCase))
        {
            ShowTaskbarFromSkin();
            AppendLine("taskbar shown");
            return;
        }

        AppendLine("usage: taskbar hide|show|status");
    }

    private void SetStealthMode(bool enabled, bool persist, bool announce)
    {
        if (enabled)
        {
            var hidden = HideTaskbarFromSkin();
            if (persist)
            {
                _settings.StealthMode = true;
                _settingsStore.Save(_settings);
            }

            if (announce)
            {
                AppendLine(hidden
                    ? "stealth mode enabled"
                    : "warning: stealth mode requested, but taskbar could not be hidden");
            }

            return;
        }

        ShowTaskbarFromSkin();
        if (persist)
        {
            _settings.StealthMode = false;
            _settingsStore.Save(_settings);
        }

        if (announce)
        {
            AppendLine("stealth mode disabled");
        }
    }

    private bool HideTaskbarFromSkin()
    {
        if (_taskbarHiddenBySkin)
        {
            return true;
        }

        var changed = TaskbarManager.SetVisible(false);
        if (changed || !TaskbarManager.IsVisible())
        {
            _taskbarHiddenBySkin = true;
            return true;
        }

        return false;
    }

    private void ShowTaskbarFromSkin()
    {
        if (!_taskbarHiddenBySkin)
        {
            _ = TaskbarManager.SetVisible(true);
            return;
        }

        _ = TaskbarManager.SetVisible(true);
        _taskbarHiddenBySkin = false;
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

    private OpenResolution ResolveOpenTarget(string rawTarget)
    {
        if (_appShortcuts.TryGetValue(rawTarget, out var mapped))
        {
            return new OpenResolution(mapped, rawTarget, []);
        }

        if (rawTarget.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            rawTarget.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new OpenResolution(rawTarget, null, []);
        }

        var resolvedPath = ResolvePath(rawTarget);
        if (File.Exists(resolvedPath) || Directory.Exists(resolvedPath))
        {
            return new OpenResolution(resolvedPath, null, []);
        }

        if (rawTarget.Contains('\\') || rawTarget.Contains('/') ||
            (rawTarget.Contains(':') && rawTarget.Length > 1))
        {
            return new OpenResolution(rawTarget, null, []);
        }

        var suggestions = FuzzyMatcher.Rank(rawTarget, _appShortcuts.Keys, 5);
        if (suggestions.Count == 1)
        {
            var suggestion = suggestions[0];
            if (suggestion.StartsWith(rawTarget, StringComparison.OrdinalIgnoreCase))
            {
                return new OpenResolution(_appShortcuts[suggestion], suggestion, suggestions);
            }
        }

        return suggestions.Count > 0
            ? new OpenResolution(null, null, suggestions)
            : new OpenResolution(rawTarget, null, []);
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
        HintText.Text = $"panic: Ctrl+Alt+Backspace | top: {(Topmost ? "on" : "off")} | stealth: {(_settings.StealthMode ? "on" : "off")}";
    }

    private void FocusCommandInput()
    {
        CommandInput.Focus();
        CommandInput.CaretIndex = CommandInput.Text.Length;
    }

    private sealed record OpenResolution(string? LaunchTarget, string? MatchedShortcut, IReadOnlyList<string> Suggestions);
}
