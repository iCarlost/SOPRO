namespace SOPRO.WinForms.Forms.Disenador;
public class DarkToolStripRenderer : ToolStripProfessionalRenderer
{
    private static readonly Color BG    = Color.FromArgb(40,  40,  65);
    private static readonly Color HOVER = Color.FromArgb(60,  80, 120);
    private static readonly Color PRESS = Color.FromArgb(0,  100, 190);
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        => e.Graphics.FillRectangle(new SolidBrush(BG), e.AffectedBounds);
    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item is not ToolStripButton b) return;
        var r = new Rectangle(Point.Empty, b.Size);
        if      (b.Pressed)  e.Graphics.FillRectangle(new SolidBrush(PRESS), r);
        else if (b.Selected) e.Graphics.FillRectangle(new SolidBrush(HOVER), r);
    }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int x = e.Item.Width / 2;
        using var p = new Pen(Color.FromArgb(65, 255, 255, 255));
        e.Graphics.DrawLine(p, x, 4, x, e.Item.Height - 4);
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Color.White : Color.FromArgb(85, 255, 255, 255);
        base.OnRenderItemText(e);
    }
}
