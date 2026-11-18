// The main window's look. The layout (positions of every box and button in a 468 x 509 window)
// is the one of Belkin's n52te Editor v2.1.2 (skin_English\main.ini). The artwork is either:
//   * Belkin's own skin images, loaded at run time from the user's copy of Belkin's software
//     (never shipped with this program: they are Belkin/Razer copyright), or
//   * this program's own drawing of the same layout.
// Each look provides, per keymap, a "normal" image (connected and not connected), a "hover" image
// and a "pressed" image of the whole window; hover and press effects are cut out of those.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace N52te
{
    internal static class SkinLayout
    {
        public const int Width = 468, Height = 509;

        public static readonly Rectangle Minimize = new Rectangle(424, 7, 17, 15);
        public static readonly Rectangle Close = new Rectangle(440, 6, 18, 18);
        public static readonly Rectangle[] MapTabs = { new Rectangle(100, 85, 53, 20), new Rectangle(153, 85, 54, 20), new Rectangle(207, 85, 55, 20) };
        public static readonly Rectangle Profile = new Rectangle(103, 466, 356, 30);
        public static readonly Rectangle AutoSwitch = new Rectangle(118, 392, 14, 14);
        public static readonly Rectangle ShowOnScreen = new Rectangle(270, 392, 14, 14);
        public static readonly Rectangle New = new Rectangle(112, 420, 45, 32);
        public static readonly Rectangle Save = new Rectangle(158, 420, 43, 32);
        public static readonly Rectangle SaveAs = new Rectangle(202, 420, 60, 32);
        public static readonly Rectangle Import = new Rectangle(262, 420, 48, 32);
        public static readonly Rectangle Delete = new Rectangle(311, 420, 53, 32);
        public static readonly Rectangle Apply = new Rectangle(364, 420, 98, 32);
        public static readonly Rectangle Reset = new Rectangle(368, 366, 96, 29);
        public static readonly Rectangle[] Polling = { new Rectangle(112, 375, 42, 17), new Rectangle(160, 375, 42, 17), new Rectangle(208, 375, 45, 17) };
        public static readonly Rectangle Status = new Rectangle(103, 331, 162, 18);

        // The 27 label boxes, in input order (main.ini 200-226).
        public static readonly Rectangle[] Boxes =
        {
            new Rectangle(1, 118, 94, 25), new Rectangle(1, 143, 94, 25), new Rectangle(1, 167, 94, 25), new Rectangle(1, 191, 94, 25),
            new Rectangle(1, 215, 94, 25), new Rectangle(1, 239, 94, 24), new Rectangle(1, 264, 94, 24), new Rectangle(1, 288, 94, 24),
            new Rectangle(1, 312, 94, 24), new Rectangle(1, 336, 94, 24), new Rectangle(1, 360, 94, 24), new Rectangle(1, 384, 94, 24),
            new Rectangle(1, 408, 94, 24), new Rectangle(1, 432, 94, 24),
            new Rectangle(268, 123, 92, 32),                                                                  // round thumb button
            new Rectangle(267, 202, 90, 30), new Rectangle(267, 232, 90, 30), new Rectangle(267, 262, 90, 30), // wheel up/click/down
            new Rectangle(266, 338, 95, 32),                                                                  // thumb pad
            new Rectangle(367, 120, 91, 24), new Rectangle(367, 144, 91, 24), new Rectangle(367, 168, 91, 24), new Rectangle(367, 192, 91, 24),
            new Rectangle(367, 216, 91, 24), new Rectangle(367, 241, 91, 24), new Rectangle(367, 266, 91, 24), new Rectangle(367, 289, 91, 24),
        };

        // Where each input is on the keypad picture (main.ini 400-426).
        public static readonly Rectangle[] Picture =
        {
            new Rectangle(115, 164, 17, 15), new Rectangle(134, 159, 16, 14), new Rectangle(153, 156, 16, 14), new Rectangle(171, 156, 16, 14),
            new Rectangle(189, 154, 17, 16), new Rectangle(118, 184, 16, 14), new Rectangle(136, 178, 16, 14), new Rectangle(155, 176, 16, 14),
            new Rectangle(172, 175, 16, 14), new Rectangle(192, 174, 16, 14), new Rectangle(120, 202, 16, 14), new Rectangle(139, 196, 16, 14),
            new Rectangle(157, 194, 16, 14), new Rectangle(175, 193, 16, 14),
            new Rectangle(222, 212, 8, 8), new Rectangle(198, 196, 8, 9), new Rectangle(198, 205, 12, 4), new Rectangle(198, 209, 15, 9),
            new Rectangle(225, 257, 17, 14),
            new Rectangle(240, 224, 4, 4), new Rectangle(235, 235, 4, 4), new Rectangle(224, 232, 4, 4), new Rectangle(228, 223, 4, 4),
            new Rectangle(240, 230, 4, 4), new Rectangle(228, 238, 4, 4), new Rectangle(224, 227, 4, 4), new Rectangle(235, 220, 4, 4),
        };

        // The group icon above each set of boxes (main.ini 500-526): keys, round button, wheel, thumb pad, D-pad.
        public static readonly Rectangle KeysIcon = new Rectangle(30, 76, 40, 40);
        public static readonly Rectangle RoundIcon = new Rectangle(290, 78, 44, 44);
        public static readonly Rectangle WheelIcon = new Rectangle(290, 157, 44, 44);
        public static readonly Rectangle ThumbIcon = new Rectangle(290, 295, 44, 44);
        public static readonly Rectangle DpadIcon = new Rectangle(389, 76, 44, 44);

        public static Rectangle IconFor(int input)
        {
            if (input <= 14) return KeysIcon;
            if (input == 15) return RoundIcon;
            if (input <= 18) return WheelIcon;
            if (input == 19) return ThumbIcon;
            return DpadIcon;
        }
    }

    internal sealed class Skin : IDisposable
    {
        public readonly string Source; // folder of Belkin's skin, or null for the built-in artwork
        private readonly Bitmap[] connected = new Bitmap[3], disconnected = new Bitmap[3], over = new Bitmap[3], down = new Bitmap[3];
        public Color BoxFill { get; private set; }
        public Color Text { get; private set; }

        private Skin(string source) { Source = source; }

        // Where input n's label box is. Belkin's boxes sit inside framed panels that hide their
        // slightly different widths; the built-in artwork lines the right-hand column up instead.
        public Rectangle Box(int n)
        {
            return Source == null ? BuiltInBox(n) : SkinLayout.Boxes[n - 1];
        }

        private static Rectangle BuiltInBox(int n)
        {
            Rectangle r = SkinLayout.Boxes[n - 1];
            return n >= 15 && n <= 19 ? new Rectangle(266, r.Y, 95, r.Height) : r;
        }

        public Bitmap Normal(int km, bool isConnected) { return isConnected ? connected[km] : disconnected[km]; }
        public Bitmap Over(int km) { return over[km]; }
        public Bitmap Down(int km) { return down[km]; }

        public void Dispose()
        {
            foreach (Bitmap[] set in new[] { connected, disconnected, over, down })
                foreach (Bitmap b in set) if (b != null) b.Dispose();
        }

        // Belkin's skin folder: skin_English from the v2.1.2 package (needs main.ini and the main_*.jpg images).
        public static bool IsBelkinSkin(string folder)
        {
            return !string.IsNullOrEmpty(folder) && File.Exists(Path.Combine(folder, "main.ini")) && File.Exists(Path.Combine(folder, "main_normal1_2.jpg"));
        }

        public static Skin LoadBelkin(string folder)
        {
            var s = new Skin(folder);
            for (int k = 0; k < 3; k++)
            {
                int n = k + 1;
                s.connected[k] = Load(folder, "main_normal" + n + "_2.jpg");
                s.disconnected[k] = Load(folder, "main_normal" + n + "_1.jpg");
                s.over[k] = Load(folder, "main_over" + n + "_2.jpg");
                s.down[k] = Load(folder, "main_down" + n + "_2.jpg");
            }
            // An empty part of the first key box: used to clear the boxes before writing in them.
            s.BoxFill = s.connected[0].GetPixel(60, 130);
            s.Text = Color.FromArgb(70, 150, 240);
            return s;
        }

        private static Bitmap Load(string folder, string name)
        {
            using (var img = Image.FromFile(Path.Combine(folder, name)))
            {
                var b = new Bitmap(SkinLayout.Width, SkinLayout.Height);
                using (Graphics g = Graphics.FromImage(b)) g.DrawImage(img, 0, 0, SkinLayout.Width, SkinLayout.Height);
                return b;
            }
        }

        // This program's own artwork in Belkin's layout.
        public static Skin BuiltIn()
        {
            var s = new Skin(null) { BoxFill = Color.FromArgb(18, 20, 24), Text = Color.FromArgb(80, 160, 245) };
            for (int k = 0; k < 3; k++)
            {
                s.connected[k] = Render(k, State.Normal, true);
                s.disconnected[k] = Render(k, State.Normal, false);
                s.over[k] = Render(k, State.Over, true);
                s.down[k] = Render(k, State.Down, true);
            }
            return s;
        }

        private enum State { Normal, Over, Down }

        private static readonly Color[] keymapColors = { Color.FromArgb(40, 120, 230), Color.FromArgb(220, 50, 50), Color.FromArgb(40, 190, 80) };

        private static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static Bitmap Render(int km, State state, bool isConnected)
        {
            var b = new Bitmap(SkinLayout.Width, SkinLayout.Height);
            using (Graphics g = Graphics.FromImage(b))
            using (var small = new Font("Arial", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var label = new Font("Arial", 11.5f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var title = new Font("Arial", 30f, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Pixel))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                Color accent = keymapColors[km];
                Color frame = state == State.Normal ? Color.FromArgb(70, 74, 82) : Color.FromArgb(60, 150, 255);
                Color text = Color.FromArgb(220, 222, 226);
                Color dim = Color.FromArgb(130, 134, 140);

                using (var bg = new LinearGradientBrush(new Rectangle(0, 0, SkinLayout.Width, SkinLayout.Height), Color.FromArgb(14, 15, 18), Color.FromArgb(4, 4, 6), 90f))
                    g.FillRectangle(bg, 0, 0, SkinLayout.Width, SkinLayout.Height);
                using (var pen = new Pen(Color.FromArgb(60, 60, 66))) g.DrawRectangle(pen, 0, 0, SkinLayout.Width - 1, SkinLayout.Height - 1);

                // Title (no Belkin or Razer logos).
                using (var tb = new SolidBrush(Color.FromArgb(120, 180, 240))) g.DrawString("n52te", title, tb, 110, 26);
                using (var tb = new SolidBrush(dim)) g.DrawString("onboard keymap editor", small, tb, 205, 47);
                using (var pen = new Pen(text, 2f))
                {
                    g.DrawLine(pen, 427, 18, 437, 18);
                    g.DrawLine(pen, 444, 9, 454, 19);
                    g.DrawLine(pen, 454, 9, 444, 19);
                }

                // Keymap tabs.
                using (var tb = new SolidBrush(text)) g.DrawString("Keymap", small, tb, 160, 71);
                for (int t = 0; t < 3; t++)
                {
                    Rectangle r = SkinLayout.MapTabs[t];
                    bool active = t == km;
                    using (var fill = new SolidBrush(active ? Color.FromArgb(40, 44, 52) : Color.FromArgb(22, 24, 28)))
                    using (var pen = new Pen(active || state != State.Normal ? Color.FromArgb(90, 160, 230) : frame))
                    using (var tb = new SolidBrush(keymapColors[t]))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.FillRectangle(fill, r);
                        g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                        g.DrawString("Map " + (t + 1), small, tb, r, sf);
                    }
                }

                // Keypad picture area, tinted with the keymap colour.
                var pic = new Rectangle(100, 106, 162, 222);
                using (var tint = new LinearGradientBrush(pic, Color.FromArgb(isConnected ? 70 : 25, accent), Color.FromArgb(0, accent), 90f))
                    g.FillRectangle(tint, pic);
                DrawKeypad(g, state, isConnected);
                if (!isConnected)
                    using (var tb = new SolidBrush(text))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center })
                        g.DrawString("n52te not connected", label, tb, SkinLayout.Status, sf);

                // Group icons and box frames.
                foreach (Rectangle icon in new[] { SkinLayout.KeysIcon, SkinLayout.RoundIcon, SkinLayout.WheelIcon, SkinLayout.ThumbIcon, SkinLayout.DpadIcon })
                    DrawIcon(g, icon, icon, state);
                DrawFrame(g, new Rectangle(0, 115, 97, 344), frame);
                DrawFrame(g, new Rectangle(365, 117, 95, 198), frame);
                DrawFrame(g, new Rectangle(264, 199, 99, 96), frame); // the wheel's three boxes
                using (var fill = new SolidBrush(Color.FromArgb(18, 20, 24)))
                using (var pen = new Pen(frame))
                {
                    for (int n = 1; n <= KeypadLayout.InputCount; n++)
                    {
                        Rectangle r = BuiltInBox(n);
                        g.FillRectangle(fill, r);
                        g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                        if (n <= 14)
                        {
                            var badge = new Rectangle(r.X + 3, r.Y + 4, 15, r.Height - 8);
                            using (GraphicsPath p = Rounded(badge, 2))
                            using (var bb = new SolidBrush(Color.FromArgb(225, 228, 232)))
                                g.FillPath(bb, p);
                            using (var tb = new SolidBrush(Color.Black))
                            using (var f = new Font("Arial", 8.5f, FontStyle.Bold, GraphicsUnit.Pixel))
                            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                                g.DrawString(n.ToString("00"), f, tb, badge, sf);
                        }
                    }
                }

                // Settings and buttons.
                using (var tb = new SolidBrush(text)) g.DrawString("Polling Rate", label, tb, 116, 356);
                string[] rates = { "125hz", "500hz", "1000hz" };
                for (int i = 0; i < 3; i++) DrawButton(g, SkinLayout.Polling[i], rates[i], small, state, false);
                DrawCheck(g, SkinLayout.AutoSwitch, "Auto-Switching Override", "Prevents Profile Switching", small, text, dim, state);
                DrawCheck(g, SkinLayout.ShowOnScreen, "Show Changes On Screen", "Show profile name on profile change", small, text, dim, state);
                DrawButton(g, SkinLayout.New, "New", small, state, true);
                DrawButton(g, SkinLayout.Save, "Save", small, state, true);
                DrawButton(g, SkinLayout.SaveAs, "Save As", small, state, true);
                DrawButton(g, SkinLayout.Import, "Import", small, state, true);
                DrawButton(g, SkinLayout.Delete, "Delete", small, state, true);
                DrawButton(g, SkinLayout.Apply, "Apply Changes", small, state, true);
                DrawButton(g, SkinLayout.Reset, "Reset Keymap", small, state, true);

                using (var tb = new SolidBrush(text))
                {
                    g.DrawString("Current Profile", label, tb, 105, 453);
                    g.DrawString("Linked Application", label, tb, 234, 453);
                    g.DrawString("Auto-Switching", label, tb, 347, 453);
                }
                DrawFrame(g, SkinLayout.Profile, frame);
                using (var tb = new SolidBrush(text))
                    g.FillPolygon(tb, new[] { new Point(440, 478), new Point(450, 478), new Point(445, 484) });
            }
            return b;
        }

        private static void DrawFrame(Graphics g, Rectangle r, Color c)
        {
            using (GraphicsPath p = Rounded(r, 3))
            using (var pen = new Pen(c, 1.5f))
                g.DrawPath(pen, p);
        }

        private static void DrawButton(Graphics g, Rectangle r, string text, Font f, State state, bool framed)
        {
            var inner = Rectangle.Inflate(r, -2, -3);
            Color edge = state == State.Normal ? Color.FromArgb(80, 84, 92) : Color.FromArgb(70, 160, 255);
            if (framed || state != State.Normal)
            {
                using (GraphicsPath p = Rounded(inner, 3))
                using (var fill = new SolidBrush(state == State.Down ? Color.FromArgb(30, 60, 100) : Color.FromArgb(24, 26, 30)))
                using (var pen = new Pen(edge, state == State.Normal ? 1f : 1.6f))
                {
                    g.FillPath(fill, p);
                    g.DrawPath(pen, p);
                }
            }
            using (var tb = new SolidBrush(state == State.Normal ? Color.FromArgb(225, 228, 232) : Color.FromArgb(150, 205, 255)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(text, f, tb, r, sf);
        }

        private static void DrawCheck(Graphics g, Rectangle box, string text, string note, Font f, Color c, Color dim, State state)
        {
            using (var pen = new Pen(state == State.Normal ? Color.FromArgb(90, 94, 100) : Color.FromArgb(70, 160, 255)))
                g.DrawRectangle(pen, box.X + 1, box.Y + 1, box.Width - 3, box.Height - 3);
            if (state == State.Down)
                using (var pen = new Pen(Color.FromArgb(70, 160, 255), 1.6f))
                {
                    g.DrawLine(pen, box.X + 3, box.Y + 3, box.Right - 4, box.Bottom - 4);
                    g.DrawLine(pen, box.Right - 4, box.Y + 3, box.X + 3, box.Bottom - 4);
                }
            using (var tb = new SolidBrush(c)) g.DrawString(text, f, tb, box.Right + 1, box.Y);
            using (var tb = new SolidBrush(dim)) g.DrawString(note, f, tb, box.X - 1, box.Bottom + 1);
        }

        private static void DrawIcon(Graphics g, Rectangle r, Rectangle which, State state)
        {
            Color ring = state == State.Normal ? Color.FromArgb(150, 154, 160) : Color.FromArgb(235, 225, 60);
            using (var fill = new SolidBrush(Color.FromArgb(40, 42, 48)))
            using (var pen = new Pen(ring, 2f))
            {
                g.FillEllipse(fill, r);
                g.DrawEllipse(pen, Rectangle.Inflate(r, -2, -2));
                var c = Rectangle.Inflate(r, -r.Width / 4, -r.Height / 4);
                if (which == SkinLayout.KeysIcon) g.DrawRectangle(pen, c);
                else if (which == SkinLayout.WheelIcon) g.DrawEllipse(pen, new Rectangle(c.X + c.Width / 3, c.Y, c.Width / 3, c.Height));
                else if (which == SkinLayout.DpadIcon)
                {
                    g.DrawLine(pen, c.X, c.Y + c.Height / 2, c.Right, c.Y + c.Height / 2);
                    g.DrawLine(pen, c.X + c.Width / 2, c.Y, c.X + c.Width / 2, c.Bottom);
                }
                else if (which == SkinLayout.ThumbIcon) g.DrawRectangle(pen, new Rectangle(c.X, c.Y + c.Height / 4, c.Width, c.Height / 2));
                else g.DrawEllipse(pen, c);
            }
        }

        // A schematic of the keypad, with each input where main.ini puts it on Belkin's picture.
        private static void DrawKeypad(Graphics g, State state, bool isConnected)
        {
            int a = isConnected ? 255 : 110;
            Color line = Color.FromArgb(a, 200, 205, 212);
            using (var pen = new Pen(line, 1.6f))
            using (var body = new SolidBrush(Color.FromArgb(14, 16, 21)))
            {
                pen.LineJoin = LineJoin.Round;
                // Wrist rest.
                using (GraphicsPath p = Rounded(new RectangleF(120, 224, 128, 94), 26))
                {
                    g.FillPath(body, p);
                    g.DrawPath(pen, p);
                }
                // Key deck, with a notch at its lower right where the wheel sits.
                using (var p = new GraphicsPath())
                {
                    p.AddLines(new[]
                    {
                        new PointF(116, 146), new PointF(208, 146), new PointF(216, 154), new PointF(216, 184), new PointF(211, 189),
                        new PointF(193, 189), new PointF(193, 210), new PointF(187, 216), new PointF(116, 216), new PointF(108, 208), new PointF(108, 154),
                    });
                    p.CloseFigure();
                    g.FillPath(body, p);
                    g.DrawPath(pen, p);
                }
                // Thumb module: wheel, round button, D-pad and thumb pad.
                using (GraphicsPath p = Rounded(new RectangleF(195, 191, 63, 87), 15))
                {
                    g.FillPath(body, p);
                    g.DrawPath(pen, p);
                }
                Color keyFill = state == State.Over ? Color.FromArgb(235, 225, 60) : Color.FromArgb(a, 50, 54, 62);
                using (var kf = new SolidBrush(keyFill))
                {
                    for (int n = 1; n <= 19; n++)
                    {
                        if (n >= 16 && n <= 18) continue; // the wheel, below
                        using (GraphicsPath p = Rounded(SkinLayout.Picture[n - 1], 2))
                        {
                            g.FillPath(kf, p);
                            g.DrawPath(pen, p);
                        }
                    }
                    // Scroll wheel: one rounded wheel over the three wheel inputs (up, click, down).
                    using (GraphicsPath p = Rounded(new RectangleF(199, 194, 13, 25), 6))
                    {
                        g.FillPath(kf, p);
                        g.DrawPath(pen, p);
                    }
                    using (var ridge = new Pen(Color.FromArgb(a, 120, 126, 136), 0.8f))
                        for (float y = 198; y <= 216; y += 3) g.DrawLine(ridge, 201, y, 210, y);
                    var dpad = new Rectangle(221, 217, 26, 26);
                    g.DrawEllipse(pen, dpad);
                    for (int n = 20; n <= 27; n++) g.FillEllipse(kf, Rectangle.Inflate(SkinLayout.Picture[n - 1], 1, 1));
                }
            }
        }
    }
}
