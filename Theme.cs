using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ChurnRunner;

/// <summary>
/// Colours, fonts and small drawing helpers for the runner window. ASC and DESC each
/// keep one colour everywhere that side appears: tiles, the meeting track and log lines.
/// </summary>
internal static class Theme
{
    // Surfaces
    public static readonly Color Canvas = Color.FromArgb(0xEE, 0xF2, 0xFB);
    public static readonly Color Panel = Color.White;
    public static readonly Color HeroFill = Color.FromArgb(0xEE, 0xF3, 0xFF);
    public static readonly Color HeroBorder = Color.FromArgb(0xD5, 0xE0, 0xFA);

    // Text and lines
    public static readonly Color Ink = Color.FromArgb(0x1B, 0x23, 0x33);
    public static readonly Color Slate = Color.FromArgb(0x66, 0x70, 0x85);
    public static readonly Color Line = Color.FromArgb(0xE2, 0xE7, 0xF1);
    public static readonly Color Rail = Color.FromArgb(0xD9, 0xE2, 0xF5);

    // Accents, each with the pale tint used behind its icon
    public static readonly Color Primary = Color.FromArgb(0x2F, 0x6F, 0xED);
    public static readonly Color PrimaryHover = Color.FromArgb(0x24, 0x5C, 0xD1);
    public static readonly Color PrimaryTint = Color.FromArgb(0xE3, 0xED, 0xFE);
    public static readonly Color Asc = Color.FromArgb(0x0E, 0x93, 0x84);
    public static readonly Color AscTint = Color.FromArgb(0xDD, 0xF4, 0xF1);
    public static readonly Color Desc = Color.FromArgb(0xE0, 0x7B, 0x1A);
    public static readonly Color DescTint = Color.FromArgb(0xFD, 0xEF, 0xE0);
    public static readonly Color Violet = Color.FromArgb(0x7C, 0x4D, 0xDB);
    public static readonly Color VioletTint = Color.FromArgb(0xEF, 0xE8, 0xFD);
    public static readonly Color Good = Color.FromArgb(0x1E, 0x9E, 0x57);
    public static readonly Color GoodTint = Color.FromArgb(0xE3, 0xF6, 0xEA);
    public static readonly Color Fault = Color.FromArgb(0xD9, 0x2D, 0x20);
    public static readonly Color FaultTint = Color.FromArgb(0xFD, 0xEC, 0xEA);
    public static readonly Color Waiting = Color.FromArgb(0x98, 0xA2, 0xB3);
    public static readonly Color WaitingTint = Color.FromArgb(0xF0, 0xF2, 0xF6);

    // Icon glyphs (Segoe Fluent Icons / Segoe MDL2 Assets share these code points)
    public const string GlyphSync = "";
    public const string GlyphRefresh = "";
    public const string GlyphPin = "";
    public const string GlyphUp = "";
    public const string GlyphDown = "";
    public const string GlyphCalendar = "";
    public const string GlyphPlug = "";
    public const string GlyphClock = "";
    public const string GlyphList = "";
    public const string GlyphPlay = "";
    public const string GlyphStop = "";

    public static Font Display(float size) => Make("Segoe UI Variable Display Semib", size, FontStyle.Regular, "Segoe UI Semibold");
    public static Font Heading(float size = 11F) => Make("Segoe UI Variable Display Semib", size, FontStyle.Regular, "Segoe UI Semibold");
    public static Font Body(float size = 9.5F, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);
    public static Font Mono(float size = 9F, FontStyle style = FontStyle.Regular) => Make("Cascadia Mono", size, style, "Consolas");
    public static Font Icons(float size) => Make("Segoe Fluent Icons", size, FontStyle.Regular, "Segoe MDL2 Assets");

    private static Font Make(string family, float size, FontStyle style, string fallback)
    {
        var font = new Font(family, size, style);
        if (font.Name == family) return font;

        // Windows substitutes a default face for a missing family; use the named fallback instead.
        font.Dispose();
        return new Font(fallback, size, style);
    }

