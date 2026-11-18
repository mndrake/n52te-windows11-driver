namespace N52te.Companion;

/// <summary>A small, click-through banner that shows the active keymap for a moment.</summary>
internal sealed class KeymapOsd : Form
{
    private static readonly Color[] Colors = { Color.FromArgb(40, 110, 230), Color.FromArgb(30, 160, 70), Color.FromArgb(215, 50, 50) };
    private readonly System.Windows.Forms.Timer hide = new() { Interval = 1400 };
    private readonly Label label = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, Font = new Font("Segoe UI", 16f, FontStyle.Bold) };

    public KeymapOsd()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(260, 64);
        Opacity = 0.92;
        Controls.Add(label);
        hide.Tick += (_, _) => { hide.Stop(); Hide(); };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOPMOST = 0x8;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOPMOST;
            return cp;
        }
    }

    public void ShowKeymap(Keymap keymap, string? detail = null)
    {
        BackColor = Colors[(int)keymap];
        label.Text = detail is null ? $"{keymap} keymap" : $"{keymap} keymap\n{detail}";
        label.Font = new Font("Segoe UI", detail is null ? 16f : 12f, FontStyle.Bold);
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(screen.Left + (screen.Width - Width) / 2, screen.Bottom - Height - 80);
        Show();
        hide.Stop();
        hide.Start();
    }
}
