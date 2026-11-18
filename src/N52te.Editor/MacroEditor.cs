using System.Diagnostics;

namespace N52te.Editor;

/// <summary>Edits a list of macro steps: add, remove, reorder, record from the keyboard.</summary>
internal sealed class MacroEditor : UserControl
{
    private readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly ComboBox key = ActionEditor.KeyCombo();
    private readonly RadioButton press = new() { Text = "Press", Checked = true, AutoSize = true };
    private readonly RadioButton release = new() { Text = "Release", AutoSize = true };
    private readonly NumericUpDown delay = new() { Minimum = 0, Maximum = Macro.MaxDelayUnits * Macro.DelayUnitMs, Increment = Macro.DelayUnitMs, Width = 70 };
    private readonly Label counter = new() { AutoSize = true };
    private readonly KeyRecorderBox recorder = new() { Width = 260 };
    private readonly CheckBox recordDelays = new() { Text = "Record delays", AutoSize = true };
    private List<MacroStep> steps = new();

    public event EventHandler? Changed;

    public MacroEditor()
    {
        var add = new Button { Text = "Add", AutoSize = true };
        var tap = new Button { Text = "Add tap", AutoSize = true };
        var remove = new Button { Text = "Remove", AutoSize = true };
        var up = new Button { Text = "Up", AutoSize = true };
        var down = new Button { Text = "Down", AutoSize = true };
        var clear = new Button { Text = "Clear", AutoSize = true };
        add.Click += (_, _) => Insert(new[] { new MacroStep(SelectedKey(), press.Checked, DelayUnits()) });
        tap.Click += (_, _) => Insert(new[] { new MacroStep(SelectedKey(), true), new MacroStep(SelectedKey(), false, DelayUnits()) });
        remove.Click += (_, _) => { if (list.SelectedIndex >= 0) { steps.RemoveAt(list.SelectedIndex); Redraw(list.SelectedIndex - 1); } };
        up.Click += (_, _) => MoveSelected(-1);
        down.Click += (_, _) => MoveSelected(+1);
        clear.Click += (_, _) => { steps.Clear(); Redraw(-1); };
        recorder.StepRecorded += (_, step) => Insert(new[] { step });
        recorder.DelayBeforeStep += (_, units) =>
        {
            // Insert() keeps the last added step selected, so the delay goes on that one.
            int previous = list.SelectedIndex >= 0 ? list.SelectedIndex : steps.Count - 1;
            if (previous >= 0) { steps[previous] = steps[previous] with { DelayUnits = units }; Redraw(previous); }
        };
        recordDelays.CheckedChanged += (_, _) => recorder.RecordDelays = recordDelays.Checked;

        var editRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        editRow.Controls.AddRange(new Control[] { key, press, release, new Label { Text = "then wait (ms)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, delay, add, tap });
        var buttonsRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        buttonsRow.Controls.AddRange(new Control[] { remove, up, down, clear, counter });
        var recordRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
        recordRow.Controls.AddRange(new Control[] { new Label { Text = "Record: click here and type", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, recorder, recordDelays });

        Controls.Add(list);
        Controls.Add(buttonsRow);
        Controls.Add(editRow);
        Controls.Add(recordRow);
        Redraw(-1);
    }

    public List<MacroStep> Steps
    {
        get => steps.ToList();
        set { steps = value.ToList(); Redraw(-1); }
    }

    private byte SelectedKey() => key.SelectedItem is KeyItem k ? k.Code : (byte)0x14;

    private int DelayUnits() => (int)Math.Round(delay.Value / Macro.DelayUnitMs);

    private void Insert(IEnumerable<MacroStep> newSteps)
    {
        int at = list.SelectedIndex >= 0 ? list.SelectedIndex + 1 : steps.Count;
        var items = newSteps.ToList();
        steps.InsertRange(at, items);
        Redraw(at + items.Count - 1);
    }

    private void MoveSelected(int by)
    {
        int i = list.SelectedIndex, j = i + by;
        if (i < 0 || j < 0 || j >= steps.Count) return;
        (steps[i], steps[j]) = (steps[j], steps[i]);
        Redraw(j);
    }

    private void Redraw(int select)
    {
        list.BeginUpdate();
        list.Items.Clear();
        foreach (var s in steps)
        {
            var text = $"{(s.Press ? "Press" : "Release")}  {FirmwareCodes.Name(s.Code)}";
            if (s.DelayUnits > 0) text += $"   then wait {s.DelayUnits * Macro.DelayUnitMs} ms";
            list.Items.Add(text);
        }
        list.EndUpdate();
        if (select >= 0 && select < list.Items.Count) list.SelectedIndex = select;
        int used = Macro.PairCount(steps);
        counter.Text = $"{used} / {Macro.MaxPairs} steps (delays count as steps)";
        counter.ForeColor = used > Macro.MaxPairs ? Color.Firebrick : SystemColors.ControlText;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>A box that turns key presses and releases into macro steps instead of typing.</summary>
internal sealed class KeyRecorderBox : TextBox
{
    private readonly HashSet<int> down = new();
    private readonly Stopwatch clock = new();

    public event EventHandler<MacroStep>? StepRecorded;

    public bool RecordDelays { get; set; }

    public KeyRecorderBox()
    {
        ReadOnly = true;
        BackColor = SystemColors.Info;
        Text = "(click, then press keys)";
        GotFocus += (_, _) => { Text = "Recording… click elsewhere to stop"; down.Clear(); clock.Reset(); };
        LostFocus += (_, _) => Text = "(click, then press keys)";
    }

    protected override bool IsInputKey(Keys keyData) => true;

    protected override bool ProcessDialogKey(Keys keyData) => false;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = e.SuppressKeyPress = true;
        int vk = (int)e.KeyCode;
        if (!down.Add(vk)) return;   // auto-repeat
        Record(vk, true);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        e.Handled = true;
        int vk = (int)e.KeyCode;
        down.Remove(vk);
        Record(vk, false);
    }

    private void Record(int vk, bool press)
    {
        if (WindowsKeys.ToFirmware(vk) is not { } code) return;
        int units = 0;
        if (RecordDelays && clock.IsRunning)
            units = (int)Math.Min(Macro.MaxDelayUnits, Math.Round(clock.ElapsedMilliseconds / (double)Macro.DelayUnitMs));
        clock.Restart();
        // The delay belongs to the previous step in the keypad's format, so it is reported on the step before.
        if (units > 0) DelayBeforeStep?.Invoke(this, units);
        StepRecorded?.Invoke(this, new MacroStep(code, press));
    }

    /// <summary>Raised with the delay (in units) that should follow the previously recorded step.</summary>
    public event EventHandler<int>? DelayBeforeStep;
}
