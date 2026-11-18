namespace N52te.Editor;

/// <summary>The 27 inputs drawn roughly where they sit on the keypad. Click one to select it.</summary>
internal sealed class KeypadView : UserControl
{
    private const int Cell = 92, CellH = 52, Gap = 6;
    private readonly Button[] buttons = new Button[KeypadLayout.InputCount];
    private int selected = 1;

    public event EventHandler? SelectedInputChanged;

    public KeypadView()
    {
        DoubleBuffered = true;
        // Keys: three rows on the left; wheel, thumb button, D-pad and thumb pad on the right.
        var positions = new Dictionary<int, (float Col, float Row)>
        {
            [1] = (0, 0), [2] = (1, 0), [3] = (2, 0), [4] = (3, 0), [5] = (4, 0),
            [6] = (0, 1), [7] = (1, 1), [8] = (2, 1), [9] = (3, 1), [10] = (4, 1),
            [11] = (0.5f, 2), [12] = (1.5f, 2), [13] = (2.5f, 2), [14] = (3.5f, 2),
            [16] = (5.4f, 0), [17] = (5.4f, 1), [18] = (5.4f, 2),
            [15] = (6.6f, 0),
            [27] = (6.6f, 1), [20] = (7.6f, 1), [24] = (8.6f, 1),
            [23] = (6.6f, 2), [21] = (8.6f, 2),
            [26] = (6.6f, 3), [22] = (7.6f, 3), [25] = (8.6f, 3),
            [19] = (7.1f, 4.1f),
        };
        foreach (var (input, pos) in positions)
        {
            var b = new Button
            {
                Left = (int)(pos.Col * (Cell + Gap)),
                Top = (int)(pos.Row * (CellH + Gap)),
                Width = input == 19 ? Cell * 2 : Cell,
                Height = CellH,
                FlatStyle = FlatStyle.Flat,
                Tag = input,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 7.5f),
                UseMnemonic = false,
            };
            b.FlatAppearance.BorderSize = 1;
            b.Click += (_, _) => SelectedInput = (int)b.Tag!;
            buttons[input - 1] = b;
            Controls.Add(b);
        }
        // Center cell of the D-pad, just a label.
        Controls.Add(new Label { Text = "D-pad", Left = (int)(7.6f * (Cell + Gap)), Top = 2 * (CellH + Gap), Width = Cell, Height = CellH, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Gray });
        Width = (int)(9.6f * (Cell + Gap));
        Height = (int)(5.1f * (CellH + Gap));
    }

    public int SelectedInput
    {
        get => selected;
        set
        {
            if (value == selected) return;
            selected = value;
            Highlight();
            SelectedInputChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Show(Profile profile, Keymap keymap, Color keymapColor)
    {
        for (int n = 1; n <= KeypadLayout.InputCount; n++)
        {
            var action = profile.Get(keymap, n).Action;
            var b = buttons[n - 1];
            b.Text = $"{KeypadLayout.Input(n).Name}\n{action.Describe()}";
            b.BackColor = action switch
            {
                DisabledAction => Color.FromArgb(225, 225, 225),
                LegacyPcAction or RawAction => Color.FromArgb(255, 225, 200),
                CompanionAction => Color.FromArgb(230, 220, 255),
                _ when IsFactory(n, action) => SystemColors.Window,
                _ => Blend(keymapColor, 0.25),
            };
        }
        Highlight();
    }

    private static bool IsFactory(int input, InputAction action) =>
        action == KeypadLayout.FactoryAction(input)
        || (action is MacroAction a && KeypadLayout.FactoryAction(input) is MacroAction f && a.Steps.SequenceEqual(f.Steps));

    private void Highlight()
    {
        foreach (var b in buttons)
        {
            bool isSelected = (int)b.Tag! == selected;
            b.FlatAppearance.BorderSize = isSelected ? 3 : 1;
            b.FlatAppearance.BorderColor = isSelected ? Color.Black : Color.Silver;
        }
    }

    private static Color Blend(Color c, double amount) => Color.FromArgb(
        (int)(255 - (255 - c.R) * amount), (int)(255 - (255 - c.G) * amount), (int)(255 - (255 - c.B) * amount));
}
