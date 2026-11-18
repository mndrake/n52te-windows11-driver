namespace N52te.Editor;

/// <summary>A label that wraps its text to its width and grows to fit, at any display scaling.</summary>
internal sealed class WrapLabel : Label
{
    public WrapLabel()
    {
        AutoSize = false;
        Padding = new Padding(0, 2, 0, 4);
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); FitHeight(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); FitHeight(); }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); FitHeight(); }

    private void FitHeight()
    {
        if (Width <= 0) return;
        var size = TextRenderer.MeasureText(Text.Length == 0 ? " " : Text, Font, new Size(Width - Padding.Horizontal, 0),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        int height = size.Height + Padding.Vertical;
        if (Height != height) Height = height;
    }
}

internal static class Dpi
{
    /// <summary>Makes a code-built form scale its pixel sizes with Windows display scaling. Call first in the constructor.</summary>
    public static void Scale(Form form)
    {
        form.AutoScaleDimensions = new SizeF(96F, 96F);
        form.AutoScaleMode = AutoScaleMode.Dpi;
    }

    /// <summary>Fits a form on the screen it opens on.</summary>
    public static void FitToScreen(Form form)
    {
        var area = Screen.FromControl(form).WorkingArea;
        int w = Math.Min(form.Width, (int)(area.Width * 0.95)), h = Math.Min(form.Height, (int)(area.Height * 0.95));
        form.Bounds = new Rectangle(area.Left + (area.Width - w) / 2, area.Top + (area.Height - h) / 2, w, h);
    }
}
