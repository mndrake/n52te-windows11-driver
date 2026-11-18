// Small dialogs, in the dark style of Belkin's editor: Single Key, Macro, and a name prompt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace N52te
{
    internal static class DarkStyle
    {
        public static readonly Color Back = Color.FromArgb(16, 17, 20);
        public static readonly Color Field = Color.FromArgb(28, 30, 35);
        public static readonly Color Text = Color.FromArgb(225, 228, 232);
        public static readonly Color Accent = Color.FromArgb(80, 160, 245);

        public static void Apply(Form f)
        {
            f.BackColor = Back;
            f.ForeColor = Text;
            f.Font = new Font("Arial", 9f);
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent;
            f.AutoScaleMode = AutoScaleMode.Dpi;
        }

        public static Button Button(string text, DialogResult result)
        {
            return new Button { Text = text, DialogResult = result, FlatStyle = FlatStyle.Flat, BackColor = Field, ForeColor = Text, AutoSize = true, Padding = new Padding(8, 2, 8, 2), Margin = new Padding(6) };
        }
    }

    // "Please Enter a Single Key": captures one key press, like Belkin's Single Key dialog,
    // telling left and right Shift/Ctrl/Alt apart. A list offers every key as well.
    internal sealed class SingleKeyDialog : Form
    {
        private readonly Label shown;
        private readonly ComboBox list;
        private readonly Button ok;
        public byte Code { get; private set; }

        public SingleKeyDialog(string inputName)
        {
            DarkStyle.Apply(this);
            Text = "Single Key - " + inputName;
            KeyPreview = true;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(14), AutoSize = true };
            layout.Controls.Add(new Label { Text = "Please Enter a Single Key", AutoSize = true, Font = new Font("Arial", 11f, FontStyle.Bold) });
            shown = new Label { Text = "(press a key)", AutoSize = true, ForeColor = DarkStyle.Accent, Font = new Font("Arial", 16f, FontStyle.Bold), Margin = new Padding(3, 10, 3, 10) };
            layout.Controls.Add(shown);
            layout.Controls.Add(new Label { Text = "or choose one:", AutoSize = true });
            list = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, BackColor = DarkStyle.Field, ForeColor = DarkStyle.Text, FlatStyle = FlatStyle.Flat, TabStop = false };
            var keys = new List<KeyValuePair<byte, string>>();
            for (int c = 0x10; c <= 0x9F; c++)
            {
                string n = FirmwareCodes.Name(c);
                if (!n.StartsWith("0x") && !n.StartsWith("DPad") && c != 0x87) keys.Add(new KeyValuePair<byte, string>((byte)c, n));
            }
            keys.Sort((a, b) => string.Compare(a.Value, b.Value, StringComparison.OrdinalIgnoreCase));
            foreach (var k in keys) list.Items.Add(new KeyItem(k.Key, k.Value));
            list.SelectedIndexChanged += (s, e) => { var k = list.SelectedItem as KeyItem; if (k != null) Set(k.Code); };
            layout.Controls.Add(list);
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            ok = DarkStyle.Button("OK", DialogResult.OK);
            ok.Enabled = false;
            ok.TabStop = false;
            var cancel = DarkStyle.Button("Cancel", DialogResult.Cancel);
            cancel.TabStop = false;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            layout.Controls.Add(buttons);
            Controls.Add(layout);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        private sealed class KeyItem
        {
            public readonly byte Code;
            private readonly string name;
            public KeyItem(byte code, string name) { Code = code; this.name = name; }
            public override string ToString() { return name; }
        }

        private void Set(byte code)
        {
            Code = code;
            shown.Text = FirmwareCodes.Name(code);
            ok.Enabled = true;
        }

        // Every key, including Tab, Enter, Esc, arrows and Alt, arrives here before the controls see it.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            const int WM_KEYDOWN = 0x100, WM_SYSKEYDOWN = 0x104;
            if (msg.Msg == WM_KEYDOWN || msg.Msg == WM_SYSKEYDOWN)
            {
                int vk = (int)(keyData & Keys.KeyCode);
                long lp = msg.LParam.ToInt64();
                int scan = (int)((lp >> 16) & 0xFF);
                bool extended = ((lp >> 24) & 1) != 0;
                if (vk == 0x10) vk = scan == 0x36 ? 0xA1 : 0xA0;           // Shift: left/right by scan code
                else if (vk == 0x11) vk = extended ? 0xA3 : 0xA2;          // Ctrl
                else if (vk == 0x12) vk = extended ? 0xA5 : 0xA4;          // Alt
                byte code = vk == 0x0D && extended ? (byte)0x74 : FirmwareCodes.FromVirtualKey(vk); // numpad Enter
                if (code != 0) Set(code);
                else shown.Text = "(the keypad has no such key)";
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Print Screen only arrives as a key release.
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.PrintScreen) Set(0x7A);
            base.OnKeyUp(e);
        }
    }

    // Macro: steps as text, plus Belkin's Macro Behavior options. (The full macro editor with
    // recording comes with phase 3 of docs/onboard-editor-plan.md.)
    internal sealed class MacroDialog : Form
    {
        private readonly TextBox steps;
        private readonly ComboBox mode, interval;
        private readonly NumericUpDown times;
        private readonly Label preview;
        private readonly Button ok;
        public byte[] Sequence { get; private set; }
        public byte[] Options { get; private set; }

        public MacroDialog(string inputName, InputRecord current, bool isMacro)
        {
            DarkStyle.Apply(this);
            Text = "Macro - " + inputName;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(14), AutoSize = true };
            layout.Controls.Add(new Label { Text = "Steps", AutoSize = true, Font = new Font("Arial", 10f, FontStyle.Bold) }, 0, 0);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, 0), 2);
            steps = new TextBox { Width = 420, BackColor = DarkStyle.Field, ForeColor = DarkStyle.Text, BorderStyle = BorderStyle.FixedSingle };
            if (isMacro && current.FirmwareSequence == null) steps.Text = Macro.ToText(current.Sequence);
            layout.Controls.Add(steps, 0, 1);
            layout.SetColumnSpan(steps, 2);
            var help = new Label
            {
                AutoSize = true, MaximumSize = new Size(420, 0), ForeColor = Color.FromArgb(150, 154, 160),
                Text = "Separate steps with spaces. H = tap H.  +LShift / -LShift = press / release.  500ms = wait after the previous step (50 ms steps, up to 6350 ms).  Examples: \"H I\"   \"+LShift A -LShift\"   \"A 1000ms B\"   \"Mouse_Left\". Up to 128 steps; each delay counts as one.",
            };
            layout.Controls.Add(help, 0, 2);
            layout.SetColumnSpan(help, 2);
            preview = new Label { AutoSize = true, MaximumSize = new Size(420, 0), ForeColor = DarkStyle.Accent, Margin = new Padding(3, 8, 3, 8) };
            layout.Controls.Add(preview, 0, 3);
            layout.SetColumnSpan(preview, 2);

            layout.Controls.Add(new Label { Text = "Macro Behavior", AutoSize = true, Font = new Font("Arial", 10f, FontStyle.Bold) }, 0, 4);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, 4), 2);
            mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240, BackColor = DarkStyle.Field, ForeColor = DarkStyle.Text, FlatStyle = FlatStyle.Flat };
            mode.Items.AddRange(new object[] { "Run Times", "Repeat while pressed", "Repeat until next key is pressed" });
            times = new NumericUpDown { Minimum = 1, Maximum = 10, Width = 60, BackColor = DarkStyle.Field, ForeColor = DarkStyle.Text };
            interval = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100, BackColor = DarkStyle.Field, ForeColor = DarkStyle.Text, FlatStyle = FlatStyle.Flat };
            interval.Items.AddRange(new object[] { "50 ms", "100 ms", "150 ms", "200 ms", "250 ms" });
            byte[] o = isMacro ? current.Options : new byte[] { 0, 0, 0 };
            mode.SelectedIndex = o[0] <= 2 ? o[0] : 0;
            times.Value = o[1] <= 9 ? o[1] + 1 : 1;
            interval.SelectedIndex = o[2] <= 4 ? o[2] : 0;
            layout.Controls.Add(new Label { Text = "Mode", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
            layout.Controls.Add(mode, 1, 5);
            layout.Controls.Add(new Label { Text = "Run times", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
            layout.Controls.Add(times, 1, 6);
            layout.Controls.Add(new Label { Text = "Repetition interval", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
            layout.Controls.Add(interval, 1, 7);

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            ok = DarkStyle.Button("OK", DialogResult.OK);
            buttons.Controls.Add(DarkStyle.Button("Cancel", DialogResult.Cancel));
            buttons.Controls.Add(ok);
            layout.Controls.Add(buttons, 0, 8);
            layout.SetColumnSpan(buttons, 2);
            Controls.Add(layout);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            AcceptButton = ok;

            steps.TextChanged += (s, e) => UpdatePreview();
            mode.SelectedIndexChanged += (s, e) => { times.Enabled = mode.SelectedIndex == 0; };
            times.Enabled = mode.SelectedIndex == 0;
            UpdatePreview();
            ok.Click += (s, e) => { Options = new[] { (byte)mode.SelectedIndex, (byte)(times.Value - 1), (byte)interval.SelectedIndex }; };
        }

        private void UpdatePreview()
        {
            try
            {
                Sequence = Macro.Parse(steps.Text);
                preview.Text = Macro.Describe(Sequence);
                preview.ForeColor = DarkStyle.Accent;
                ok.Enabled = true;
            }
            catch (ArgumentException e)
            {
                Sequence = null;
                preview.Text = steps.Text.Trim().Length == 0 ? "Type the macro's steps." : e.Message;
                preview.ForeColor = Color.FromArgb(240, 120, 110);
                ok.Enabled = false;
            }
        }
    }

    internal sealed class NameDialog : Form
    {
        private readonly TextBox box;
        public string Value { get { return box.Text.Trim(); } }

        public NameDialog(string title, string prompt, string initial)
        {
            DarkStyle.Apply(this);
            Text = title;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(14), AutoSize = true };
            layout.Controls.Add(new Label { Text = prompt, AutoSize = true });
            box = new TextBox { Width = 300, Text = initial, BackColor = DarkStyle.Field, ForeColor = DarkStyle.Text, BorderStyle = BorderStyle.FixedSingle };
            layout.Controls.Add(box);
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            var ok = DarkStyle.Button("OK", DialogResult.OK);
            buttons.Controls.Add(DarkStyle.Button("Cancel", DialogResult.Cancel));
            buttons.Controls.Add(ok);
            layout.Controls.Add(buttons);
            Controls.Add(layout);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            AcceptButton = ok;
            box.TextChanged += (s, e) => ok.Enabled = IsValidName(Value);
            ok.Enabled = IsValidName(Value);
        }

        public static bool IsValidName(string s)
        {
            return s.Length > 0 && s.Length <= 60 && s.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) < 0;
        }
    }
}
