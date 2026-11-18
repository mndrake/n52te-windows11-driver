using System.Diagnostics;
using static N52te.Companion.NativeMethods;

namespace N52te.Companion;

/// <summary>Performs Companion actions when the keypad's hotkey arrives.</summary>
internal static class ActionRunner
{
    /// <summary>Runs <paramref name="action"/>; <paramref name="target"/> is the window that was in front when the hotkey fired.</summary>
    public static async Task RunAsync(CompanionAction action, IntPtr target)
    {
        // The keypad is still holding Ctrl+Alt+Shift when the hotkey fires. Wait until it lets go,
        // so they don't combine with what we send.
        await WaitForModifiersReleasedAsync();
        switch (action.Kind)
        {
            case CompanionKind.LaunchProgram:
                Launch(action.Argument);
                break;
            case CompanionKind.OpenUrl:
                OpenUrl(action.Argument);
                break;
            case CompanionKind.MinimizeWindow:
                PostMessage(target, WM_SYSCOMMAND, SC_MINIMIZE, IntPtr.Zero);
                break;
            case CompanionKind.MaximizeWindow:
                PostMessage(target, WM_SYSCOMMAND, SC_MAXIMIZE, IntPtr.Zero);
                break;
            case CompanionKind.CloseWindow:
                PostMessage(target, WM_SYSCOMMAND, SC_CLOSE, IntPtr.Zero);
                break;
            case CompanionKind.ShowDesktop:
                Send(Key(VK_LWIN, false), Key((ushort)'D', false), Key((ushort)'D', true), Key(VK_LWIN, true));
                break;
            case CompanionKind.ZoomIn:
                Send(Key(VK_CONTROL, false), Wheel(120), Key(VK_CONTROL, true));
                break;
            case CompanionKind.ZoomOut:
                Send(Key(VK_CONTROL, false), Wheel(-120), Key(VK_CONTROL, true));
                break;
            case CompanionKind.TypeText:
                TypeText(action.Argument);
                break;
        }
    }

    private static async Task WaitForModifiersReleasedAsync()
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 1500 && (Down(VK_CONTROL) || Down(VK_MENU) || Down(VK_SHIFT)))
            await Task.Delay(10);
        static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    /// <summary>Accepts a plain path, or "path with spaces" followed by arguments.</summary>
    internal static void Launch(string command)
    {
        command = command.Trim();
        string file = command, args = "";
        if (!File.Exists(command))
        {
            if (command.StartsWith('"'))
            {
                int end = command.IndexOf('"', 1);
                if (end > 0) { file = command[1..end]; args = command[(end + 1)..].Trim(); }
            }
            else
            {
                int space = command.IndexOf(' ');
                if (space > 0) { file = command[..space]; args = command[(space + 1)..].Trim(); }
            }
        }
        Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true });
    }

    private static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "mailto"))
            throw new InvalidOperationException($"Not a web address: {url}");
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private static void TypeText(string text)
    {
        foreach (var c in text.Replace("\r\n", "\n"))
        {
            if (c == '\n') Send(Key(VK_RETURN, false), Key(VK_RETURN, true));
            else Send(Unicode(c, false), Unicode(c, true));
        }
    }
}
