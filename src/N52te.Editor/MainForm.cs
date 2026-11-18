using System.Diagnostics;

namespace N52te.Editor;

internal sealed class MainForm : Form
{
    private static readonly Color[] KeymapColors = { Color.FromArgb(40, 110, 230), Color.FromArgb(30, 160, 70), Color.FromArgb(215, 50, 50) };

    private Profile profile = KeypadLayout.FactoryProfile();
    private string? path;
    private bool dirty;
    private Keymap keymap = Keymap.Blue;
    private bool loading;

    private readonly KeypadView keypad = new() { Margin = new Padding(8) };
    private readonly ActionEditor editor = new() { Dock = DockStyle.Fill };
    private readonly TextBox nameBox = new() { Width = 220 };
    private readonly ComboBox startKeymap = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly ComboBox polling = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly RadioButton[] keymapButtons = new RadioButton[3];
    private readonly ListBox issues = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly ToolStripStatusLabel status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly SplitContainer split = new() { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2 };

    public MainForm(string? fileToOpen)
    {
        Text = "n52te Editor";
        Width = 1400;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;

        MainMenuStrip = BuildMenu();
        Controls.Add(BuildLayout());
        Controls.Add(MainMenuStrip);
        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(status);
        Controls.Add(statusStrip);

        keypad.SelectedInputChanged += (_, _) => ShowSelected();
        editor.ActionApplied += (_, action) =>
        {
            profile.Set(keymap, keypad.SelectedInput, action, profile.Get(keymap, keypad.SelectedInput).Options);
            MarkDirty();
            RefreshAll();
        };
        nameBox.TextChanged += (_, _) => { if (!loading) { profile.Name = nameBox.Text; MarkDirty(); } };
        startKeymap.SelectedIndexChanged += (_, _) => { if (!loading) { profile.StartKeymap = (Keymap)startKeymap.SelectedIndex; MarkDirty(); RefreshIssues(); } };
        polling.SelectedIndexChanged += (_, _) =>
        {
            if (loading) return;
            profile.PollingIntervalMs = polling.SelectedIndex switch { 1 => 8, 2 => 2, 3 => 1, _ => null };
            MarkDirty();
        };
        FormClosing += (_, e) => { if (!ConfirmDiscard()) e.Cancel = true; };
        // The action panel keeps a fixed width; set it once the window has its real size.
        Shown += (_, _) => split.SplitterDistance = Math.Max(300, split.Width - 520);

        if (fileToOpen is not null) OpenFile(fileToOpen);
        else LoadProfile(profile, null);
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add("&New", null, (_, _) => { if (ConfirmDiscard()) LoadProfile(KeypadLayout.FactoryProfile(), null); });
        file.DropDownItems.Add("&Open…", null, (_, _) => OpenDialog());
        file.DropDownItems.Add("&Save", null, (_, _) => Save(false));
        file.DropDownItems.Add("Save &As…", null, (_, _) => Save(true));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("&Import Belkin profile (.ini)…", null, (_, _) => OpenDialog(belkinOnly: true));
        file.DropDownItems.Add("&Export Belkin profile (.ini)…", null, (_, _) => ExportBelkin());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("E&xit", null, (_, _) => Close());

        var keypadMenu = new ToolStripMenuItem("&Keypad");
        keypadMenu.DropDownItems.Add("&Save to keypad…", null, (_, _) => SaveToKeypad());
        keypadMenu.DropDownItems.Add("&Copy this keymap to…", null, (_, _) => CopyKeymap());
        keypadMenu.DropDownItems.Add("Reset this keymap to &factory", null, (_, _) => ResetKeymap());

        var companion = new ToolStripMenuItem("&Companion");
        companion.DropDownItems.Add("&Use this profile in the Companion", null, (_, _) => UseInCompanion());
        companion.DropDownItems.Add("&App rules (auto keymap per app)…", null, (_, _) => EditAppRules());
        companion.DropDownItems.Add("&Start the Companion", null, (_, _) => StartCompanion());

        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add("&Documentation", null, (_, _) => OpenUrl("https://github.com/mndrake/n52te-windows11-driver/blob/claude/n52te-driver-decompile-tejavx/docs/onboard-editor-plan.md"));
        help.DropDownItems.Add("&About", null, (_, _) => MessageBox.Show(this,
            $"n52te Editor {Application.ProductVersion}\n\nEdits profiles for the Belkin / Razer Nostromo n52te and saves them to the keypad's onboard memory. " +
            "No remapper and no keyboard filter driver.", "About", MessageBoxButtons.OK, MessageBoxIcon.Information));

        menu.Items.AddRange(new ToolStripItem[] { file, keypadMenu, companion, help });
        return menu;
    }

