namespace N52te.Editor;

/// <summary>
/// "Read mapping by pressing buttons": listens to the keypad only (Raw Input) while the user presses
/// each of the 27 inputs, and works out what each one does.
/// </summary>
internal sealed class DetectDialog : Form
{
    private const string DefaultDeviceFilter = "VID_050D";
    private static readonly string[] Hints =
    {
        "top row, 1st key", "top row, 2nd key", "top row, 3rd key", "top row, 4th key", "top row, 5th key",
        "middle row, 1st key", "middle row, 2nd key", "middle row, 3rd key", "middle row, 4th key", "middle row, 5th key",
        "bottom row, 1st key", "bottom row, 2nd key", "bottom row, 3rd key", "bottom row, 4th key",
        "round orange button under your thumb", "roll the wheel up one notch", "click the wheel", "roll the wheel down one notch",
        "large thumb pad", "D-pad up", "D-pad right", "D-pad down", "D-pad left",
        "D-pad diagonally up and right", "D-pad diagonally down and right", "D-pad diagonally down and left", "D-pad diagonally up and left",
    };

    private readonly Keymap keymap;
    private readonly InputAction?[] results = new InputAction?[KeypadLayout.InputCount];
    private readonly List<DetectedEvent> heard = new();
    private readonly HashSet<byte> keysDown = new();
    private readonly System.Windows.Forms.Timer quiet = new() { Interval = 450 };
    private readonly HashSet<string> otherDevices = new();
    private string deviceFilter = DefaultDeviceFilter;
    private int current = 1;

