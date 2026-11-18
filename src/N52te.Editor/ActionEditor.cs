namespace N52te.Editor;

internal sealed record KeyItem(byte Code, string Name)
{
    public override string ToString() => Name;
}

internal sealed record Choice<T>(T Value, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Edits the action of the selected input.</summary>
internal sealed class ActionEditor : UserControl
{
    private enum Kind { Factory, Key, Mouse, Keymap, Macro, Text, Companion, Disabled, Imported }

    private readonly Label title = new() { Font = new Font("Segoe UI", 11f, FontStyle.Bold), AutoSize = true };
    private readonly ComboBox kind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly Panel body = new() { Dock = DockStyle.Fill };
    private readonly Label note = new() { Dock = DockStyle.Bottom, Height = 48, ForeColor = Color.DimGray };
    private readonly Button apply = new() { Text = "Apply to this input", AutoSize = true, Dock = DockStyle.Bottom };

    private readonly ComboBox keyCombo = KeyCombo();
    private readonly ComboBox mouseCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly ComboBox keymapCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly MacroEditor macro = new() { Dock = DockStyle.Fill };
    private readonly TextBox text = new() { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
    private readonly Label textBudget = new() { Dock = DockStyle.Bottom, AutoSize = false, Height = 22 };
    private readonly ComboBox companionKind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly TextBox companionArg = new() { Width = 300 };
    private readonly Button browse = new() { Text = "Browse…", AutoSize = true };
    private readonly Dictionary<Kind, Control> pages = new();

    private int input = 1;
    private InputAction? imported;

    /// <summary>Raised when the user applies a new action to the current input.</summary>
    public event EventHandler<InputAction>? ActionApplied;

    public ActionEditor()
    {
        kind.Items.AddRange(new object[]
        {
            new Choice<Kind>(Kind.Factory, "Factory default"),
            new Choice<Kind>(Kind.Key, "Key or media key"),
            new Choice<Kind>(Kind.Mouse, "Mouse button / wheel / pointer"),
            new Choice<Kind>(Kind.Keymap, "Keymap switch (Blue / Green / Red)"),
            new Choice<Kind>(Kind.Macro, "Macro"),
            new Choice<Kind>(Kind.Text, "Type text"),
            new Choice<Kind>(Kind.Companion, "Companion action (launch, window, …)"),
            new Choice<Kind>(Kind.Disabled, "Disabled"),
        });
        foreach (MouseFunction f in Enum.GetValues<MouseFunction>())
            mouseCombo.Items.Add(new Choice<MouseFunction>(f, new MouseAction(f).Describe()));
        foreach (KeymapSwitch s in Enum.GetValues<KeymapSwitch>())
            keymapCombo.Items.Add(new Choice<KeymapSwitch>(s, new KeymapSwitchAction(s).Describe()));
        foreach (CompanionKind k in Enum.GetValues<CompanionKind>())
            companionKind.Items.Add(new Choice<CompanionKind>(k, new CompanionAction(k).Describe().Replace(" (Companion)", "")));
        mouseCombo.SelectedIndex = keymapCombo.SelectedIndex = companionKind.SelectedIndex = 0;

        pages[Kind.Factory] = Info("The input does what it did out of the box.");
        pages[Kind.Key] = Stack(keyCombo);
        pages[Kind.Mouse] = Stack(mouseCombo);
        pages[Kind.Keymap] = Stack(keymapCombo, Info("A keymap button is written to all three keymaps, so it works whichever keymap is active. The LED shows the active keymap."));
        pages[Kind.Macro] = macro;
        var textPage = new Panel { Dock = DockStyle.Fill };
        textPage.Controls.Add(text);
        textPage.Controls.Add(textBudget);
        pages[Kind.Text] = textPage;
        pages[Kind.Companion] = Stack(companionKind, companionArg, browse,
            Info("The keypad sends a hotkey and the Companion app performs the action. It only works while the Companion is running."));
        pages[Kind.Disabled] = Info("The input does nothing.");
        pages[Kind.Imported] = Info("");

        foreach (var page in pages.Values) { page.Dock = DockStyle.Fill; page.Visible = false; body.Controls.Add(page); }

        kind.SelectedIndexChanged += (_, _) => ShowPage(CurrentKind());
        text.TextChanged += (_, _) => UpdateTextBudget();
        companionKind.SelectedIndexChanged += (_, _) => UpdateCompanionArg();
        browse.Click += (_, _) => BrowseProgram();
        apply.Click += (_, _) => Apply();

        var header = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown };
        header.Controls.Add(title);
        header.Controls.Add(kind);
        Controls.Add(body);
        Controls.Add(note);
        Controls.Add(apply);
        Controls.Add(header);
        Padding = new Padding(8);
    }

    public static ComboBox KeyCombo()
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, MaxDropDownItems = 25 };
        // Letters and digits, then F-keys, other keys, firmware commands, media keys.
        static int Group(byte code, string name) =>
            code >= 0xE0 ? 4 : code >= 0xC0 ? 3 : name.Length == 1 ? 0 : System.Text.RegularExpressions.Regex.IsMatch(name, @"^F\d+$") ? 1 : 2;
        foreach (var (code, name) in FirmwareCodes.Keys
                     .OrderBy(k => Group(k.Key, k.Value))
                     .ThenBy(k => k.Value.StartsWith('F') && k.Value.Length <= 3 ? k.Value.Length : 0)
                     .ThenBy(k => k.Value, StringComparer.OrdinalIgnoreCase))
            combo.Items.Add(new KeyItem(code, name));
        combo.SelectedIndex = 0;
        return combo;
    }

    /// <summary>Shows the action of <paramref name="inputNumber"/> in the given keymap.</summary>
    public void Show(int inputNumber, Keymap keymap, InputAction action)
    {
        input = inputNumber;
        imported = null;
        title.Text = $"{KeypadLayout.Input(inputNumber).Name}  ({keymap} keymap)";
        note.Text = "";
        if (kind.Items.Count > 8) kind.Items.RemoveAt(8);
        switch (action)
        {
            case var a when a == KeypadLayout.FactoryAction(inputNumber)
                         || (a is MacroAction m && KeypadLayout.FactoryAction(inputNumber) is MacroAction f && m.Steps.SequenceEqual(f.Steps)):
                Select(Kind.Factory);
                break;
            case KeyAction k:
                keyCombo.SelectedItem = keyCombo.Items.Cast<KeyItem>().FirstOrDefault(i => i.Code == k.Code) ?? keyCombo.Items[0];
                Select(Kind.Key);
                break;
            case MouseAction mo:
                mouseCombo.SelectedItem = mouseCombo.Items.Cast<Choice<MouseFunction>>().First(i => i.Value == mo.Function);
                Select(Kind.Mouse);
                break;
            case KeymapSwitchAction s:
                keymapCombo.SelectedItem = keymapCombo.Items.Cast<Choice<KeymapSwitch>>().First(i => i.Value == s.Switch);
                Select(Kind.Keymap);
                break;
            case MacroAction ma:
                macro.Steps = ma.Steps;
                Select(Kind.Macro);
                break;
            case CompanionAction c:
                companionKind.SelectedItem = companionKind.Items.Cast<Choice<CompanionKind>>().First(i => i.Value == c.Kind);
                companionArg.Text = c.Argument;
                Select(Kind.Companion);
                break;
            case DisabledAction:
                Select(Kind.Disabled);
                break;
            default:
                imported = action;
                kind.Items.Add(new Choice<Kind>(Kind.Imported, "Imported, not editable"));
                ((Label)pages[Kind.Imported]).Text = $"{action.Describe()}. Choose another action to replace it.";
                Select(Kind.Imported);
                break;
        }
    }

    private void Select(Kind k) => kind.SelectedItem = kind.Items.Cast<Choice<Kind>>().First(i => i.Value == k);

    private Kind CurrentKind() => kind.SelectedItem is Choice<Kind> c ? c.Value : Kind.Factory;

    private void ShowPage(Kind k)
    {
        foreach (var (pageKind, page) in pages) page.Visible = pageKind == k;
        if (k == Kind.Companion) UpdateCompanionArg();
        if (k == Kind.Text) UpdateTextBudget();
    }

    private void UpdateTextBudget()
    {
        try
        {
            int steps = TextMacro.Build(text.Text).Count;
            textBudget.Text = $"{steps} / {Macro.MaxPairs} steps. US keyboard layout.";
            textBudget.ForeColor = SystemColors.ControlText;
        }
        catch (ProfileException e)
        {
            textBudget.Text = e.Message;
            textBudget.ForeColor = Color.Firebrick;
        }
    }

    private void UpdateCompanionArg()
    {
        var k = companionKind.SelectedItem is Choice<CompanionKind> c ? c.Value : CompanionKind.LaunchProgram;
        bool needsArg = k is CompanionKind.LaunchProgram or CompanionKind.OpenUrl or CompanionKind.TypeText;
        companionArg.Enabled = needsArg;
        browse.Visible = k == CompanionKind.LaunchProgram;
        companionArg.PlaceholderText = k switch
        {
            CompanionKind.LaunchProgram => @"C:\Path\To\Program.exe",
            CompanionKind.OpenUrl => "https://…",
            CompanionKind.TypeText => "Text to type (any length)",
            _ => "",
        };
    }

    private void BrowseProgram()
    {
        using var dialog = new OpenFileDialog { Filter = "Programs (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) companionArg.Text = dialog.FileName;
    }

    private void Apply()
    {
        try
        {
            InputAction action = CurrentKind() switch
            {
                Kind.Factory => KeypadLayout.FactoryAction(input),
                Kind.Key => new KeyAction(((KeyItem)keyCombo.SelectedItem!).Code),
                Kind.Mouse => new MouseAction(((Choice<MouseFunction>)mouseCombo.SelectedItem!).Value),
                Kind.Keymap => new KeymapSwitchAction(((Choice<KeymapSwitch>)keymapCombo.SelectedItem!).Value),
                Kind.Macro => new MacroAction(macro.Steps),
                Kind.Text => new MacroAction(TextMacro.Build(text.Text)),
                Kind.Companion => new CompanionAction(((Choice<CompanionKind>)companionKind.SelectedItem!).Value, companionArg.Text.Trim()),
                Kind.Disabled => new DisabledAction(),
                Kind.Imported => imported ?? throw new ProfileException("Nothing to apply."),
                _ => throw new ProfileException("Choose an action."),
            };
            ProfileCompiler.Encode(input, new InputAssignment(action), CompanionChords.All[0]);   // validate before applying
            note.ForeColor = Color.DimGray;
            note.Text = "Applied.";
            ActionApplied?.Invoke(this, action);
        }
        catch (ProfileException e)
        {
            note.ForeColor = Color.Firebrick;
            note.Text = e.Message;
        }
    }

    private static Label Info(string text) => new() { Text = text, Dock = DockStyle.Top, Height = 60, ForeColor = Color.DimGray };

    private static FlowLayoutPanel Stack(params Control[] controls)
    {
        var panel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        foreach (var c in controls)
        {
            if (c is Label l) { l.AutoSize = false; l.Width = 320; }
            panel.Controls.Add(c);
        }
        return panel;
    }
}
