using System.Diagnostics;
using Microsoft.Win32;
using static N52te.Companion.NativeMethods;

namespace N52te.Companion;

/// <summary>
/// The tray app: registers the active profile's hotkeys, runs their actions, and follows the
/// foreground app for keymap rules. It never hooks or filters the keyboard.
/// </summary>
internal sealed class CompanionContext : ApplicationContext
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "n52te Companion";

    private readonly NotifyIcon tray = new() { Icon = SystemIcons.Application, Text = "n52te Companion", Visible = true };
    private readonly ToolStripMenuItem statusItem = new() { Enabled = false };
    private readonly ToolStripMenuItem startWithWindows = new("Start with Windows") { CheckOnClick = true };
    private readonly HotkeyWindow hotkeys = new();
    private readonly KeymapOsd osd = new();
    private readonly FileSystemWatcher? watcher;
    private readonly System.Windows.Forms.Timer reloadDelay = new() { Interval = 400 };
    private readonly WinEventProc foregroundChanged;
    private readonly IntPtr foregroundHook;

    private Profile? profile;
    private Dictionary<int, CompanionAction> actionsById = new();
    private Keymap? lastRuleKeymap;

    public CompanionContext()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Show hotkeys", null, (_, _) => ShowHotkeys());
        menu.Items.Add("Reload profile", null, (_, _) => LoadProfile(showResult: true));
        menu.Items.Add("Open editor", null, (_, _) => OpenEditor());
        menu.Items.Add("Test keymap display", null, (_, _) => osd.ShowKeymap(Keymap.Green, "test"));
        menu.Items.Add(startWithWindows);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => ShowHotkeys();

        startWithWindows.Checked = IsStartWithWindows();
        startWithWindows.CheckedChanged += (_, _) => SetStartWithWindows(startWithWindows.Checked);

        hotkeys.HotkeyPressed += OnHotkey;

        Directory.CreateDirectory(AppPaths.DataFolder);
        watcher = new FileSystemWatcher(AppPaths.DataFolder, Path.GetFileName(AppPaths.ActiveProfile))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        // Events arrive on a worker thread and in bursts; reload once, on the UI thread.
        void Changed(object? s, FileSystemEventArgs e) => osd.BeginInvoke(() => { reloadDelay.Stop(); reloadDelay.Start(); });
        watcher.Changed += Changed;
        watcher.Created += Changed;
        watcher.Renamed += (s, e) => Changed(s, e);
        reloadDelay.Tick += (_, _) => { reloadDelay.Stop(); LoadProfile(showResult: true); };

        foregroundChanged = OnForegroundChanged;
        foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, foregroundChanged, 0, 0, WINEVENT_OUTOFCONTEXT);

        _ = osd.Handle;   // create the handle so BeginInvoke works before the banner is first shown
        LoadProfile(showResult: false);
    }

    private void LoadProfile(bool showResult)
    {
        hotkeys.UnregisterAll();
        actionsById.Clear();
        profile = null;
        if (!File.Exists(AppPaths.ActiveProfile))
        {
            SetStatus("No profile yet: in the editor use Companion → Use this profile in the Companion.");
            return;
        }
        try
        {
            profile = ProfileJson.Deserialize(File.ReadAllText(AppPaths.ActiveProfile));
            var bindings = ProfileCompiler.Compile(profile).Companion;
            var failed = new List<string>();
            int id = 1;
            foreach (var group in bindings.GroupBy(b => b.Chord))
            {
                if (hotkeys.Register(id, group.Key))
                    actionsById[id] = group.First().Action;
                else
                    failed.Add(group.Key.Display);
                id++;
            }
            var text = $"\"{profile.Name}\": {actionsById.Count} hotkey(s), {profile.AppRules.Count} app rule(s)";
            SetStatus(text);
            if (failed.Count > 0)
                tray.ShowBalloonTip(5000, "n52te Companion", $"Another program already uses: {string.Join(", ", failed)}. Those actions won't work.", ToolTipIcon.Warning);
            else if (showResult)
                tray.ShowBalloonTip(2500, "n52te Companion", $"Loaded {text}.", ToolTipIcon.Info);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ProfileException or System.Text.Json.JsonException)
        {
            SetStatus("Couldn't read the profile: " + e.Message);
            if (showResult) tray.ShowBalloonTip(5000, "n52te Companion", "Couldn't read the profile: " + e.Message, ToolTipIcon.Error);
        }
    }

    private void SetStatus(string text)
    {
        statusItem.Text = text;
        tray.Text = text.Length > 60 ? text[..60] : text;   // tray tooltips are limited to 63 characters
    }

    private async void OnHotkey(object? sender, int id)
    {
        if (!actionsById.TryGetValue(id, out var action)) return;
        var target = GetForegroundWindow();
        try
        {
            await ActionRunner.RunAsync(action, target);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            tray.ShowBalloonTip(4000, "n52te Companion", $"{action.Describe()}: {e.Message}", ToolTipIcon.Warning);
        }
    }

    private void OnForegroundChanged(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (profile is not { AppRules.Count: > 0 } || hwnd == IntPtr.Zero) return;
        string exe;
        try
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            using var process = Process.GetProcessById((int)pid);
            exe = process.ProcessName + ".exe";
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return; }

        var rule = profile.AppRules.FirstOrDefault(r => string.Equals(r.ExecutableName, exe, StringComparison.OrdinalIgnoreCase));
        var wanted = rule?.Keymap ?? profile.StartKeymap;
        if (wanted == lastRuleKeymap) return;
        lastRuleKeymap = wanted;
        SelectKeymap(wanted, rule is null ? null : exe);
    }

    /// <summary>
    /// Switches the keypad's keymap. Needs a direct keypad connection, which is chosen after the
    /// probe; until then this only reports what it would do.
    /// </summary>
    private void SelectKeymap(Keymap keymap, string? reason)
    {
        var transport = KeypadConnection.TryOpen();
        if (transport is null)
        {
            SetStatus($"Would switch to {keymap}{(reason is null ? "" : $" for {reason}")}, but there is no keypad connection yet.");
            return;
        }
        try
        {
            using (transport) new KeypadClient(transport).SelectKeymap(keymap);
            osd.ShowKeymap(keymap, reason);
        }
        catch (Exception e) when (e is IOException or KeypadException or InvalidOperationException)
        {
            SetStatus("Keymap switch failed: " + e.Message);
        }
    }

    private void ShowHotkeys()
    {
        if (profile is null) { MessageBox.Show(statusItem.Text, "n52te Companion"); return; }
        var lines = ProfileCompiler.Compile(profile).Companion
            .GroupBy(b => b.Chord)
            .Select(g => $"{g.Key.Display}  →  {g.First().Action.Describe()}  [{string.Join(", ", g.Select(b => $"{b.Keymap} {KeypadLayout.Input(b.Input).Name}"))}]");
        var rules = profile.AppRules.Select(r => $"{r.ExecutableName}  →  {r.Keymap} keymap");
        MessageBox.Show(
            $"Profile \"{profile.Name}\"\n\nHotkeys:\n{string.Join("\n", lines.DefaultIfEmpty("(none)"))}\n\nApp rules:\n{string.Join("\n", rules.DefaultIfEmpty("(none)"))}",
            "n52te Companion", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void OpenEditor()
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "n52te-editor.exe");
        if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        else MessageBox.Show($"n52te-editor.exe was not found next to the Companion ({AppContext.BaseDirectory}).", "n52te Companion");
    }

    private static bool IsStartWithWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunValue) is string;
    }

    private static void SetStartWithWindows(bool on)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue(RunValue, throwOnMissingValue: false);
    }

    protected override void ExitThreadCore()
    {
        if (foregroundHook != IntPtr.Zero) UnhookWinEvent(foregroundHook);
        hotkeys.UnregisterAll();
        hotkeys.DestroyHandle();
        watcher?.Dispose();
        tray.Visible = false;
        tray.Dispose();
        osd.Dispose();
        base.ExitThreadCore();
    }
}

/// <summary>A hidden window that receives WM_HOTKEY.</summary>
internal sealed class HotkeyWindow : NativeWindow
{
    private readonly HashSet<int> registered = new();

    public event EventHandler<int>? HotkeyPressed;

    public HotkeyWindow() => CreateHandle(new CreateParams());

    public bool Register(int id, HotkeyChord chord)
    {
        if (!RegisterHotKey(Handle, id, HotkeyChord.Modifiers, chord.VirtualKey)) return false;
        registered.Add(id);
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in registered) UnregisterHotKey(Handle, id);
        registered.Clear();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY) HotkeyPressed?.Invoke(this, (int)m.WParam);
        base.WndProc(ref m);
    }
}

/// <summary>
/// Opens a connection to the keypad for keymap switching. Returns null until a transport is
/// implemented (standard HID or temporary WinUSB, decided by the probe; see docs/onboard-editor-plan.md).
/// </summary>
internal static class KeypadConnection
{
    public static IKeypadTransport? TryOpen() => null;
}