    private readonly Label prompt = new() { Dock = DockStyle.Top, Height = 64, Font = new Font("Segoe UI", 15f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
    private readonly WrapLabel hint = new() { Dock = DockStyle.Top, TextAlign = ContentAlignment.TopCenter };
    private readonly WrapLabel live = new() { Dock = DockStyle.Top, ForeColor = Color.DimGray, TextAlign = ContentAlignment.TopCenter };
    private readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly ComboBox devices = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 520 };

    /// <summary>What was found, by input number (only inputs that were answered).</summary>
    public Dictionary<int, InputAction> Results { get; } = new();

    public DetectDialog(Keymap keymap, Color keymapColor)
    {
        Dpi.Scale(this);
        this.keymap = keymap;
        Text = $"Read keypad mapping: {keymap} keymap";
        Width = 760;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        KeyPreview = true;

        var intro = new WrapLabel
        {
            Dock = DockStyle.Top,
            Text = $"1. Put the keypad on the {keymap} keymap (LED colour). 2. Press each input when asked; this window only listens to the keypad, " +
                   "so your normal keyboard and mouse are ignored. 3. Inputs that send nothing to the PC (keymap buttons, disabled buttons, old Belkin " +
                   "PC actions) need one of the buttons below. If a button sends the Windows key, the Start menu may open: close it and continue.",
        };
        var banner = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = keymapColor };

        var back = new Button { Text = "Back", AutoSize = true };
        var nothing = new Button { Text = "It sends nothing", AutoSize = true };
        var led = new Button { Text = "The LED changed…", AutoSize = true };
        var skip = new Button { Text = "Skip (keep current)", AutoSize = true };
        var apply = new Button { Text = "Apply to keymap", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        back.Click += (_, _) => Go(Math.Max(1, current - 1));
        nothing.Click += (_, _) => Answer(new DisabledAction());
        skip.Click += (_, _) => Answer(null);
        led.Click += (_, _) => LedMenu().Show(led, new Point(0, led.Height));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        buttons.Controls.AddRange(new Control[] { back, nothing, led, skip, apply, cancel });

        var deviceRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8, 0, 8, 0) };
        deviceRow.Controls.Add(new Label { Text = "Listening to:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        deviceRow.Controls.Add(devices);
        devices.Items.Add($"n52te ({DefaultDeviceFilter})");
        devices.SelectedIndex = 0;
        devices.SelectedIndexChanged += (_, _) => deviceFilter = devices.SelectedIndex == 0 ? DefaultDeviceFilter : (string)devices.SelectedItem!;

        Controls.Add(list);
        Controls.Add(live);
        Controls.Add(hint);
        Controls.Add(prompt);
        Controls.Add(banner);
        Controls.Add(intro);
        Controls.Add(deviceRow);
        Controls.Add(buttons);
        AcceptButton = null;
        CancelButton = null;

        quiet.Tick += (_, _) => Finish();
        HandleCreated += (_, _) => RawInput.Register(Handle);
        FormClosed += (_, _) => { RawInput.Unregister(); quiet.Dispose(); };
        FormClosing += (_, _) =>
        {
            if (DialogResult != DialogResult.OK) return;
            for (int n = 1; n <= KeypadLayout.InputCount; n++)
                if (results[n - 1] is { } a) Results[n] = a;
        };
        Load += (_, _) => Dpi.FitToScreen(this);
        Go(1);
    }

    // Keys from the keypad must not press this window's buttons or open menus.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) => true;

    protected override bool ProcessDialogKey(Keys keyData) => true;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == RawInput.WM_INPUT && RawInput.Read(m.LParam) is { } input && input.Events.Count > 0)
        {
            if (input.Device.Contains(deviceFilter, StringComparison.OrdinalIgnoreCase)) Heard(input.Events);
            else NoteOtherDevice(input.Device);
        }
        base.WndProc(ref m);
    }

    private void NoteOtherDevice(string name)
    {
        // Only offer devices that look like a USB keyboard or mouse with an ID, so the user can pick a
        // keypad that reports a different vendor ID.
        if (name.Length == 0 || !name.Contains("VID_", StringComparison.OrdinalIgnoreCase) || !otherDevices.Add(name)) return;
        devices.Items.Add(name);
    }

    private void Heard(List<DetectedEvent> events)
    {
        if (current > KeypadLayout.InputCount) return;
        foreach (var e in events)
        {
            heard.Add(e);
            if (e is DetectedKey k) { if (k.Press) keysDown.Add(k.Code); else keysDown.Remove(k.Code); }
        }
        live.Text = "Heard: " + string.Join("  ", heard.Select(Describe).Distinct().Take(12));
        quiet.Stop();
        quiet.Start();
    }

    private void Finish()
    {
        if (keysDown.Count > 0) return;   // still held: keep waiting
        quiet.Stop();
        var action = MappingDetector.Interpret(heard);
        if (action is not null) Answer(action);
    }

    private void Answer(InputAction? action)
    {
        if (current > KeypadLayout.InputCount) return;
        results[current - 1] = action;
        Go(current + 1);
    }

    private void Go(int input)
    {
        quiet.Stop();
        heard.Clear();
        keysDown.Clear();
        current = input;
        if (current > KeypadLayout.InputCount)
        {
            prompt.Text = "Done";
            hint.Text = $"Check the list, then click \"Apply to keymap\" to load it into the {keymap} keymap. Use Back to redo an input.";
            live.Text = "";
        }
        else
        {
            prompt.Text = $"Press: {KeypadLayout.Input(current).Name}";
            hint.Text = $"({Hints[current - 1]})";
            live.Text = "Waiting…";
        }
        list.BeginUpdate();
        list.Items.Clear();
        for (int n = 1; n <= KeypadLayout.InputCount; n++)
        {
            var r = results[n - 1];
            var what = r is null ? "—" : r is DisabledAction ? "sends nothing (disabled, or an old Belkin PC action)" : r.Describe();
            list.Items.Add($"{(n == current ? "▶ " : "   ")}{KeypadLayout.Input(n).Name}:  {what}");
        }
        list.EndUpdate();
        if (current <= KeypadLayout.InputCount) list.TopIndex = Math.Max(0, current - 4);
    }

    private ContextMenuStrip LedMenu()
    {
        var menu = new ContextMenuStrip();
        foreach (KeymapSwitch s in Enum.GetValues<KeymapSwitch>())
        {
            var action = new KeymapSwitchAction(s);
            menu.Items.Add(action.Describe(), null, (_, _) => Answer(action));
        }
        return menu;
    }

    private static string Describe(DetectedEvent e) => e switch
    {
        DetectedKey k => (k.Press ? "+" : "-") + FirmwareCodes.Name(k.Code),
        DetectedMouse m => m.Kind.ToString(),
        _ => "?",
    };
}
