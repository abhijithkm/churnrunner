using System.Drawing.Drawing2D;

namespace ChurnRunner;

/// <summary>
/// All pending locations drawn as one rail. ASC works in from the left end (top of the
/// location list) and DESC from the right end (bottom), and the marker sits where their
/// halves meet. When both runs finish, the two fills touch at the marker.
/// </summary>
internal sealed class MeetTrack : Control
{
    private const int RailHeight = 14;
    private const int LabelGap = 6;

    private int _ascDone, _ascTotal, _descDone, _descTotal;

    public MeetTrack()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Font = Theme.Mono(9.5F, FontStyle.Bold);
        Height = 52;
        TabStop = false;
    }

    public void SetSplit(int ascTotal, int descTotal)
    {
        _ascTotal = Math.Max(ascTotal, 0);
        _descTotal = Math.Max(descTotal, 0);
        _ascDone = Math.Min(_ascDone, _ascTotal);
        _descDone = Math.Min(_descDone, _descTotal);
        Invalidate();
    }

    public void SetAsc(int done, int total)
    {
        _ascTotal = Math.Max(total, 0);
        _ascDone = Math.Clamp(done, 0, _ascTotal);
        Invalidate();
    }

    public void SetDesc(int done, int total)
    {
        _descTotal = Math.Max(total, 0);
        _descDone = Math.Clamp(done, 0, _descTotal);
        Invalidate();
    }

    public int AscDone => _ascDone;
    public int AscTotal => _ascTotal;
    public int DescDone => _descDone;
    public int DescTotal => _descTotal;
    public int Done => _ascDone + _descDone;
    public int Total => _ascTotal + _descTotal;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int labelHeight = TextRenderer.MeasureText(g, "0", Font).Height;
        var rail = new RectangleF(0, labelHeight + LabelGap, Width - 1, RailHeight);

        using (var railPath = Rounded(rail, RailHeight / 2f))
        using (var railBrush = new SolidBrush(Theme.Rail))
        {
            g.FillPath(railBrush, railPath);

            int total = Total;
            if (total > 0)
            {
                float unit = rail.Width / total;
                g.SetClip(railPath);

                using (var asc = new SolidBrush(Theme.Asc))
                    g.FillRectangle(asc, rail.Left, rail.Top, unit * _ascDone, rail.Height);
                using (var desc = new SolidBrush(Theme.Desc))
                {
                    float w = unit * _descDone;
                    g.FillRectangle(desc, rail.Right - w, rail.Top, w, rail.Height);
                }

                // One cell per location when they are wide enough to read as cells.
                if (unit >= 7)
                {
                    using var gap = new Pen(BackColor, 1.5f);
                    for (int i = 1; i < total; i++)
                    {
                        float x = rail.Left + unit * i;
                        g.DrawLine(gap, x, rail.Top, x, rail.Bottom);
                    }
                }

                g.ResetClip();

                // Where the ASC half ends and the DESC half begins.
                float meet = rail.Left + unit * _ascTotal;
                using var marker = new Pen(Theme.Ink, 2f);
                g.DrawLine(marker, meet, rail.Top - 4, meet, rail.Bottom + 4);
            }
        }

        var top = new Rectangle(0, 0, Width, labelHeight);
        if (Total == 0)
        {
            using var body = Theme.Body(9F);
            TextRenderer.DrawText(g, "No split yet. Refresh the count first.", body, top, Theme.Slate,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            return;
        }

        TextRenderer.DrawText(g, $"ASC  {_ascDone:N0} / {_ascTotal:N0}", Font, top, Theme.Asc,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, $"{_descDone:N0} / {_descTotal:N0}  DESC", Font, top, Theme.Desc,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 90, 180);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 180);
        path.CloseFigure();
        return path;
    }
}
