using System.Runtime.InteropServices;

namespace TermalogySkin.Services;

public static class TaskbarManager
{
    private const int SwHide = 0;
    private const int SwShow = 5;

    public static bool SetVisible(bool visible)
    {
        var command = visible ? SwShow : SwHide;
        var changed = false;

        foreach (var handle in GetTaskbarHandles())
        {
            if (handle == IntPtr.Zero)
            {
                continue;
            }

            _ = ShowWindow(handle, command);
            changed = true;
        }

        return changed;
    }

    public static bool IsVisible()
    {
        var primary = FindWindow("Shell_TrayWnd", null);
        return primary != IntPtr.Zero && IsWindowVisible(primary);
    }

    private static IEnumerable<IntPtr> GetTaskbarHandles()
    {
        var primary = FindWindow("Shell_TrayWnd", null);
        if (primary != IntPtr.Zero)
        {
            yield return primary;
        }

        var current = IntPtr.Zero;
        while (true)
        {
            current = FindWindowEx(IntPtr.Zero, current, "Shell_SecondaryTrayWnd", null);
            if (current == IntPtr.Zero)
            {
                break;
            }

            yield return current;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowTitle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
}
