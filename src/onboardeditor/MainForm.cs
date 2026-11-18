// The main window, laid out and behaving like Belkin's n52te Editor v2.1.2: Map 1/2/3 tabs, a label
// box for each of the 27 inputs with a right-click menu, polling rate, profile buttons, Apply
// Changes and Reset Keymap. The keypad is read and written through src\core (UsbDk); the keypad
// doesn't type for the few seconds an operation takes.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace N52te
{
    internal sealed class MainForm : Form
    {
        private static readonly string ProfileDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "n52te", "Profiles");

        private readonly Settings settings = Settings.Load();
        private Skin skin;
        private float scale = 1f;
        private Profile profile = Profile.Factory();
        private string profilePath;      // null: not saved as a file
        private bool modified;
        private int km;                  // keymap being edited (0 Blue, 1 Red, 2 Green)
        private bool connected;
        private int polling = -1;        // 0 125 Hz, 1 500 Hz, 2 1000 Hz, -1 unknown
        private string status = "";
        private bool busy;
        private Rectangle hover = Rectangle.Empty, pressed = Rectangle.Empty;
        private int hoverInput, menuInput;
        private readonly Timer presence = new Timer { Interval = 3000 };
        private readonly ToolTip tip = new ToolTip();
        private readonly MacroEditor macros;

        public MainForm(bool live)
        {
            Text = "n52te Editor";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = Color.Black;
            Font = new Font("Arial", 9f);
            using (Graphics g = CreateGraphics()) scale = settings.Scale > 0 ? settings.Scale : Math.Max(1f, (float)Math.Round(g.DpiX / 96f * 1.25f * 4) / 4);
            macros = new MacroEditor(this);
            LoadSkin(live ? settings.BelkinSkin : null);
            ApplyScale();
            if (!live) return;

            Directory.CreateDirectory(ProfileDir);
            presence.Tick += (s, e) => CheckPresence();
            presence.Start();
            Shown += (s, e) =>
            {
                macros.ShowState();
                if (UsbDkTransport_IsPresent()) ReadFromKeypad(false);
                else
                {
                    connected = false;
                    SetStatus(UsbDk.IsInstalled ? "n52te not connected" : "UsbDk is not installed");
                }
            };
        }

        // ---- look ----

        private void LoadSkin(string belkinFolder)
        {
            Skin old = skin;
            try { skin = Skin.IsBelkinSkin(belkinFolder) ? Skin.LoadBelkin(belkinFolder) : Skin.BuiltIn(); }
            catch (Exception e)
            {
                skin = Skin.BuiltIn();
                status = "couldn't load Belkin's artwork: " + e.Message;
            }
            if (old != null) old.Dispose();
            if (macros != null) macros.SetLook(skin.Source, scale);
            Invalidate();
        }

        private void ApplyScale()
        {
            ClientSize = new Size((int)Math.Round(SkinLayout.Width * scale), (int)Math.Round(SkinLayout.Height * scale));
            if (macros != null) macros.SetLook(skin.Source, scale);
            Invalidate();
        }

        public void SetStatusText(string s) { SetStatus(s); }

        // Development aid and README picture: the window (and optionally the macro editor with a
        // sample macro) rendered offline, with the factory profile.
        public void SaveScreenshot(string path, string belkinFolder, int highlight, bool withMacroEditor)
        {
            LoadSkin(belkinFolder);
            scale = 1.5f;
            ApplyScale();
            connected = true;
            polling = 0;
            if (highlight > 0) { hoverInput = highlight; hover = skin.Box(highlight); }
            status = withMacroEditor ? "" : "screenshot";
            CreateControl();
            using (var main = new Bitmap(ClientSize.Width, ClientSize.Height))
            {
                DrawToBitmap(main, new Rectangle(Point.Empty, ClientSize));
                if (!withMacroEditor) { main.Save(path, System.Drawing.Imaging.ImageFormat.Png); return; }
                macros.Demo();
                using (Bitmap panel = macros.Render())
                using (var both = new Bitmap(main.Width + panel.Width - 2, main.Height))
                {
                    using (Graphics g = Graphics.FromImage(both))
                    {
                        g.Clear(Color.Transparent);
                        g.DrawImage(main, 0, 0);
                        g.DrawImage(panel, main.Width - 2, (int)(30 * scale));
                    }
                    both.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.ScaleTransform(scale, scale);
            g.DrawImage(skin.Normal(km, connected), 0, 0, SkinLayout.Width, SkinLayout.Height);

            // Active tab, selected polling rate, and the input whose menu is open: Belkin's "pressed" look.
            Cut(g, skin.Down(km), SkinLayout.MapTabs[km]);
            if (polling >= 0) Cut(g, skin.Down(km), SkinLayout.Polling[polling]);
            if (menuInput > 0) CutInput(g, skin.Down(km), menuInput);
            if (hoverInput > 0) CutInput(g, skin.Over(km), hoverInput);
            else if (!hover.IsEmpty) Cut(g, pressed == hover ? skin.Down(km) : skin.Over(km), hover);

            using (var font = new Font("Arial", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var fill = new SolidBrush(skin.BoxFill))
            using (var textBrush = new SolidBrush(skin.Text))
            using (var grey = new SolidBrush(Color.FromArgb(120, 124, 130)))
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                for (int n = 1; n <= KeypadLayout.InputCount; n++)
                {
                    Rectangle box = skin.Box(n);
                    var inner = n <= 14 ? Rectangle.FromLTRB(box.X + 21, box.Y + 3, box.Right - 3, box.Bottom - 3) : Rectangle.Inflate(box, -3, -3);
                    g.FillRectangle(fill, inner);
                    InputRecord r = profile.Keymaps[km][n - 1];
                    g.DrawString(Assignment.Short(n, r), font, Assignment.IsInactive(n, r) ? grey : textBrush, inner, center);
                }

                // Profile row: name, linked application, auto-switching.
                var row = Rectangle.Inflate(SkinLayout.Profile, -4, -4);
                using (var white = new SolidBrush(Color.FromArgb(230, 232, 236)))
                using (var left = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                {
                    g.DrawString((profilePath == null ? profile.Name + " (not saved)" : profile.Name) + (modified ? " *" : ""), font, white, new Rectangle(row.X + 4, row.Y, 124, row.Height), left);
                    g.DrawString("-", font, grey, new Rectangle(row.X + 132, row.Y, 100, row.Height), left);
                    g.DrawString("Off", font, grey, new Rectangle(row.X + 245, row.Y, 70, row.Height), left);
                }

                if (connected && status.Length > 0)
                    using (var bold = new Font("Arial", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var shadow = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                    using (var white = new SolidBrush(Color.FromArgb(240, 242, 245)))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    {
                        RectangleF s = SkinLayout.Status;
                        g.DrawString(status, bold, shadow, new RectangleF(s.X + 1, s.Y + 1, s.Width, s.Height), sf);
                        g.DrawString(status, bold, white, s, sf);
                    }
            }
        }

        private void Cut(Graphics g, Bitmap from, Rectangle r)
        {
            r.Inflate(2, 2);
            g.DrawImage(from, r, r, GraphicsUnit.Pixel);
        }

        private void CutInput(Graphics g, Bitmap from, int n)
        {
            Cut(g, from, skin.Box(n));
            Cut(g, from, SkinLayout.Picture[n - 1]);
            Cut(g, from, SkinLayout.IconFor(n));
        }

        // ---- hit testing ----

        private Point Logical(Point p) { return new Point((int)(p.X / scale), (int)(p.Y / scale)); }

        private int InputAt(Point p)
        {
            for (int n = 1; n <= KeypadLayout.InputCount; n++)
                if (skin.Box(n).Contains(p) || Rectangle.Inflate(SkinLayout.Picture[n - 1], 2, 2).Contains(p)) return n;
            return 0;
        }

        private Rectangle ButtonAt(Point p)
        {
            foreach (Rectangle r in Buttons()) if (r.Contains(p)) return r;
            return Rectangle.Empty;
        }

        private IEnumerable<Rectangle> Buttons()
        {
            yield return SkinLayout.Minimize;
            yield return SkinLayout.Close;
            foreach (Rectangle r in SkinLayout.MapTabs) yield return r;
            foreach (Rectangle r in SkinLayout.Polling) yield return r;
            yield return SkinLayout.AutoSwitch;
            yield return SkinLayout.ShowOnScreen;
            yield return SkinLayout.New;
            yield return SkinLayout.Save;
            yield return SkinLayout.SaveAs;
            yield return SkinLayout.Import;
            yield return SkinLayout.Delete;
            yield return SkinLayout.Apply;
            yield return SkinLayout.Reset;
            yield return SkinLayout.Profile;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Point p = Logical(e.Location);
            int n = InputAt(p);
            Rectangle b = n > 0 ? Rectangle.Empty : ButtonAt(p);
            if (n != hoverInput || b != hover)
            {
                hoverInput = n;
                hover = b;
                Cursor = n > 0 || !b.IsEmpty ? Cursors.Hand : Cursors.Default;
                tip.SetToolTip(this, Tooltip(n, b));
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoverInput = 0;
            hover = Rectangle.Empty;
            Invalidate();
            base.OnMouseLeave(e);
        }

        private string Tooltip(int n, Rectangle b)
        {
            if (n > 0) return KeypadLayout.InputNames[n - 1] + ": " + KeymapImage.DescribeRecord(n, profile.Keymaps[km][n - 1]);
            if (b == SkinLayout.AutoSwitch || b == SkinLayout.ShowOnScreen) return "Not available: profiles are switched by writing them to the keypad (Apply Changes).";
            if (b == SkinLayout.Apply) return "Write all three keymaps to the keypad. It doesn't type for the few seconds this takes.";
            if (b == SkinLayout.Reset) return "Reset this keymap to the factory layout.";
            for (int i = 0; i < 3; i++) if (b == SkinLayout.Polling[i]) return "Keyboard polling rate; takes effect after you unplug and replug the keypad.";
            for (int i = 0; i < 3; i++) if (b == SkinLayout.MapTabs[i]) return "Edit the " + KeypadLayout.KeymapNames[i] + " keymap; the keypad starts in the tab shown when you apply.";
            return "";
        }

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Point p = Logical(e.Location);
            int n = InputAt(p);
            Rectangle b = ButtonAt(p);
            if (n > 0) { ShowInputMenu(n, e.Location); return; }
            if (!b.IsEmpty && e.Button == MouseButtons.Left) { pressed = b; Invalidate(); return; }
            if (e.Button == MouseButtons.Right) { ShowWindowMenu(e.Location); return; }
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // drag the window by its background
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            Rectangle b = ButtonAt(Logical(e.Location));
            Rectangle was = pressed;
            pressed = Rectangle.Empty;
            Invalidate();
            if (!was.IsEmpty && b == was) ClickButton(b);
            base.OnMouseUp(e);
        }

        private void ClickButton(Rectangle b)
        {
            if (b == SkinLayout.Close) { Close(); return; }
            if (b == SkinLayout.Minimize) { WindowState = FormWindowState.Minimized; return; }
            for (int i = 0; i < 3; i++)
                if (b == SkinLayout.MapTabs[i])
                {
                    km = i;
                    if (profile.StartKeymap != i) { profile.StartKeymap = i; modified = true; }
                    SetStatus(KeypadLayout.KeymapNames[i] + " keymap");
                    return;
                }
            for (int i = 0; i < 3; i++) if (b == SkinLayout.Polling[i]) { SetPolling(i); return; }
            if (b == SkinLayout.AutoSwitch || b == SkinLayout.ShowOnScreen) { SetStatus("not available in this version"); return; }
            if (b == SkinLayout.New) NewProfile();
            else if (b == SkinLayout.Save) SaveProfile(false);
            else if (b == SkinLayout.SaveAs) SaveProfile(true);
            else if (b == SkinLayout.Import) ImportProfile();
            else if (b == SkinLayout.Delete) DeleteProfile();
            else if (b == SkinLayout.Apply) ApplyChanges();
            else if (b == SkinLayout.Reset) ResetKeymap();
            else if (b == SkinLayout.Profile) ShowProfileMenu();
        }

        private void SetStatus(string s)
        {
            status = s;
            Invalidate();
        }

        private void Changed()
        {
            modified = true;
            Invalidate();
        }

        // ---- the input menu (Belkin's right-click menu) ----

        private void ShowInputMenu(int n, Point at)
        {
            if (busy) return;
            menuInput = n;
            Invalidate();
            string name = KeypadLayout.InputNames[n - 1];
            InputRecord r = profile.Keymaps[km][n - 1];
            var m = new ContextMenuStrip { ShowItemToolTips = true };
            m.Items.Add(new ToolStripMenuItem(name + " - " + KeypadLayout.KeymapNames[km] + " keymap") { Enabled = false });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Single Key...", null, (s, e) => SingleKey(n));
            m.Items.Add(MacroMenu(n));
            m.Items.Add(Submenu("Keymap", Assignment.Keymap, n));
            m.Items.Add(Submenu("Mouse Button", Assignment.Mouse, n));
            m.Items.Add(Submenu("Basic Commands", Assignment.BasicCommands, n));
            m.Items.Add(Submenu("Additional Commands", Assignment.AdditionalCommands, n));
            var media = Submenu("Media Functions", Assignment.Media, n);
            media.DropDownItems.Add(new ToolStripSeparator());
            media.DropDownItems.Add(Submenu("More Media and Browser Keys", Assignment.MoreMedia, n));
            m.Items.Add(media);
            m.Items.Add(new ToolStripMenuItem("Load Profile") { Enabled = false, ToolTipText = "Belkin's background service did this; it doesn't exist on Windows 11." });
            m.Items.Add(new ToolStripMenuItem("Load Application") { Enabled = false, ToolTipText = "Belkin's background service did this; it doesn't exist on Windows 11." });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Reset Key to Default", null, (s, e) => ResetInput(n));
            m.Items.Add("Disable Button", null, (s, e) => SetCode(n, FirmwareCodes.Disabled));
            m.Closed += (s, e) => { menuInput = 0; Invalidate(); };
            m.Show(this, at);
        }

        // Macro: the saved macros (as in Belkin's menu), the macro open in the macro editor, and editing.
        private ToolStripMenuItem MacroMenu(int n)
        {
            var sub = new ToolStripMenuItem("Macro");
            foreach (string saved in SavedMacro.List())
            {
                string file = SavedMacro.PathFor(saved);
                sub.DropDownItems.Add(saved, null, (s, e) =>
                {
                    try { AssignMacro(n, SavedMacro.Read(file)); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "Macro", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                });
            }
            if (sub.DropDownItems.Count > 0) sub.DropDownItems.Add(new ToolStripSeparator());
            sub.DropDownItems.Add(new ToolStripMenuItem("The Macro Editor's Macro (" + macros.MacroName + ")", null, (s, e) => AssignMacro(n, macros.Current())) { Enabled = macros.HasSteps });
            InputRecord r = profile.Keymaps[km][n - 1];
            bool isMacro = r.Code == KeypadLayout.MacroMarker[n - 1] && r.FirmwareSequence == null && r.Sequence[0] != 0;
            sub.DropDownItems.Add(new ToolStripMenuItem("Edit This Input's Macro in the Macro Editor", null, (s, e) =>
            {
                var m = new SavedMacro { Name = KeypadLayout.InputNames[n - 1] + " (" + KeypadLayout.KeymapNames[km] + ")", Sequence = (byte[])r.Sequence.Clone(), Options = (byte[])r.Options.Clone() };
                macros.LoadMacro(m, null);
                if (!macros.Expanded) macros.Toggle();
            }) { Enabled = isMacro });
            sub.DropDownItems.Add("Type the Steps...", null, (s, e) => EditMacro(n));
            sub.DropDownItems.Add(new ToolStripSeparator());
            sub.DropDownItems.Add(macros.Expanded ? "Close the Macro Editor" : "Open the Macro Editor", null, (s, e) => macros.Toggle());
            return sub;
        }

        private void AssignMacro(int n, SavedMacro m)
        {
            if (m.Sequence[0] == 0) { SetStatus("that macro has no steps"); return; }
            var o = (byte[])m.Options.Clone();
            if (o[0] > 2) o[0] = 0;
            Replace(n, new InputRecord { Code = KeypadLayout.MacroMarker[n - 1], Sequence = (byte[])m.Sequence.Clone(), Options = o });
            SetStatus("macro '" + (m.Name.Length > 0 ? m.Name : "unnamed") + "' assigned");
        }

        private ToolStripMenuItem Submenu(string text, Assignment.Item[] items, int n)
        {
            var sub = new ToolStripMenuItem(text);
            byte current = profile.Keymaps[km][n - 1].Code;
            foreach (Assignment.Item it in items)
            {
                Assignment.Item item = it;
                var mi = new ToolStripMenuItem(item.Text) { Checked = current == item.Code, Enabled = item.Works, ToolTipText = item.Note };
                mi.Click += (s, e) => SetCode(n, item.Code);
                sub.DropDownItems.Add(mi);
            }
            return sub;
        }

        private void SingleKey(int n)
        {
            using (var d = new SingleKeyDialog(KeypadLayout.InputNames[n - 1]))
                if (d.ShowDialog(this) == DialogResult.OK) SetCode(n, d.Code);
        }

        private void EditMacro(int n)
        {
            InputRecord r = profile.Keymaps[km][n - 1];
            bool isMacro = r.Code == KeypadLayout.MacroMarker[n - 1];
            using (var d = new MacroDialog(KeypadLayout.InputNames[n - 1], r, isMacro))
            {
                if (d.ShowDialog(this) != DialogResult.OK || d.Sequence == null) return;
                var rec = new InputRecord { Code = KeypadLayout.MacroMarker[n - 1], Sequence = d.Sequence, Options = d.Options };
                Replace(n, rec);
            }
        }

        private void SetCode(int n, byte code)
        {
            var rec = profile.Keymaps[km][n - 1].Clone();
            rec.Code = code;
            rec.Sequence = new byte[InputRecord.SequenceSize];
            rec.FirmwareSequence = null;
            Replace(n, rec);
        }

        // Keymap switches go into all three keymaps (as in Belkin's editor and as the keypad needs).
        // Replacing a switch replaces it in the other keymaps too, so no keymap keeps a lone switch.
        private void Replace(int n, InputRecord rec)
        {
            InputRecord old = profile.Keymaps[km][n - 1];
            bool wasSwitch = FirmwareCodes.IsKeymapSwitch(old.Code);
            var also = new List<string>();
            for (int k = 0; k < KeypadLayout.KeymapCount; k++)
            {
                if (k == km) continue;
                if (FirmwareCodes.IsKeymapSwitch(rec.Code) || (wasSwitch && profile.Keymaps[k][n - 1].Code == old.Code))
                {
                    profile.Keymaps[k][n - 1] = rec.Clone();
                    also.Add(KeypadLayout.KeymapNames[k]);
                }
            }
            profile.Keymaps[km][n - 1] = rec;
            SetStatus(also.Count > 0 ? "also set in " + string.Join(" and ", also.ToArray()) : "");
            Changed();
        }

        private void ResetInput(int n)
        {
            Replace(n, Profile.Factory().Keymaps[km][n - 1]);
        }

        // Like Belkin's Reset Keymap: the shown keymap gets the factory layout; the D-pad diagonals
        // get their factory macro in all three keymaps.
        private void ResetKeymap()
        {
            if (MessageBox.Show(this, "Reset the " + KeypadLayout.KeymapNames[km] + " keymap to the factory layout?", "Reset Keymap", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            Profile f = Profile.Factory();
            for (int n = 1; n <= KeypadLayout.InputCount; n++)
            {
                if (n >= 24) for (int k = 0; k < KeypadLayout.KeymapCount; k++) profile.Keymaps[k][n - 1] = f.Keymaps[k][n - 1].Clone();
                else
                {
                    if (FirmwareCodes.IsKeymapSwitch(profile.Keymaps[km][n - 1].Code)) Replace(n, f.Keymaps[km][n - 1].Clone());
                    profile.Keymaps[km][n - 1] = f.Keymaps[km][n - 1].Clone();
                }
            }
            SetStatus(KeypadLayout.KeymapNames[km] + " keymap reset");
            Changed();
        }

        // ---- window menu (right-click on the background) ----

        private void ShowWindowMenu(Point at)
        {
            var m = new ContextMenuStrip();
            m.Items.Add("Read from Keypad", null, (s, e) => ReadFromKeypad(true)).Enabled = !busy;
            m.Items.Add(macros.Expanded ? "Close the Macro Editor" : "Macro Editor", null, (s, e) => macros.Toggle());
            m.Items.Add("Apply Changes", null, (s, e) => ApplyChanges()).Enabled = !busy;
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Open Profile File...", null, (s, e) => OpenProfileFile());
            m.Items.Add("Export Profile File...", null, (s, e) => ExportProfileFile());
            m.Items.Add(new ToolStripSeparator());
            var art = new ToolStripMenuItem("Artwork");
            art.DropDownItems.Add(new ToolStripMenuItem("Built-in", null, (s, e) => { settings.BelkinSkin = null; settings.Save(); LoadSkin(null); }) { Checked = skin.Source == null });
            art.DropDownItems.Add(new ToolStripMenuItem("Belkin's (from your copy of the v2.1.2 software)...", null, (s, e) => ChooseBelkinSkin()) { Checked = skin.Source != null });
            m.Items.Add(art);
            var size = new ToolStripMenuItem("Size");
            foreach (float f in new[] { 1f, 1.25f, 1.5f, 1.75f, 2f, 2.5f })
            {
                float v = f;
                size.DropDownItems.Add(new ToolStripMenuItem((v * 100).ToString(CultureInfo.InvariantCulture) + "%", null, (s, e) => { scale = v; settings.Scale = v; settings.Save(); ApplyScale(); }) { Checked = Math.Abs(scale - v) < 0.01 });
            }
            m.Items.Add(size);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Profiles Folder", null, (s, e) => System.Diagnostics.Process.Start(ProfileDir));
            m.Items.Add("Backups Folder", null, (s, e) => { Directory.CreateDirectory(KeypadSession.BackupDir); System.Diagnostics.Process.Start(KeypadSession.BackupDir); });
            m.Items.Add("About", null, (s, e) => MessageBox.Show(this,
                "n52te Editor\n\nEdits the Belkin n52te's three onboard keymaps and writes them into the keypad, which runs them by itself.\n\n" +
                "Layout after Belkin's n52te Editor v2.1.2. Belkin's artwork is only shown when you point the editor at your own copy of Belkin's software; it isn't part of this program.\n\n" +
                "Talks to the keypad through UsbDk. Every write keeps a backup in Documents\\n52te keymaps.", "About n52te Editor"));
            m.Items.Add("Close", null, (s, e) => Close());
            m.Show(this, at);
        }

        private void ChooseBelkinSkin()
        {
            using (var d = new FolderBrowserDialog { Description = "Choose the skin_English folder from Belkin's n52te v2.1.2 software (it contains main.ini and main_normal1_2.jpg)." })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (!Skin.IsBelkinSkin(d.SelectedPath))
                {
                    MessageBox.Show(this, "That folder doesn't contain Belkin's main window skin (main.ini, main_normal1_2.jpg).", "Belkin's artwork");
                    return;
                }
                settings.BelkinSkin = d.SelectedPath;
                settings.Save();
                LoadSkin(d.SelectedPath);
            }
        }

        // ---- profiles ----

        private bool ConfirmDiscard()
        {
            if (!modified) return true;
            DialogResult r = MessageBox.Show(this, "Save the changes to '" + profile.Name + "' first?", "n52te Editor", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel) return false;
            return r == DialogResult.No || SaveProfile(false);
        }

        private void LoadProfile(Profile p, string path)
        {
            profile = p;
            profilePath = path;
            modified = false;
            km = p.StartKeymap;
            Invalidate();
        }

        private string AskName(string title, string initial)
        {
            using (var d = new NameDialog(title, "Profile name:", initial))
                return d.ShowDialog(this) == DialogResult.OK ? d.Value : null;
        }

        private void NewProfile()
        {
            if (!ConfirmDiscard()) return;
            string name = AskName("New Profile", "New Profile");
            if (name == null) return;
            Profile p = Profile.Factory();
            p.Name = name;
            LoadProfile(p, null);
            modified = true;
            SaveProfile(false);
        }

        private bool SaveProfile(bool asNew)
        {
            if (asNew || profilePath == null)
            {
                string name = AskName(asNew ? "Save As" : "Save Profile", profile.Name);
                if (name == null) return false;
                string path = Path.Combine(ProfileDir, name + ".ini");
                if (File.Exists(path) && !string.Equals(path, profilePath, StringComparison.OrdinalIgnoreCase)
                    && MessageBox.Show(this, "Replace the existing profile '" + name + "'?", "Save", MessageBoxButtons.OKCancel) != DialogResult.OK) return false;
                profile.Name = name;
                profilePath = path;
            }
            ProfileIni.Write(profile, profilePath);
            modified = false;
            SetStatus("saved '" + profile.Name + "'");
            return true;
        }

        private void ImportProfile()
        {
            if (!ConfirmDiscard()) return;
            using (var d = new OpenFileDialog { Filter = "n52te profiles (*.ini)|*.ini", Title = "Import a Belkin n52te profile" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                Profile p = ReadIni(d.FileName);
                if (p == null) return;
                if (string.IsNullOrEmpty(p.Name)) p.Name = Path.GetFileNameWithoutExtension(d.FileName);
                string path = Path.Combine(ProfileDir, p.Name + ".ini");
                if (File.Exists(path) && MessageBox.Show(this, "Replace the existing profile '" + p.Name + "'?", "Import", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
                ProfileIni.Write(p, path);
                LoadProfile(p, path);
                SetStatus("imported '" + p.Name + "'");
            }
        }

        private void DeleteProfile()
        {
            if (profilePath == null || !File.Exists(profilePath)) { SetStatus("this profile isn't saved"); return; }
            if (MessageBox.Show(this, "Delete the profile '" + profile.Name + "'? The keypad keeps whatever was written to it.", "Delete", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            File.Delete(profilePath);
            profilePath = null;
            modified = true;
            SetStatus("deleted the profile file");
        }

        private Profile ReadIni(string path)
        {
            try
            {
                var warnings = new List<string>();
                Profile p = ProfileIni.Read(path, warnings);
                if (warnings.Count > 0) MessageBox.Show(this, string.Join("\n", warnings.ToArray()), "Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return p;
            }
            catch (Exception e)
            {
                MessageBox.Show(this, e.Message, "Profile", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private void ShowProfileMenu()
        {
            var m = new ContextMenuStrip();
            foreach (string path in Directory.GetFiles(ProfileDir, "*.ini"))
            {
                string p = path;
                var item = new ToolStripMenuItem(Path.GetFileNameWithoutExtension(p), null, (s, e) =>
                {
                    if (!ConfirmDiscard()) return;
                    Profile loaded = ReadIni(p);
                    if (loaded != null) LoadProfile(loaded, p);
                }) { Checked = string.Equals(p, profilePath, StringComparison.OrdinalIgnoreCase) };
                m.Items.Add(item);
            }
            if (m.Items.Count > 0) m.Items.Add(new ToolStripSeparator());
            m.Items.Add("What the keypad holds now (Read from Keypad)", null, (s, e) => ReadFromKeypad(true)).Enabled = !busy;
            m.Items.Add("Factory Profile", null, (s, e) => { if (ConfirmDiscard()) { LoadProfile(Profile.Factory(), null); modified = true; } });
            m.Show(this, new Point((int)(SkinLayout.Profile.X * scale), (int)(SkinLayout.Profile.Bottom * scale)));
        }

        private void OpenProfileFile()
        {
            if (!ConfirmDiscard()) return;
            using (var d = new OpenFileDialog { Filter = "n52te profiles (*.ini)|*.ini" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                Profile p = ReadIni(d.FileName);
                if (p != null) LoadProfile(p, d.FileName);
            }
        }

        private void ExportProfileFile()
        {
            using (var d = new SaveFileDialog { Filter = "n52te profiles (*.ini)|*.ini", FileName = profile.Name + ".ini" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                ProfileIni.Write(profile, d.FileName);
                SetStatus("exported");
            }
        }

        // ---- keypad ----

        private bool UsbDkTransport_IsPresent()
        {
            try { return UsbDkTransport.IsPresent(); } catch (Exception) { return false; }
        }

        private void CheckPresence()
        {
            if (busy) return;
            bool now = UsbDkTransport_IsPresent();
            if (now == connected) return;
            connected = now;
            SetStatus(now ? "n52te connected" : "");
        }

        private async Task<T> OnKeypad<T>(string doing, Func<KeypadClient, T> work)
        {
            busy = true;
            SetStatus(doing);
            UseWaitCursor = true;
            try
            {
                return await Task.Run(() =>
                {
                    using (KeypadClient pad = KeypadClient.OpenUsbDk()) return work(pad);
                });
            }
            finally
            {
                busy = false;
                UseWaitCursor = false;
            }
        }

        private async void ReadFromKeypad(bool ask)
        {
            if (busy || (ask && !ConfirmDiscard())) return;
            try
            {
                var warnings = new List<string>();
                int rate = -1;
                Profile p = await OnKeypad("reading the keypad...", pad =>
                {
                    Profile read = KeypadSession.Read(pad, "Keypad", warnings);
                    byte[] r = pad.PollingRate();
                    if (KeypadClient.ChecksumOk(r)) rate = r[5] == 8 ? 0 : r[5] == 2 ? 1 : r[5] == 1 ? 2 : -1;
                    return read;
                });
                connected = true;
                polling = rate;
                LoadProfile(p, null);
                SetStatus("read from the keypad");
                if (warnings.Count > 0) MessageBox.Show(this, string.Join("\n", warnings.ToArray()), "Read from Keypad", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception e)
            {
                connected = UsbDkTransport_IsPresent();
                SetStatus(connected ? "couldn't read the keypad" : "n52te not connected");
                if (ask) MessageBox.Show(this, e.Message, "Read from Keypad", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ApplyChanges()
        {
            if (busy) return;
            List<string> issues = profile.Check();
            try
            {
                Profile onKeypad = await OnKeypad("comparing with the keypad...", pad => KeypadSession.Read(pad, "Keypad", new List<string>()));
                List<string> diffs = KeypadSession.Differences(onKeypad, profile);
                if (diffs.Count == 0) { SetStatus("the keypad already has this profile"); return; }
                var sb = new StringBuilder();
                sb.AppendLine("Write these changes to the keypad?").AppendLine();
                for (int i = 0; i < diffs.Count && i < 20; i++) sb.AppendLine(diffs[i]);
                if (diffs.Count > 20) sb.AppendLine("... and " + (diffs.Count - 20) + " more");
                if (issues.Count > 0)
                {
                    sb.AppendLine().AppendLine("Please note:");
                    foreach (string i in issues) sb.AppendLine("- " + i);
                }
                sb.AppendLine().Append("The keypad doesn't type for the few seconds this takes. A backup of each changed keymap is kept in Documents\\n52te keymaps.");
                if (MessageBox.Show(this, sb.ToString(), "Apply Changes", MessageBoxButtons.OKCancel, issues.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Question) != DialogResult.OK)
                {
                    SetStatus("");
                    return;
                }
                Profile toWrite = profile;
                string error = await OnKeypad("writing...", pad => KeypadSession.Apply(pad, toWrite, line => BeginInvoke((Action)(() => SetStatus(Shorten(line))))));
                if (error != null)
                {
                    SetStatus("writing failed");
                    MessageBox.Show(this, error, "Apply Changes", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                connected = true;
                SetStatus("changes applied");
            }
            catch (Exception e)
            {
                SetStatus("couldn't reach the keypad");
                MessageBox.Show(this, e.Message, "Apply Changes", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string Shorten(string line)
        {
            if (line.StartsWith("writing keymap")) return "writing " + line.Substring(line.IndexOf('(') + 1).TrimEnd('.', ')').Replace(")", "") + "...";
            if (line.StartsWith("backup")) return "backing up...";
            return line.Length > 40 ? line.Substring(0, 40) : line;
        }

        private async void SetPolling(int i)
        {
            if (busy) return;
            byte[] ms = { 8, 2, 1 };
            try
            {
                await OnKeypad("setting the polling rate...", pad => { pad.SetPollingInterval(ms[i]); return 0; });
                polling = i;
                connected = true;
                SetStatus("replug the keypad to apply");
            }
            catch (Exception e)
            {
                SetStatus("couldn't set the polling rate");
                MessageBox.Show(this, e.Message, "Polling Rate", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (busy) { e.Cancel = true; return; }
            if (!ConfirmDiscard()) e.Cancel = true;
            else macros.StopRecording();
            base.OnFormClosing(e);
        }
    }

    // %APPDATA%\n52te\editor.ini: where Belkin's skin is, window size.
    internal sealed class Settings
    {
        private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "n52te", "editor.ini");
        public string BelkinSkin;
        public float Scale;

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    if (k == "BelkinSkin") s.BelkinSkin = v;
                    else if (k == "Scale") float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out s.Scale);
                }
            }
            catch (IOException) { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, "BelkinSkin=" + (BelkinSkin ?? "") + "\r\nScale=" + Scale.ToString(CultureInfo.InvariantCulture) + "\r\n");
            }
            catch (IOException) { }
        }
    }
}
