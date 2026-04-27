using System.Drawing.Drawing2D;
namespace SOPRO.WinForms.Forms.Disenador;

public class DivisorFranja : Panel
{
    public event Action<int>? DeltaY;
    private bool _drag, _hover;
    private int  _y0;
    private static readonly Color CN = Color.FromArgb(55, 55, 85);
    private static readonly Color CH = Color.FromArgb(0, 120, 215);
    private static readonly Color CA = Color.FromArgb(0, 160, 255);

    public DivisorFranja()
    {
        Height = 10; Cursor = Cursors.SizeNS;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(228, 231, 242);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var c = _drag ? CA : _hover ? CH : CN;
        int cy = Height / 2, cx = Width / 2;
        using (var pen = new Pen(c, _drag ? 2f : 1f))
            g.DrawLine(pen, 8, cy, Width - 8, cy);
        using var br = new SolidBrush(c);
        for (int i = -3; i <= 3; i++)
            g.FillEllipse(br, cx + i * 7 - 2, cy - 2, 4, 4);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true;  Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _drag = true; _y0 = e.Y; Capture = true; Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_drag) return;
        int d = e.Y - _y0;
        if (d != 0) DeltaY?.Invoke(d);
    }
    protected override void OnMouseUp(MouseEventArgs e) { _drag = false; Capture = false; Invalidate(); }
}
