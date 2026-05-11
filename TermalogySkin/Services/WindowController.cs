using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TermalogySkin.Services;

public static class WindowController
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const uint WmClose = 0x0010;

    public static IReadOnlyList<WindowSummary> ListVisibleWindows(string currentProcessName)
    {
        var windows = new List<WindowSummary>();

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
            {
                return true;
            }

            var titleLength = GetWindowTextLength(hWnd);
            if (titleLength <= 0)
            {
                return true;
            }

            var titleBuilder = new StringBuilder(titleLength + 1);
            _ = GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
            var title = titleBuilder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            var threadId = GetWindowThreadProcessId(hWnd, out var processId);
            if (threadId == 0 || processId == 0)
            {
                return true;
            }

            try
            {
                var process = Process.GetProcessById((int)processId);
                if (process.ProcessName.Equals(currentProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                windows.Add(new WindowSummary(hWnd, process.ProcessName, title));
            }
            catch
            {
                // Process can disappear while enumerating; skip it.
            }

            return true;
        }, IntPtr.Zero);

        return windows
            .OrderBy(window => window.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(window => window.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool CloseWindow(IntPtr handle)
    {
        return PostMessage(handle, WmClose, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}

public readonly record struct WindowSummary(IntPtr Handle, string ProcessName, string Title);
