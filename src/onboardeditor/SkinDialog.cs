// Prompts in the style of Belkin's editor (Save_changes_*.jpg, Profile_Save_Warning_*.jpg,
// Save_macro_*.jpg): a borderless black box with cut-off lower corners, a blue title in capitals,
// the message, and flat buttons on the right ("SAVE CHANGES  Do you want to save your changes?
// Yes  No  Cancel"). Drawn by this program, so the text can be anything.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace N52te
{
    internal sealed class SkinDialog : Form
    {
        private static readonly Color Back = Color.FromArgb(10, 10, 12), Edge = Color.FromArgb(60, 62, 70), TitleColor = Color.FromArgb(40, 150, 245), TextColor = Color.FromArgb(225, 228, 232);
        private readonly string title, message;
        private readonly string[] buttons;
        private readonly Rectangle[] buttonRects;
        private readonly float scale;
        private readonly List<string> detailLines = new List<string>();
        private int detailTop;
        private int hover = -1, down = -1;
        private int logicalHeight;
        public int Choice = -1;

        private SkinDialog(string title, string message, string detailText, string[] buttons, float scale)
        {
            this.title = title.ToUpperInvariant();
            this.message = message;
            this.buttons = buttons;
            this.scale = scale;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            BackColor = Back;
            KeyPreview = true;

            const int width = 460;
            int messageHeight;
            using (var g = CreateGraphics())
            using (var f = MessageFont())
                messageHeight = (int)Math.Ceiling(g.MeasureString(message, f, (int)(380 * scale)).Height / scale);
            if (detailText != null)
            {
                const int maxLines = 18;
                string[] all = detailText.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
                for (int i = 0; i < all.Length && i < maxLines; i++) detailLines.Add(all[i]);
                if (all.Length > maxLines) detailLines.Add("... and " + (all.Length - maxLines) + " more");
            }
            int detailHeight = detailLines.Count * 15;
            detailTop = 58 + messageHeight + 6;
            logicalHeight = Math.Max(143, 58 + messageHeight + (detailHeight > 0 ? detailHeight + 10 : 0) + 58);
            ClientSize = new Size((int)(width * scale), (int)(logicalHeight * scale));
            Region = new Region(Shape(width * scale, logicalHeight * scale, 18 * scale));

            buttonRects = new Rectangle[buttons.Length];
            int x = width - 22;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                int w = Math.Max(50, TextWidth(buttons[i]) + 22);
                x -= w;
                buttonRects[i] = new Rectangle(x, logicalHeight - 48, w, 30);
                x -= 8;
            }
        }

        private static int CountLines(string s)
        {
            int n = 0;
            foreach (char c in s) if (c == '\n') n++;
            return n;
        }

        private Font MessageFont() { return new Font("Arial", 12f * scale, FontStyle.Regular, GraphicsUnit.Pixel); }

        private int TextWidth(string s)
        {
            using (var g = CreateGraphics())
            using (var f = new Font("Arial", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
                return (int)g.MeasureString(s, f).Width;
        }

        private static GraphicsPath Shape(float w, float h, float cut)
        {
            var p = new GraphicsPath();
            p.AddLines(new[] { new PointF(0, 0), new PointF(w, 0), new PointF(w, h - cut), new PointF(w - cut, h), new PointF(cut, h), new PointF(0, h - cut) });
            p.CloseFigure();
            return p;
        }

        // Returns the index of the button chosen, or -1 (closed with Esc).
        public static int Ask(IWin32Window owner, float scale, string title, string message, params string[] buttons)
        {
            return AskWithDetail(owner, scale, title, message, null, buttons);
        }

        public static int AskWithDetail(IWin32Window owner, float scale, string title, string message, string detail, params string[] buttons)
        {
            using (var d = new SkinDialog(title, message, detail, buttons, scale))
            {
                d.ShowDialog(owner);
                return d.Choice;
            }
        }

        // Development aid: the dialog rendered to a PNG.
        public static void SaveSample(string path, float scale, string title, string message, string detail, params string[] buttons)
        {
            using (var d = new SkinDialog(title, message, detail, buttons, scale))
            {
                d.CreateControl();
                using (var b = new Bitmap(d.ClientSize.Width, d.ClientSize.Height))
                {
                    d.DrawToBitmap(b, new Rectangle(Point.Empty, d.ClientSize));
                    b.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        public static void Tell(IWin32Window owner, float scale, string title, string message)
        {
            Ask(owner, scale, title, message, "Ok");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            using (var bg = new LinearGradientBrush(ClientRectangle, Color.FromArgb(28, 29, 33), Back, 90f)) g.FillRectangle(bg, ClientRectangle);
            g.ScaleTransform(scale, scale);
            using (GraphicsPath edge = Shape(ClientSize.Width / scale - 1, ClientSize.Height / scale - 1, 18))
            using (var pen = new Pen(Edge, 1.5f)) g.DrawPath(pen, edge);
            using (var tf = new Font("Arial", 12f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var tb = new SolidBrush(TitleColor)) g.DrawString(title, tf, tb, 14, 12);
            g.ResetTransform();
            using (var mf = MessageFont())
            using (var mb = new SolidBrush(TextColor))
                g.DrawString(message, mf, mb, new RectangleF(58 * scale, 52 * scale, 380 * scale, 400 * scale));
            g.ScaleTransform(scale, scale);
            using (var df = new Font("Arial", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var db = new SolidBrush(Color.FromArgb(150, 205, 255)))
            using (var clip = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                for (int i = 0; i < detailLines.Count; i++)
                    g.DrawString(detailLines[i], df, db, new RectangleF(58, detailTop + i * 15, 390, 15), clip);
            using (var bf = new Font("Arial", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    Rectangle r = buttonRects[i];
                    using (var fill = new SolidBrush(i == down ? Color.FromArgb(30, 60, 100) : i == hover ? Color.FromArgb(34, 38, 46) : Color.FromArgb(20, 21, 25)))
                    using (var pen = new Pen(i == hover || i == down ? Color.FromArgb(70, 160, 255) : Color.FromArgb(48, 50, 58)))
                    using (var tb = new SolidBrush(i == hover ? Color.FromArgb(150, 205, 255) : TextColor))
                    {
                        g.FillRectangle(fill, r);
                        g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                        g.DrawString(buttons[i], bf, tb, r, sf);
                    }
                }
            }
        }

        private int ButtonAt(Point p)
        {
            var l = new Point((int)(p.X / scale), (int)(p.Y / scale));
            for (int i = 0; i < buttonRects.Length; i++) if (buttonRects[i].Contains(l)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = ButtonAt(e.Location);
            if (h != hover) { hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        protected override void OnMouseDown(MouseEventArgs e)
        {
            down = ButtonAt(e.Location);
            if (down < 0 && e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            int b = ButtonAt(e.Location);
            if (b >= 0 && b == down) { Choice = b; Close(); }
            down = -1;
            Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Choice = -1; Close(); return true; }
            if (keyData == Keys.Enter) { Choice = 0; Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