    public static GraphicsPath Rounded(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var path = new GraphicsPath();
        if (d <= 0)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>A rounded section panel with a 1px border.</summary>
internal sealed class Card : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int Radius { get; set; } = 12;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Color BorderColor { get; set; } = Theme.Line;

    public Card()
    {
        BackColor = Theme.Panel;
        Padding = new Padding(18, 16, 18, 18);
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Canvas);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Rounded(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Radius);
        using var fill = new SolidBrush(BackColor);
        using var pen = new Pen(BorderColor);
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
    }
}

/// <summary>A pale rounded square with one icon glyph, as used on the tiles and card titles.</summary>
internal sealed class IconChip : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public string Glyph { get; set; } = "";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Color Tint { get; set; } = Theme.PrimaryTint;

    public IconChip(string glyph, Color color, Color tint, int size = 40, float glyphSize = 14F)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Glyph = glyph;
        ForeColor = color;
        Tint = tint;
        Size = new Size(size, size);
        Font = Theme.Icons(glyphSize);
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Panel);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Theme.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), Width / 4f))
        using (var fill = new SolidBrush(Tint))
            g.FillPath(fill, path);
        TextRenderer.DrawText(g, Glyph, Font, ClientRectangle, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

internal enum ButtonKind { Primary, Secondary, Danger, Ghost }

/// <summary>
/// A rounded button drawn in the window's colours, with hover, pressed, disabled
/// and keyboard-focus states. An optional icon glyph sits before the text.
/// </summary>
internal sealed class ThemedButton : Button
{
    private bool _hover, _pressed;
    private readonly ButtonKind _kind;
    private readonly Font _iconFont = Theme.Icons(10F);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public string Glyph { get; set; } = "";

    public ThemedButton(string text, ButtonKind kind, string glyph = "")
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        _kind = kind;
        Text = text;
        Glyph = glyph;
        Font = Theme.Body(9.5F, FontStyle.Bold);
        Cursor = Cursors.Hand;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Height = 36;
        Width = PreferredWidth();
    }

    private int PreferredWidth()
    {
        int text = TextRenderer.MeasureText(Text, Font).Width;
        int icon = Glyph.Length > 0 ? 22 : 0;
        return text + icon + 30;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Width = Math.Max(Width, PreferredWidth());
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Panel);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        (Color fill, Color border, Color text) = Colors();
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = Theme.Rounded(rect, 8))
        using (var brush = new SolidBrush(fill))
        using (var pen = new Pen(border))
        {
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }

        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(Theme.Primary, 2f);
            using var ring = Theme.Rounded(new RectangleF(2, 2, Width - 5, Height - 5), 7);
            g.DrawPath(focus, ring);
        }

        int textWidth = TextRenderer.MeasureText(g, Text, Font).Width;
        int iconWidth = Glyph.Length > 0 ? 22 : 0;
        int x = (Width - textWidth - iconWidth) / 2;
        if (iconWidth > 0)
        {
            TextRenderer.DrawText(g, Glyph, _iconFont, new Rectangle(x, 0, iconWidth, Height), text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        TextRenderer.DrawText(g, Text, Font, new Rectangle(x + iconWidth, 0, textWidth + 2, Height), text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private (Color fill, Color border, Color text) Colors()
    {
        if (!Enabled)
            return _kind == ButtonKind.Primary
                ? (Color.FromArgb(0xB9, 0xCC, 0xF7), Color.FromArgb(0xB9, 0xCC, 0xF7), Color.White)
                : (Theme.WaitingTint, Theme.Line, Theme.Waiting);

        return _kind switch
        {
            ButtonKind.Primary => (_hover || _pressed ? Theme.PrimaryHover : Theme.Primary, Theme.Primary, Color.White),
            ButtonKind.Danger => (_hover || _pressed ? Theme.FaultTint : Theme.Panel, Theme.Fault, Theme.Fault),
            ButtonKind.Ghost => (_hover || _pressed ? Theme.PrimaryTint : Theme.Panel, Theme.Line, Theme.Primary),
            _ => (_hover || _pressed ? Theme.Canvas : Theme.Panel, Theme.Line, Theme.Ink)
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _iconFont.Dispose();
        base.Dispose(disposing);
    }
}
