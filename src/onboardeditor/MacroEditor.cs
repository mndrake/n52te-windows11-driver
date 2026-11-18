// The macro editor: the panel that slides out of the main window's right edge in Belkin's n52te
// Editor v2.1.2 (skin_English\Macro_expand.ini, 266 x 456), with its collapsed "MACRO EDITOR" tab.
// It edits saved macros (Documents\n52te\Macros, Belkin's macro file format); the main window's
// right-click menu assigns them to inputs. Behaviour follows the decompiled editor
// (docs/reverse-engineering/07): the recorder hooks the keyboard system-wide, skips auto-repeat,
// inserts the time between events as delay steps unless "Ignore delays" is set, and stops at the
// keypad's 128 steps.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace N52te
{
    internal static class MacroLayout
    {
        public const int Width = 266, Height = 456, TabWidth = 22, TabHeight = 143;

        public static readonly Rectangle Collapse = new Rectangle(242, 100, 24, 44);
        public static readonly Rectangle Name = new Rectangle(24, 25, 206, 23);
        public static readonly Rectangle New = new Rectangle(22, 49, 39, 26);
        public static readonly Rectangle Open = new Rectangle(61, 49, 35, 26);
        public static readonly Rectangle Save = new Rectangle(96, 49, 41, 26);
        public static readonly Rectangle SaveAs = new Rectangle(137, 49, 51, 26);
        public static readonly Rectangle Delete = new Rectangle(188, 49, 45, 26);
        public static readonly Rectangle List = new Rectangle(12, 93, 135, 243);
        public static readonly Rectangle Record = new Rectangle(150, 93, 89, 23);
        public static readonly Rectangle Stop = new Rectangle(150, 117, 89, 23);
        public static readonly Rectangle Advanced = new Rectangle(150, 142, 89, 23);
        public static readonly Rectangle Up = new Rectangle(150, 166, 89, 23);
        public static readonly Rectangle Down = new Rectangle(150, 190, 89, 23);
        public static readonly Rectangle DeleteSteps = new Rectangle(150, 214, 89, 23);
        public static readonly Rectangle SelectAll = new Rectangle(150, 238, 89, 23);
        public static readonly Rectangle SelectDelays = new Rectangle(150, 262, 89, 23);
        public static readonly Rectangle InsertDelay = new Rectangle(150, 286, 89, 23);
        public static readonly Rectangle DelayTime = new Rectangle(150, 310, 89, 23);
        public static readonly Rectangle Ignore = new Rectangle(11, 340, 12, 13);
        public static readonly Rectangle[] Modes = { new Rectangle(11, 376, 62, 18), new Rectangle(11, 398, 139, 15), new Rectangle(11, 414, 170, 14) };
        public static readonly Rectangle Times = new Rectangle(75, 377, 53, 18);
        public static readonly Rectangle Interval = new Rectangle(105, 428, 54, 18);
        public static readonly Rectangle MouseLeft = new Rectangle(164, 360, 22, 38);
        public static readonly Rectangle MouseMiddle = new Rectangle(186, 360, 15, 38);
        public static readonly Rectangle MouseRight = new Rectangle(200, 360, 20, 38);
        public const int RowHeight = 15;
    }

    // Belkin's macro editor images (from the user's copy), or built-in artwork in the same layout.
    internal sealed class MacroSkin : IDisposable
    {
        public Bitmap Normal, Over, Down, Tab;

        public static MacroSkin Load(string belkinFolder)
        {
            if (!string.IsNullOrEmpty(belkinFolder) && File.Exists(Path.Combine(belkinFolder, "Macro_Editor_normal.JPG")))
            {
                try
                {
                    return new MacroSkin
                    {
                        Normal = Copy(belkinFolder, "Macro_Editor_normal.JPG", MacroLayout.Width, MacroLayout.Height),
                        Over = Copy(belkinFolder, "Macro_Editor_over.JPG", MacroLayout.Width, MacroLayout.Height),
                        Down = Copy(belkinFolder, "Macro_Editor_down.JPG", MacroLayout.Width, MacroLayout.Height),
                        Tab = Copy(belkinFolder, "macro_close.JPG", MacroLayout.TabWidth, MacroLayout.TabHeight),
                    };
                }
                catch (Exception) { }
            }
            return new MacroSkin { Normal = Render(0), Over = Render(1), Down = Render(2), Tab = RenderTab() };
        }

        private static Bitmap Copy(string folder, string name, int w, int h)
        {
            using (Image img = Image.FromFile(Path.Combine(folder, name)))
            {
                var b = new Bitmap(w, h);
                using (Graphics g = Graphics.FromImage(b)) g.DrawImage(img, 0, 0, w, h);
                return b;
            }
        }

        public void Dispose()
        {
            foreach (Bitmap b in new[] { Normal, Over, Down, Tab }) if (b != null) b.Dispose();
        }

        private static readonly Color Back = Color.FromArgb(14, 15, 18), Text = Color.FromArgb(225, 228, 232), Blue = Color.FromArgb(70, 160, 255);

        private static Bitmap RenderTab()
        {
            var b = new Bitmap(MacroLayout.TabWidth, MacroLayout.TabHeight);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(30, 32, 38));
                using (var pen = new Pen(Color.FromArgb(80, 84, 92))) g.DrawRectangle(pen, 0, 0, b.Width - 1, b.Height - 1);
                VerticalText(g, "MACRO EDITOR", new Rectangle(0, 8, b.Width, 110), Blue);
                using (var br = new SolidBrush(Text)) g.FillPolygon(br, new[] { new Point(8, 122), new Point(14, 128), new Point(8, 134) });
            }
            return b;
        }

        private static void VerticalText(Graphics g, string text, Rectangle r, Color c)
        {
            using (var f = new Font("Arial", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var br = new SolidBrush(c))
            {
                GraphicsState s = g.Save();
                g.TranslateTransform(r.X + r.Width / 2f + 6, r.Y);
                g.RotateTransform(90);
                g.DrawString(text, f, br, 0, 0);
                g.Restore(s);
            }
        }

        // state: 0 normal, 1 hover, 2 pressed/selected
        private static Bitmap Render(int state)
        {
            var b = new Bitmap(MacroLayout.Width, MacroLayout.Height);
            using (Graphics g = Graphics.FromImage(b))
            using (var small = new Font("Arial", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var bold = new Font("Arial", 11.5f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var text = new SolidBrush(Text))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.Clear(Back);
                Color frame = state == 0 ? Color.FromArgb(80, 84, 92) : Blue;
                // The tab on the right edge.
                using (var tab = new SolidBrush(Color.FromArgb(30, 32, 38))) g.FillRectangle(tab, 242, 0, 24, 150);
                using (var pen = new Pen(Color.FromArgb(80, 84, 92))) g.DrawRectangle(pen, 242, 0, 23, 150);
                VerticalText(g, "MACRO EDITOR", new Rectangle(242, 8, 24, 110), Blue);
                using (var br = new SolidBrush(Text)) g.FillPolygon(br, new[] { new Point(258, 116), new Point(252, 122), new Point(258, 128) });

                g.DrawString("Macro Name:", bold, text, 22, 8);
                Frame(g, MacroLayout.Name, Color.FromArgb(80, 84, 92), Color.FromArgb(24, 26, 30));
                g.FillPolygon(text, new[] { new Point(211, 34), new Point(219, 34), new Point(215, 39) });
                Button(g, MacroLayout.New, "New", small, frame, state);
                Button(g, MacroLayout.Open, "Open", small, frame, state);
                Button(g, MacroLayout.Save, "Save", small, frame, state);
                Button(g, MacroLayout.SaveAs, "Save As", small, frame, state);
                Button(g, MacroLayout.Delete, "Delete", small, frame, state);
                g.DrawString("Current Macro Assignment", bold, text, 12, 78);
                Frame(g, MacroLayout.List, Color.FromArgb(70, 74, 82), Color.Black);
                string[] labels = { "Record", "Stop", "Advanced", "Move Up", "Move Down", "Delete", "Select All", "Select All Delays", "Insert Delay" };
                Rectangle[] rects = { MacroLayout.Record, MacroLayout.Stop, MacroLayout.Advanced, MacroLayout.Up, MacroLayout.Down, MacroLayout.DeleteSteps, MacroLayout.SelectAll, MacroLayout.SelectDelays, MacroLayout.InsertDelay };
                for (int i = 0; i < labels.Length; i++) Button(g, rects[i], labels[i], small, frame, state);
                Frame(g, MacroLayout.DelayTime, frame, Color.FromArgb(24, 26, 30));
                Spin(g, new Rectangle(212, 312, 14, 18), text);

                Check(g, MacroLayout.Ignore, frame, state == 2);
                g.DrawString("Ignore delays between", small, text, 25, 339);
                g.DrawString("events", small, text, 13, 352);
                g.DrawString("Macro Behavior", bold, text, 11, 362);
                using (var mode = new SolidBrush(state == 2 ? Blue : Text))
                {
                    g.DrawString("Run Times:", small, mode, 12, 378);
                    g.DrawString("Repeat while pressed", small, mode, 12, 398);
                    g.DrawString("Repeat until next key is pressed", small, mode, 12, 414);
                    g.DrawString("Repetition interval:", small, mode, 12, 430);
                }
                Frame(g, MacroLayout.Times, frame, Color.FromArgb(24, 26, 30));
                Spin(g, new Rectangle(110, 377, 16, 18), text);
                Frame(g, MacroLayout.Interval, frame, Color.FromArgb(24, 26, 30));
                Spin(g, new Rectangle(141, 428, 16, 18), text);

                g.DrawString("Mouse Button", small, text, 160, 343);
                // A mouse seen from above: left, middle (wheel) and right areas.
                using (var body = new SolidBrush(Color.FromArgb(28, 30, 36)))
                using (var pen = new Pen(state == 0 ? Color.FromArgb(150, 154, 160) : Blue, 1.4f))
                using (var p = new GraphicsPath())
                {
                    p.AddArc(166, 358, 52, 30, 180, 180);
                    p.AddLine(218, 373, 218, 402);
                    p.AddArc(166, 386, 52, 30, 0, 180);
                    p.CloseFigure();
                    g.FillPath(body, p);
                    g.DrawPath(pen, p);
                    g.DrawLine(pen, 192, 360, 192, 380);
                    g.DrawLine(pen, 166, 380, 218, 380);
                    using (var wheel = new SolidBrush(state == 0 ? Color.FromArgb(120, 124, 132) : Blue)) g.FillRectangle(wheel, 189, 364, 6, 11);
                }
            }
            return b;
        }

        private static void Frame(Graphics g, Rectangle r, Color c, Color fill)
        {
            using (var br = new SolidBrush(fill)) g.FillRectangle(br, r);
            using (var pen = new Pen(c)) g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
        }

        private static void Button(Graphics g, Rectangle r, string s, Font f, Color frame, int state)
        {
            var inner = Rectangle.Inflate(r, -1, -1);
            Frame(g, inner, frame, state == 2 ? Color.FromArgb(30, 60, 100) : Color.FromArgb(24, 26, 30));
            using (var br = new SolidBrush(state == 0 ? Color.FromArgb(225, 228, 232) : Color.FromArgb(150, 205, 255)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(s, f, br, r, sf);
        }

        private static void Check(Graphics g, Rectangle r, Color c, bool x)
        {
            using (var pen = new Pen(c)) g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
            if (!x) return;
            using (var pen = new Pen(Blue, 1.6f))
            {
                g.DrawLine(pen, r.X + 2, r.Y + 2, r.Right - 3, r.Bottom - 3);
                g.DrawLine(pen, r.Right - 3, r.Y + 2, r.X + 2, r.Bottom - 3);
            }
        }

        private static void Spin(Graphics g, Rectangle r, Brush b)
        {
            int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            g.FillPolygon(b, new[] { new Point(cx - 4, cy - 2), new Point(cx + 4, cy - 2), new Point(cx, cy - 7) });
            g.FillPolygon(b, new[] { new Point(cx - 4, cy + 2), new Point(cx + 4, cy + 2), new Point(cx, cy + 7) });
        }
    }

    internal sealed class MacroEditor : Form
    {
        private static readonly int[] DelayChoices = { 1, 2, 3, 4, 5, 10, 20 }; // x 50 ms, Belkin's DelayTimes popup

        private readonly MainForm main;
        private readonly MacroTab tab;
        private MacroSkin skin;
        private float scale = 1f;
        private bool expanded;

        // The macro being edited, in the profile form: (code, 1 press / 0 release) and (FF, n x 50 ms).
        private readonly List<byte[]> steps = new List<byte[]>();
        private readonly List<bool> selected = new List<bool>();
        private string name = "";
        private string path;
        private bool modified;
        private byte mode, times, interval;
        private bool ignoreDelays;
        private int delayChoice;
        private int scroll;
        private int lastClicked = -1;
        private string note = "";

        private Rectangle hover = Rectangle.Empty, pressed = Rectangle.Empty;

        public MacroEditor(MainForm owner)
        {
            main = owner;
            Owner = owner;
            Text = "n52te Macro Editor";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            BackColor = Color.Black;
            tab = new MacroTab(this);
            owner.LocationChanged += (s, e) => Attach();

        }

        public bool Expanded { get { return expanded; } }
        public string MacroName { get { return name.Length > 0 ? name : "(unnamed)"; } }
        public bool HasSteps { get { return steps.Count > 0; } }

        public void SetLook(string belkinFolder, float newScale)
        {
            MacroSkin old = skin;
            skin = MacroSkin.Load(belkinFolder);
            if (old != null) old.Dispose();
            scale = newScale;
            ClientSize = new Size((int)Math.Round(MacroLayout.Width * scale), (int)Math.Round(MacroLayout.Height * scale));
            tab.SetLook(skin, scale);
            Attach();
            Invalidate();
        }

        private void Attach()
        {
            Location = new Point(main.Right - 2, main.Top + (int)(30 * scale));
            tab.Location = new Point(main.Right - 2, main.Top + (int)(100 * scale));
        }

        public void ShowState()
        {
            Attach();
            if (expanded) { tab.Hide(); if (!Visible) Show(main); } else { Hide(); if (!tab.Visible) tab.Show(main); }
        }

        public void Toggle()
        {
            expanded = !expanded;
            ShowState();
        }

        // ---- the macro ----

        public SavedMacro Current()
        {
            var m = new SavedMacro { Name = name, IgnoreDelay = ignoreDelays, Options = new[] { mode, times, interval } };
            for (int i = 0; i < steps.Count && 2 * i + 1 < m.Sequence.Length; i++)
            {
                m.Sequence[2 * i] = steps[i][0];
                m.Sequence[2 * i + 1] = steps[i][1];
            }
            return m;
        }

        public void LoadMacro(SavedMacro m, string filePath)
        {
            StopRecording();
            steps.Clear();
            selected.Clear();
            for (int i = 0; i + 1 < m.Sequence.Length; i += 2)
            {
                if (m.Sequence[i] == 0) break;
                steps.Add(new[] { m.Sequence[i], m.Sequence[i + 1] });
                selected.Add(false);
            }
            name = m.Name;
            path = filePath;
            mode = m.Options[0] <= 2 ? m.Options[0] : (byte)0;
            times = m.Options[1] <= 9 ? m.Options[1] : (byte)0;
            interval = m.Options[2] <= 4 ? m.Options[2] : (byte)0;
            ignoreDelays = m.IgnoreDelay;
            modified = false;
            scroll = 0;
            note = "";
            Invalidate();
        }

        private void Edited()
        {
            modified = true;
            Invalidate();
        }

        private void SetNote(string s)
        {
            note = s;
            Invalidate();
        }

        // ---- painting ----

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.ScaleTransform(scale, scale);
            g.DrawImage(skin.Normal, 0, 0, MacroLayout.Width, MacroLayout.Height);
            Cut(g, skin.Down, MacroLayout.Modes[mode]);
            if (ignoreDelays) Cut(g, skin.Down, MacroLayout.Ignore);
            if (recording) Cut(g, skin.Down, MacroLayout.Record);
            if (!hover.IsEmpty) Cut(g, pressed == hover ? skin.Down : skin.Over, hover);

            using (var font = new Font("Arial", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var white = new SolidBrush(Color.FromArgb(230, 232, 236)))
            using (var blue = new SolidBrush(Color.FromArgb(80, 165, 245)))
            using (var grey = new SolidBrush(Color.FromArgb(120, 124, 130)))
            using (var field = new SolidBrush(Color.FromArgb(24, 26, 30)))
            using (var black = new SolidBrush(Color.Black))
            using (var selBrush = new SolidBrush(Color.FromArgb(30, 70, 130)))
            using (var left = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                // Name box.
                var nameBox = Rectangle.FromLTRB(MacroLayout.Name.X + 3, MacroLayout.Name.Y + 3, MacroLayout.Name.Right - 22, MacroLayout.Name.Bottom - 3);
                g.FillRectangle(field, nameBox);
                g.DrawString((name.Length > 0 ? name : "(new macro)") + (modified ? " *" : ""), font, name.Length > 0 ? white : grey, nameBox, left);

                // Steps.
                var list = Rectangle.Inflate(MacroLayout.List, -2, -2);
                g.FillRectangle(black, list);
                int rows = list.Height / MacroLayout.RowHeight;
                if (steps.Count == 0)
                    g.DrawString(recording ? "Recording: press keys..." : "No steps. Record, or use Advanced and Mouse Button.", font, grey, new RectangleF(list.X + 4, list.Y + 4, list.Width - 8, 60));
                for (int r = 0; r < rows && scroll + r < steps.Count; r++)
                {
                    int i = scroll + r;
                    var row = new Rectangle(list.X, list.Y + r * MacroLayout.RowHeight, list.Width, MacroLayout.RowHeight);
                    if (selected[i]) g.FillRectangle(selBrush, row);
                    g.DrawString(StepText(steps[i]), font, steps[i][0] == FirmwareCodes.DelayStep ? blue : white, new Rectangle(row.X + 4, row.Y, row.Width - 6, row.Height), left);
                }
                if (steps.Count > rows)
                {
                    float h = list.Height * (float)rows / steps.Count, y = list.Y + list.Height * (float)scroll / steps.Count;
                    g.FillRectangle(grey, list.Right - 3, y, 3, h);
                }

                // Delay to insert, run count, interval.
                var delayBox = new Rectangle(MacroLayout.DelayTime.X + 3, MacroLayout.DelayTime.Y + 3, 58, MacroLayout.DelayTime.Height - 6);
                g.FillRectangle(field, delayBox);
                g.DrawString(DelayChoices[delayChoice] * 50 + " ms", font, white, delayBox, center);
                var timesBox = new Rectangle(MacroLayout.Times.X + 2, MacroLayout.Times.Y + 2, 32, MacroLayout.Times.Height - 4);
                g.FillRectangle(field, timesBox);
                g.DrawString((times + 1).ToString(CultureInfo.InvariantCulture), font, mode == 0 ? white : grey, timesBox, center);
                var intervalBox = new Rectangle(MacroLayout.Interval.X + 2, MacroLayout.Interval.Y + 2, 34, MacroLayout.Interval.Height - 4);
                g.FillRectangle(field, intervalBox);
                using (var narrow = new Font("Arial", 10f, FontStyle.Regular, GraphicsUnit.Pixel))
                    g.DrawString((interval + 1) * 50 + "ms", narrow, white, intervalBox, center);

                // Step count and notes, in the list's bottom corner.
                string count = (note.Length > 0 ? note + "  " : "") + steps.Count + "/128";
                using (var tiny = new Font("Arial", 9.5f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Far })
                {
                    var corner = new RectangleF(list.X, list.Bottom - 14, list.Width - 6, 13);
                    SizeF size = g.MeasureString(count, tiny);
                    g.FillRectangle(black, corner.Right - size.Width - 2, corner.Y, size.Width + 4, corner.Height);
                    g.DrawString(count, tiny, grey, corner, right);
                }
            }
        }

        private static string StepText(byte[] s)
        {
            if (s[0] == FirmwareCodes.DelayStep) return "Delay " + s[1] * Macro.DelayUnitMs + " ms";
            string what = FirmwareCodes.Name(s[0]);
            if (s[0] == 0xD0) what = "Left Button";
            else if (s[0] == 0xD1) what = "Right Button";
            else if (s[0] == 0xD2) what = "Middle Button";
            return what + (s[1] != 0 ? "  Press" : "  Release");
        }

        private void Cut(Graphics g, Bitmap from, Rectangle r)
        {
            r.Inflate(1, 1);
            g.DrawImage(from, r, r, GraphicsUnit.Pixel);
        }

        // ---- mouse ----

        private Point Logical(Point p) { return new Point((int)(p.X / scale), (int)(p.Y / scale)); }

        private IEnumerable<Rectangle> Buttons()
        {
            return new[]
            {
                MacroLayout.Collapse, MacroLayout.Name, MacroLayout.New, MacroLayout.Open, MacroLayout.Save, MacroLayout.SaveAs, MacroLayout.Delete,
                MacroLayout.Record, MacroLayout.Stop, MacroLayout.Advanced, MacroLayout.Up, MacroLayout.Down, MacroLayout.DeleteSteps,
                MacroLayout.SelectAll, MacroLayout.SelectDelays, MacroLayout.InsertDelay, MacroLayout.DelayTime, MacroLayout.Ignore,
                MacroLayout.Modes[0], MacroLayout.Modes[1], MacroLayout.Modes[2], MacroLayout.Times, MacroLayout.Interval,
                MacroLayout.MouseLeft, MacroLayout.MouseMiddle, MacroLayout.MouseRight,
            };
        }

        private Rectangle ButtonAt(Point p)
        {
            foreach (Rectangle r in Buttons()) if (r.Contains(p)) return r;
            return Rectangle.Empty;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Rectangle b = ButtonAt(Logical(e.Location));
            if (b != hover) { hover = b; Cursor = b.IsEmpty ? Cursors.Default : Cursors.Hand; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = Rectangle.Empty;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Point p = Logical(e.Location);
            var list = Rectangle.Inflate(MacroLayout.List, -2, -2);
            if (list.Contains(p)) { ClickList(p, list); return; }
            Rectangle b = ButtonAt(p);
            if (!b.IsEmpty) { pressed = b; Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            Point p = Logical(e.Location);
            Rectangle b = ButtonAt(p), was = pressed;
            pressed = Rectangle.Empty;
            Invalidate();
            if (!was.IsEmpty && b == was) ClickButton(b, p);
            base.OnMouseUp(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Point p = Logical(e.Location);
            int d = e.Delta > 0 ? 1 : -1;
            if (MacroLayout.Times.Contains(p)) { if (mode == 0) times = (byte)Math.Max(0, Math.Min(9, times + d)); Edited(); }
            else if (MacroLayout.Interval.Contains(p)) { interval = (byte)Math.Max(0, Math.Min(4, interval + d)); Edited(); }
            else if (MacroLayout.DelayTime.Contains(p)) { delayChoice = Math.Max(0, Math.Min(DelayChoices.Length - 1, delayChoice + d)); Invalidate(); }
            else
            {
                int rows = (MacroLayout.List.Height - 4) / MacroLayout.RowHeight;
                scroll = Math.Max(0, Math.Min(Math.Max(0, steps.Count - rows), scroll - d * 3));
                Invalidate();
            }
            base.OnMouseWheel(e);
        }

        private void ClickList(Point p, Rectangle list)
        {
            int i = scroll + (p.Y - list.Y) / MacroLayout.RowHeight;
            if (i >= steps.Count) { for (int k = 0; k < selected.Count; k++) selected[k] = false; Invalidate(); return; }
            if ((ModifierKeys & Keys.Shift) != 0 && lastClicked >= 0)
                for (int k = Math.Min(lastClicked, i); k <= Math.Max(lastClicked, i); k++) selected[k] = true;
            else if ((ModifierKeys & Keys.Control) != 0) selected[i] = !selected[i];
            else
            {
                for (int k = 0; k < selected.Count; k++) selected[k] = false;
                selected[i] = true;
            }
            lastClicked = i;
            Invalidate();
        }

        private bool Upper(Point p, Rectangle r) { return p.Y < r.Y + r.Height / 2; }

        private void ClickButton(Rectangle b, Point p)
        {
            if (b == MacroLayout.Collapse) { Toggle(); return; }
            if (b == MacroLayout.Name) { ShowMacroList(); return; }
            if (b == MacroLayout.New) { NewMacro(); return; }
            if (b == MacroLayout.Open) { ShowMacroList(); return; }
            if (b == MacroLayout.Save) { SaveMacro(false); return; }
            if (b == MacroLayout.SaveAs) { SaveMacro(true); return; }
            if (b == MacroLayout.Delete) { DeleteMacro(); return; }
            if (b == MacroLayout.Record) { StartRecording(); return; }
            if (b == MacroLayout.Stop) { StopRecording(); return; }
            if (b == MacroLayout.Advanced) { ShowAdvanced(); return; }
            if (b == MacroLayout.Up) { MoveSelected(-1); return; }
            if (b == MacroLayout.Down) { MoveSelected(1); return; }
            if (b == MacroLayout.DeleteSteps) { DeleteSelected(); return; }
            if (b == MacroLayout.SelectAll) { for (int i = 0; i < selected.Count; i++) selected[i] = true; Invalidate(); return; }
            if (b == MacroLayout.SelectDelays) { for (int i = 0; i < selected.Count; i++) selected[i] = steps[i][0] == FirmwareCodes.DelayStep; Invalidate(); return; }
            if (b == MacroLayout.InsertDelay) { InsertDelay(); return; }
            if (b == MacroLayout.DelayTime)
            {
                int d = p.X >= 210 ? (Upper(p, b) ? 1 : -1) : 1;
                delayChoice = (delayChoice + d + DelayChoices.Length) % DelayChoices.Length;
                Invalidate();
                return;
            }
            if (b == MacroLayout.Ignore) { ignoreDelays = !ignoreDelays; Edited(); return; }
            for (int m = 0; m < 3; m++) if (b == MacroLayout.Modes[m]) { mode = (byte)m; Edited(); return; }
            if (b == MacroLayout.Times)
            {
                if (mode != 0) return;
                times = (byte)((times + (p.X >= 108 && !Upper(p, b) ? 9 : 1)) % 10);
                Edited();
                return;
            }
            if (b == MacroLayout.Interval) { interval = (byte)((interval + (p.X >= 139 && !Upper(p, b) ? 4 : 1)) % 5); Edited(); return; }
            if (b == MacroLayout.MouseLeft) { AddClick(0xD0); return; }
            if (b == MacroLayout.MouseMiddle) { AddClick(0xD2); return; }
            if (b == MacroLayout.MouseRight) { AddClick(0xD1); return; }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!recording && keyData == Keys.Delete) { DeleteSelected(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---- editing ----

        private bool Room(int pairs)
        {
            if (steps.Count + pairs <= Macro.MaxPairs) return true;
            SetNote("full");
            return false;
        }

        private void Insert(int at, byte code, byte value)
        {
            steps.Insert(at, new[] { code, value });
            selected.Insert(at, false);
        }

        private int InsertPoint()
        {
            int last = selected.LastIndexOf(true);
            return last >= 0 ? last + 1 : steps.Count;
        }

        private void AddPair(byte code)
        {
            if (!Room(2)) return;
            int at = InsertPoint();
            Insert(at, code, 1);
            Insert(at + 1, code, 0);
            Reveal(at + 1);
            Edited();
        }

        private void AddClick(byte code)
        {
            if (recording)
            {
                RecordDelay();
                RecordEvent(code, false);
                RecordEvent(code, true);
                return;
            }
            AddPair(code);
        }

        // Like Belkin's Insert Delay: added to a neighbouring delay, or inserted after the selection.
        private void InsertDelay()
        {
            int units = DelayChoices[delayChoice];
            int sel = selected.IndexOf(true);
            int at = sel >= 0 ? sel : steps.Count - 1;
            if (at >= 0 && at + 1 < steps.Count && steps[at + 1][0] == FirmwareCodes.DelayStep) at = at + 1;
            if (at >= 0 && at < steps.Count && steps[at][0] == FirmwareCodes.DelayStep)
                steps[at][1] = (byte)Math.Min(Macro.MaxDelayUnits, steps[at][1] + units);
            else
            {
                if (steps.Count == 0) { SetNote("a macro can't start with a delay"); return; }
                if (!Room(1)) return;
                Insert(at + 1, FirmwareCodes.DelayStep, (byte)units);
                Reveal(at + 1);
            }
            Edited();
        }

        private void MoveSelected(int d)
        {
            int first = selected.IndexOf(true), last = selected.LastIndexOf(true);
            if (first < 0 || first + d < 0 || last + d >= steps.Count) return;
            if (d < 0)
            {
                byte[] s = steps[first - 1];
                steps.RemoveAt(first - 1);
                steps.Insert(last, s);
                selected.RemoveAt(first - 1);
                selected.Insert(last, false);
            }
            else
            {
                byte[] s = steps[last + 1];
                steps.RemoveAt(last + 1);
                steps.Insert(first, s);
                selected.RemoveAt(last + 1);
                selected.Insert(first, false);
            }
            Edited();
        }

        private void DeleteSelected()
        {
            for (int i = steps.Count - 1; i >= 0; i--)
                if (selected[i]) { steps.RemoveAt(i); selected.RemoveAt(i); }
            while (steps.Count > 0 && steps[0][0] == FirmwareCodes.DelayStep) { steps.RemoveAt(0); selected.RemoveAt(0); }
            scroll = Math.Min(scroll, Math.Max(0, steps.Count - 1));
            Edited();
        }

        private void Reveal(int i)
        {
            int rows = (MacroLayout.List.Height - 4) / MacroLayout.RowHeight;
            if (i < scroll) scroll = i;
            else if (i >= scroll + rows) scroll = i - rows + 1;
        }

        // Advanced: insert keys and functions that can't be recorded (Belkin's "Advanced" dialog).
        private void ShowAdvanced()
        {
            var m = new ContextMenuStrip();
            m.Items.Add("Single Key...", null, (s, e) =>
            {
                using (var d = new SingleKeyDialog("macro step"))
                    if (d.ShowDialog(this) == DialogResult.OK) AddPair(d.Code);
            });
            m.Items.Add(Group("Mouse", Assignment.Mouse));
            m.Items.Add(Group("Media Functions", Assignment.Media));
            m.Items.Add(Group("More Media and Browser Keys", Assignment.MoreMedia));
            m.Show(this, new Point((int)(MacroLayout.Advanced.X * scale), (int)(MacroLayout.Advanced.Bottom * scale)));
        }

        private ToolStripMenuItem Group(string text, Assignment.Item[] items)
        {
            var g = new ToolStripMenuItem(text);
            foreach (Assignment.Item it in items)
            {
                if (!it.Works) continue;
                byte code = it.Code;
                g.DropDownItems.Add(it.Text, null, (s, e) => AddPair(code));
            }
            return g;
        }

        // ---- saved macros ----

        private bool ConfirmDiscard()
        {
            if (!modified || steps.Count == 0) return true;
            DialogResult r = MessageBox.Show(this, "Save the changes to the macro '" + MacroName + "' first?", "Macro Editor", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            return r == DialogResult.No || (r == DialogResult.Yes && SaveMacro(false));
        }

        private void NewMacro()
        {
            if (!ConfirmDiscard()) return;
            LoadMacro(new SavedMacro(), null);
        }

        private void ShowMacroList()
        {
            var m = new ContextMenuStrip();
            foreach (string n in SavedMacro.List())
            {
                string nm = n;
                m.Items.Add(new ToolStripMenuItem(nm, null, (s, e) => OpenMacro(SavedMacro.PathFor(nm))) { Checked = string.Equals(nm, name, StringComparison.OrdinalIgnoreCase) && path != null });
            }
            if (m.Items.Count == 0) m.Items.Add(new ToolStripMenuItem("(no saved macros yet)") { Enabled = false });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Open a Belkin Macro File...", null, (s, e) =>
            {
                using (var d = new OpenFileDialog { Filter = "Macro files (*.ini)|*.ini" })
                    if (d.ShowDialog(this) == DialogResult.OK) OpenMacro(d.FileName);
            });
            m.Items.Add("Macros Folder", null, (s, e) => { Directory.CreateDirectory(SavedMacro.Folder); System.Diagnostics.Process.Start(SavedMacro.Folder); });
            m.Show(this, new Point((int)(MacroLayout.Name.X * scale), (int)(MacroLayout.Name.Bottom * scale)));
        }

        private void OpenMacro(string file)
        {
            if (!ConfirmDiscard()) return;
            try
            {
                SavedMacro m = SavedMacro.Read(file);
                LoadMacro(m, string.Equals(Path.GetDirectoryName(Path.GetFullPath(file)), Path.GetFullPath(SavedMacro.Folder), StringComparison.OrdinalIgnoreCase) ? file : null);
            }
            catch (Exception e)
            {
                MessageBox.Show(this, e.Message, "Open Macro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public bool SaveMacro(bool asNew)
        {
            StopRecording();
            if (steps.Count == 0) { SetNote("nothing to save"); return false; }
            SavedMacro m = Current();
            if (SavedMacro.PressesMoreThanReleases(m.Sequence) &&
                MessageBox.Show(this, "This macro presses more keys than it releases, so a key may stay held down after it plays. Save it anyway?", "Macro Editor", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                return false;
            if (asNew || path == null)
            {
                using (var d = new NameDialog(asNew ? "Save Macro As" : "Save Macro", "Macro name:", name.Length > 0 ? name : "New Macro"))
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return false;
                    string p = SavedMacro.PathFor(d.Value);
                    if (File.Exists(p) && !string.Equals(p, path, StringComparison.OrdinalIgnoreCase)
                        && MessageBox.Show(this, "Replace the saved macro '" + d.Value + "'?", "Save Macro", MessageBoxButtons.OKCancel) != DialogResult.OK) return false;
                    name = d.Value;
                    path = p;
                    m.Name = name;
                }
            }
            m.Write(path);
            modified = false;
            SetNote("saved");
            return true;
        }

        private void DeleteMacro()
        {
            if (path == null || !File.Exists(path)) { SetNote("not saved"); return; }
            if (MessageBox.Show(this, "Delete the saved macro '" + name + "'? Inputs it was assigned to keep their copy.", "Delete Macro", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            File.Delete(path);
            path = null;
            modified = true;
            SetNote("deleted");
        }

        // ---- recording (system-wide keyboard hook, like Belkin's KBHook.dll) ----

        private bool recording;
        private IntPtr hook = IntPtr.Zero;
        private HookProc hookProc;
        private int lastTick;
        private byte lastCode;
        private bool lastUp;
        private bool firstEvent;

        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KbdLlHookStruct { public uint VkCode, ScanCode, Flags, Time; public IntPtr ExtraInfo; }

        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc fn, IntPtr module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);

        private void StartRecording()
        {
            if (recording) return;
            hookProc = OnHook;
            hook = SetWindowsHookEx(13 /* WH_KEYBOARD_LL */, hookProc, GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero) { SetNote("couldn't start recording"); return; }
            recording = true;
            firstEvent = steps.Count == 0;
            lastTick = Environment.TickCount;
            lastCode = 0;
            SetNote("recording");
            main.SetStatusText("recording a macro: press keys, then Stop");
        }

        public void StopRecording()
        {
            if (!recording) return;
            UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
            recording = false;
            SetNote("");
            main.SetStatusText("");
        }

        private IntPtr OnHook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && recording)
            {
                var k = (KbdLlHookStruct)Marshal.PtrToStructure(lParam, typeof(KbdLlHookStruct));
                bool up = (k.Flags & 0x80) != 0;
                int vk = (int)k.VkCode;
                byte fw = vk == 0x0D && (k.Flags & 1) != 0 ? (byte)0x74 : FirmwareCodes.FromVirtualKey(vk);
                if (fw == 0) SetNote("key not on the keypad");
                else if (!(fw == lastCode && up == lastUp)) // skip auto-repeat
                {
                    RecordDelay();
                    RecordEvent(fw, up);
                }
            }
            return CallNextHookEx(hook, code, wParam, lParam);
        }

        private void RecordDelay()
        {
            int now = Environment.TickCount;
            int units = Math.Min(Macro.MaxDelayUnits, (now - lastTick) / Macro.DelayUnitMs);
            lastTick = now;
            if (ignoreDelays || firstEvent || units < 1) return;
            if (steps.Count + 1 >= Macro.MaxPairs) return;
            steps.Add(new[] { FirmwareCodes.DelayStep, (byte)units });
            selected.Add(false);
        }

        private void RecordEvent(byte fw, bool up)
        {
            if (steps.Count >= Macro.MaxPairs) { StopRecording(); SetNote("full: 128 steps"); return; }
            steps.Add(new[] { fw, up ? (byte)0 : (byte)1 });
            selected.Add(false);
            lastCode = fw;
            lastUp = up;
            firstEvent = false;
            Reveal(steps.Count - 1);
            Edited();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopRecording();
            base.OnFormClosing(e);
        }

        // Screenshot support: the panel as a bitmap at the current scale.
        public Bitmap Render()
        {
            var b = new Bitmap(ClientSize.Width, ClientSize.Height);
            using (Graphics g = Graphics.FromImage(b)) OnPaint(new PaintEventArgs(g, new Rectangle(Point.Empty, ClientSize)));
            return b;
        }

        public void Demo()
        {
            var m = new SavedMacro { Name = "Greeting" };
            byte[] s = Macro.Parse("+LShift H -LShift 100ms E L 50ms L O 500ms Mouse_Left");
            Buffer.BlockCopy(s, 0, m.Sequence, 0, s.Length);
            LoadMacro(m, null);
            selected[4] = true;
            mode = 0;
            times = 0;
        }
    }

    // The collapsed "MACRO EDITOR" tab on the main window's right edge.
    internal sealed class MacroTab : Form
    {
        private readonly MacroEditor editor;
        private MacroSkin skin;
        private float scale = 1f;

        public MacroTab(MacroEditor editor)
        {
            this.editor = editor;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            new ToolTip().SetToolTip(this, "Macro Editor");
        }

        public void SetLook(MacroSkin s, float newScale)
        {
            skin = s;
            scale = newScale;
            ClientSize = new Size((int)Math.Round(MacroLayout.TabWidth * scale), (int)Math.Round(MacroLayout.TabHeight * scale));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(skin.Tab, new Rectangle(Point.Empty, ClientSize));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            editor.Toggle();
            base.OnMouseUp(e);
        }
    }
}