    private Control BuildLayout()
    {
        var settings = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 8, 8, 0) };
        startKeymap.Items.AddRange(new object[] { "Blue", "Green", "Red" });
        polling.Items.AddRange(new object[] { "Don't change", "125 Hz", "500 Hz", "1000 Hz" });
        settings.Controls.AddRange(new Control[]
        {
            Caption("Profile name"), nameBox, Caption("   Start keymap"), startKeymap, Caption("   Polling rate"), polling,
        });

        var keymapRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 4, 8, 0) };
        keymapRow.Controls.Add(Caption("Editing keymap"));
        for (int k = 0; k < 3; k++)
        {
            int index = k;
            var b = new RadioButton
            {
                Appearance = Appearance.Button, Text = ((Keymap)k).ToString(), Width = 90, Height = 30,
                TextAlign = ContentAlignment.MiddleCenter, FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
                BackColor = KeymapColors[k], Checked = k == 0,
            };
            b.CheckedChanged += (_, _) => { if (b.Checked) { keymap = (Keymap)index; RefreshAll(); } };
            keymapButtons[k] = b;
            keymapRow.Controls.Add(b);
        }

        var left = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        left.Controls.Add(keypad);
        var issuesBox = new GroupBox { Text = "Checks", Dock = DockStyle.Bottom, Height = 150 };
        issuesBox.Controls.Add(issues);

        split.Panel1.Controls.Add(left);
        split.Panel1.Controls.Add(issuesBox);
        split.Panel2.Controls.Add(editor);

        var root = new Panel { Dock = DockStyle.Fill };
        root.Controls.Add(split);
        root.Controls.Add(keymapRow);
        root.Controls.Add(settings);
        return root;
    }

    private static Label Caption(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(0, 6, 2, 0) };

    // ---- profile state -------------------------------------------------------------------

    private void LoadProfile(Profile p, string? newPath)
    {
        loading = true;
        profile = p;
        path = newPath;
        nameBox.Text = p.Name;
        startKeymap.SelectedIndex = (int)p.StartKeymap;
        polling.SelectedIndex = p.PollingIntervalMs switch { 8 => 1, 2 => 2, 1 => 3, _ => 0 };
        loading = false;
        dirty = false;
        keymapButtons[0].Checked = true;
        keymap = Keymap.Blue;
        RefreshAll();
        UpdateTitle();
    }

    private void RefreshAll()
    {
        keypad.Show(profile, keymap, KeymapColors[(int)keymap]);
        ShowSelected();
        RefreshIssues();
    }

    private void ShowSelected() => editor.Show(keypad.SelectedInput, keymap, profile.Get(keymap, keypad.SelectedInput).Action);

    private List<ProfileIssue> RefreshIssues()
    {
        var found = ProfileValidator.Validate(profile);
        issues.BeginUpdate();
        issues.Items.Clear();
        if (found.Count == 0) issues.Items.Add("No problems found.");
        foreach (var i in found) issues.Items.Add(i.ToString());
        issues.EndUpdate();
        return found;
    }

    private void MarkDirty()
    {
        dirty = true;
        UpdateTitle();
    }

    private void UpdateTitle() =>
        Text = $"{(dirty ? "*" : "")}{(path is null ? profile.Name : Path.GetFileName(path))} - n52te Editor";

    private bool ConfirmDiscard()
    {
        if (!dirty) return true;
        var answer = MessageBox.Show(this, "Save changes to this profile?", "n52te Editor", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return answer == DialogResult.No || (answer == DialogResult.Yes && Save(false));
    }

    // ---- files ----------------------------------------------------------------------------

    private void OpenDialog(bool belkinOnly = false)
    {
        if (!ConfirmDiscard()) return;
        using var dialog = new OpenFileDialog
        {
            Filter = belkinOnly
                ? "Belkin n52te profile (*.ini)|*.ini"
                : "n52te profiles (*.json;*.ini)|*.json;*.ini|n52te profile (*.json)|*.json|Belkin n52te profile (*.ini)|*.ini",
            InitialDirectory = Directory.Exists(AppPaths.ProfilesFolder) ? AppPaths.ProfilesFolder : null,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) OpenFile(dialog.FileName);
    }

    private void OpenFile(string file)
    {
        try
        {
            var text = File.ReadAllText(file);
            bool belkin = file.EndsWith(".ini", StringComparison.OrdinalIgnoreCase);
            var p = belkin ? BelkinProfileFile.Import(text) : ProfileJson.Deserialize(text);
            LoadProfile(p, belkin ? null : file);
            if (belkin) { dirty = true; UpdateTitle(); }
            status.Text = belkin ? $"Imported {Path.GetFileName(file)}. Save it to keep it as an n52te profile." : $"Opened {file}";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ProfileException or System.Text.Json.JsonException)
        {
            MessageBox.Show(this, $"Couldn't open {Path.GetFileName(file)}:\n\n{e.Message}", "Open", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private bool Save(bool askForName)
    {
        if (askForName || path is null)
        {
            Directory.CreateDirectory(AppPaths.ProfilesFolder);
            using var dialog = new SaveFileDialog
            {
                Filter = "n52te profile (*.json)|*.json",
                FileName = SafeFileName(profile.Name) + ".json",
                InitialDirectory = AppPaths.ProfilesFolder,
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            path = dialog.FileName;
        }
        try
        {
            File.WriteAllText(path, ProfileJson.Serialize(profile));
            dirty = false;
            UpdateTitle();
            status.Text = $"Saved {path}";
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, e.Message, "Save", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private void ExportBelkin()
    {
        if (!CheckNoErrors("export")) return;
        using var dialog = new SaveFileDialog { Filter = "Belkin n52te profile (*.ini)|*.ini", FileName = SafeFileName(profile.Name) + ".ini" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, BelkinProfileFile.Export(profile), System.Text.Encoding.ASCII);
            status.Text = $"Exported {dialog.FileName}";
            MessageBox.Show(this,
                "Exported. To put it on the keypad today: open it in Belkin's original n52te software (v2.1.2) " +
                "in a Windows XP or 7 virtual machine with the keypad passed through, then use \"Load to Synapse\".",
                "Export Belkin profile", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ProfileException)
        {
            MessageBox.Show(this, e.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string SafeFileName(string name)
    {
        var cleaned = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        return cleaned.Length == 0 ? "profile" : cleaned;
    }

    // ---- keypad and Companion -----------------------------------------------------------

    private bool CheckNoErrors(string what)
    {
        var errors = RefreshIssues().Where(i => i.Severity == IssueSeverity.Error).ToList();
        if (errors.Count == 0) return true;
        MessageBox.Show(this, $"Fix these before you {what}:\n\n" + string.Join("\n", errors.Take(10)), "n52te Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private void SaveToKeypad()
    {
        if (!CheckNoErrors("save to the keypad")) return;
        MessageBox.Show(this,
            "Saving directly to the keypad isn't available yet. The connection method is chosen once the " +
            "read-only probe (research\\n52te-probe.ps1) has been run on your keypad and the protocol has been confirmed.\n\n" +
            "Until then: File → Export Belkin profile, and load it with Belkin's original software in a Windows XP/7 virtual machine.",
            "Save to keypad", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CopyKeymap()
    {
        var targets = Enum.GetValues<Keymap>().Where(k => k != keymap).ToArray();
        using var form = new Form { Text = "Copy keymap", Width = 300, Height = 160, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false };
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 16, Top = 16, Width = 250 };
        foreach (var t in targets) combo.Items.Add($"Copy {keymap} to {t}");
        combo.SelectedIndex = 0;
        var ok = new Button { Text = "Copy", DialogResult = DialogResult.OK, Left = 110, Top = 60, Width = 75 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 190, Top = 60, Width = 75 };
        form.Controls.AddRange(new Control[] { combo, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        if (form.ShowDialog(this) != DialogResult.OK) return;
        var target = targets[combo.SelectedIndex];
        profile.Keymaps[(int)target].Inputs = profile.Keymaps[(int)keymap].Inputs.ToArray();
        MarkDirty();
        RefreshAll();
        status.Text = $"Copied {keymap} to {target}.";
    }

    private void ResetKeymap()
    {
        if (MessageBox.Show(this, $"Reset every input of the {keymap} keymap to the factory layout?", "Reset keymap", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        for (int n = 1; n <= KeypadLayout.InputCount; n++)
            profile.Keymaps[(int)keymap].Inputs[n - 1] = new InputAssignment(KeypadLayout.FactoryAction(n));
        MarkDirty();
        RefreshAll();
    }

    private void UseInCompanion()
    {
        if (!CheckNoErrors("hand it to the Companion")) return;
        try
        {
            AppPaths.WriteActiveProfile(profile);
            var bindings = ProfileCompiler.Compile(profile).Companion;
            status.Text = $"The Companion now follows \"{profile.Name}\" ({bindings.Count} Companion action(s), {profile.AppRules.Count} app rule(s)).";
            if (bindings.Count > 0)
                MessageBox.Show(this,
                    "Done. The Companion reacts to these hotkeys, which the keypad sends once this profile is on it:\n\n" +
                    string.Join("\n", bindings.Select(b => $"{b.Chord.Display}  →  {b.Action.Describe()}  ({b.Keymap}, {KeypadLayout.Input(b.Input).Name})").Distinct()),
                    "Companion", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ProfileException)
        {
            MessageBox.Show(this, e.Message, "Companion", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void EditAppRules()
    {
        using var dialog = new AppRulesDialog(profile.AppRules);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        profile.AppRules = dialog.Rules;
        MarkDirty();
        RefreshIssues();
    }

    private void StartCompanion()
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "n52te-companion.exe");
        if (!File.Exists(exe))
        {
            MessageBox.Show(this, $"n52te-companion.exe was not found next to the editor ({AppContext.BaseDirectory}).", "Companion", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
    }

    private void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception e) { status.Text = e.Message; }
    }
}

/// <summary>Edits the Companion's "keymap per application" rules.</summary>
internal sealed class AppRulesDialog : Form
{
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };

    public List<AppRule> Rules { get; private set; } = new();

    public AppRulesDialog(IEnumerable<AppRule> rules)
    {
        Text = "App rules";
        Width = 520;
        Height = 380;
        StartPosition = FormStartPosition.CenterParent;
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "exe", HeaderText = "Program (e.g. game.exe)" });
        var km = new DataGridViewComboBoxColumn { Name = "keymap", HeaderText = "Keymap" };
        km.Items.AddRange("Blue", "Green", "Red");
        grid.Columns.Add(km);
        foreach (var r in rules) grid.Rows.Add(r.ExecutableName, r.Keymap.ToString());

        var help = new Label
        {
            Dock = DockStyle.Top, Height = 40, ForeColor = Color.DimGray,
            Text = "When one of these programs comes to the front, the Companion switches the keypad to that keymap. " +
                   "Needs the Companion running and a direct keypad connection (coming after the probe).",
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 90 };
        var buttons = new Panel { Dock = DockStyle.Bottom, Height = 34 };
        buttons.Controls.Add(ok);
        Controls.Add(grid);
        Controls.Add(help);
        Controls.Add(buttons);
        AcceptButton = ok;
        FormClosing += (_, _) =>
        {
            if (DialogResult != DialogResult.OK) return;
            Rules = grid.Rows.Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow && r.Cells[0].Value is string exe && exe.Trim().Length > 0)
                .Select(r => new AppRule(((string)r.Cells[0].Value!).Trim(), Enum.TryParse<Keymap>(r.Cells[1].Value as string, out var k) ? k : Keymap.Blue))
                .ToList();
        };
    }
}
