// The "Current Profile" drop-down: the saved profiles in Belkin's three columns (Current Profile,
// Linked Application, Auto-Switching), opened under the profile box. A dot marks the profile last
// written to the keypad.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace N52te
{
    internal sealed class ProfileList : Form
    {
        public sealed class Entry
        {
            public readonly string Text;
            public readonly bool OnKeypad, Current;
            public readonly Action Pick;
            public Entry(string text, bool onKeypad, bool current, Action pick) { Text = text; OnKeypad = onKeypad; Current = current; Pick = pick; }
        }

        private const int Row = 20, MaxRows = 14;
        private readonly List<Entry> entries;
        private readonly float scale;
        private int hover = -1, top;

        public ProfileList(List<Entry> entries, float scale)
        {
            this.entries = entries;
            this.scale = scale;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(12, 13, 16);
            KeyPreview = true;
        }

        public void Show(IWin32Window owner, Point at, int width)
        {
            int rows = Math.Min(MaxRows, Math.Max(1, entries.Count));
            Location = at;
            ClientSize = new Size(width, (int)(rows * Row * scale) + 2);
            Show(owner);
            Activate();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private int RowAt(Point p)
        {
            int i = top + (int)((p.Y - 1) / (Row * scale));
            return i >= 0 && i < entries.Count && entries[i] != null ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = RowAt(e.Location);
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int max = Math.Max(0, entries.Count - MaxRows);
            top = Math.Max(0, Math.Min(max, top - Math.Sign(e.Delta) * 3));
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            int i = RowAt(e.Location);
            if (i < 0) return;
            Action pick = entries[i].Pick;
            Close();
            pick();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(70, 150, 240))) g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            g.ScaleTransform(scale, scale);
            float w = ClientSize.Width / scale;
            using (var font = new Font("Arial", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var bold = new Font("Arial", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var white = new SolidBrush(Color.FromArgb(230, 232, 236)))
            using (var grey = new SolidBrush(Color.FromArgb(120, 124, 130)))
            using (var dot = new SolidBrush(Color.FromArgb(70, 200, 110)))
            using (var hot = new SolidBrush(Color.FromArgb(30, 60, 100)))
            using (var line = new Pen(Color.FromArgb(50, 52, 60)))
            using (var left = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                for (int r = 0; r < MaxRows && top + r < entries.Count; r++)
                {
                    Entry en = entries[top + r];
                    float y = 1 + r * Row;
                    if (en == null) { g.DrawLine(line, 6, y + Row / 2f, w - 6, y + Row / 2f); continue; }
                    if (top + r == hover) g.FillRectangle(hot, 1, y, w - 2, Row);
                    if (en.OnKeypad) g.FillEllipse(dot, 6, y + 7, 6, 6);
                    bool isProfile = en.Pick != null && en.Text.IndexOf("(what it holds now)", StringComparison.Ordinal) < 0 && en.Text != "Factory Profile";
                    g.DrawString(en.Text, en.Current ? bold : font, white, new RectangleF(16, y, isProfile ? 112 : w - 22, Row), left);
                    if (isProfile)
                    {
                        g.DrawString("-", font, grey, new RectangleF(131, y, 100, Row), left);
                        g.DrawString("Off", font, grey, new RectangleF(244, y, 60, Row), left);
                    }
                }
            }
            if (entries.Count > MaxRows)
                using (var bar = new SolidBrush(Color.FromArgb(90, 94, 100)))
                    g.FillRectangle(bar, ClientSize.Width / scale - 4, 1 + (float)top / entries.Count * MaxRows * Row, 3, (float)MaxRows / entries.Count * MaxRows * Row);
        }
    }
}
