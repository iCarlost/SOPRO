// ============================================================
// SOPRO POC v8 — CanvasPanel
// - Trabaja en dmm internamente
// - Escala dmm→px para dibujar
// - Regla en cm en borde superior e izquierdo
// - Snap al milímetro al soltar
// - Grid en mm (líneas menores) y cada 10mm (líneas mayores)
// - Hit-testing y handles en coordenadas px, convertidas a dmm
// ============================================================
using System.Drawing.Drawing2D;

namespace SOPRO.WinForms.Forms.Disenador;

public class CanvasPanel : Panel
{
    // ── Dimensiones físicas del canvas ───────────────────────
    // AnchoHojaDmm: ancho imprimible en dmm (lo fija el Form)
    // AltoZonaDmm:  alto de esta franja en dmm
    public int AnchoHojaDmm { get; set; } = Unidades.MmADmm(195.9);
    public int AltoZonaDmm  { get; set; } = Unidades.MmADmm(40);

    // Escala px/dmm — se recalcula cuando cambia el tamaño del panel
    public double Escala { get; private set; } = 1.0;

    // ── Dependencias ─────────────────────────────────────────
    public ImagenCache          Cache { get; set; } = new();
    public CanvasCommandService Cmd   { get; set; } = new();

    // ── Estado ───────────────────────────────────────────────
    public List<ElementoCanvas>    Elementos            { get; } = new();
    public ElementoCanvas?         ElementoSeleccionado { get; private set; }
    public ZonaCanvas              Zona                 { get; set; }

    public event Action<ElementoCanvas?>? SeleccionCambiada;
    public event Action?                  CanvasModificado;

    // ── Interacción ──────────────────────────────────────────
    private const int HANDLE_PX   = 8;
    private const int RULER_PX    = 18;   // grosor de la regla
    private bool      _drag, _resize;
    private int       _handleIdx = -1;
    // Drag: guardamos offset en dmm
    private int       _dragOffDmm_X, _dragOffDmm_Y;
    // Resize: bounds iniciales en dmm
    private (int x, int y, int w, int h) _r0;
    private Point     _resizePt0px;

    // ── Colores ───────────────────────────────────────────────
    private static readonly Color C_SEL     = Color.FromArgb(0, 120, 215);
    private static readonly Color C_RULER   = Color.FromArgb(245, 246, 250);
    private static readonly Color C_RULERBD = Color.FromArgb(200, 205, 220);
    private static readonly Color C_GRID_MM = Color.FromArgb(15,  0,   0, 180);
    private static readonly Color C_GRID_CM = Color.FromArgb(35,  0,   0, 180);
    private static readonly Color C_ETQ_BG  = Color.FromArgb(225, 242, 255);
    private static readonly Color C_ETQ_BD  = Color.FromArgb(0,   120, 215);
    private static readonly Color C_AREA_BG = Color.White;
    private static readonly Color C_AFUERA  = Color.FromArgb(230, 232, 240);

    // ── Constructores ─────────────────────────────────────────
    public CanvasPanel() : this(ZonaCanvas.Encabezado) { }
    public CanvasPanel(ZonaCanvas zona)
    {
        Zona = zona;
        DoubleBuffered = true;
        BackColor = C_AFUERA;
        BorderStyle = BorderStyle.None;
        TabStop = true;
    }

    /// <summary>
    /// Cuando es false, el canvas ignora todos los clics.
    /// Solo el dropdown de zona puede activarlo.
    /// </summary>
    public bool EsActivo { get; set; } = true;

    // ── Recalcular escala ────────────────────────────────────
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RecalcularEscala();
    }

    private void RecalcularEscala()
    {
        int areaW = Width - RULER_PX;
        if (AnchoHojaDmm > 0 && areaW > 10)
            Escala = (double)areaW / AnchoHojaDmm;
        else
            Escala = 1.0;
        Invalidate();
    }

    // Cuando el Form cambia el alto de la franja, actualiza AltoZonaDmm
    public void SetAltura(int alturaPx)
    {
        AltoZonaDmm = Unidades.PxADmm(Math.Max(1, alturaPx - RULER_PX), Escala);
    }

    // ── API pública ───────────────────────────────────────────
    public void AgregarElemento(ElementoCanvas el)
    {
        if (!Elementos.Contains(el)) Elementos.Add(el);
        ElementoSeleccionado = el;
        SeleccionCambiada?.Invoke(el);
        Invalidate();
    }

    public void SeleccionarExterno(ElementoCanvas? el)
    {
        ElementoSeleccionado = el;
        SeleccionCambiada?.Invoke(el);
        Invalidate();
    }

    public void EliminarSeleccionado()
    {
        if (ElementoSeleccionado == null) return;
        Cmd.Eliminar(Elementos, ElementoSeleccionado);
        ElementoSeleccionado = null;
        SeleccionCambiada?.Invoke(null);
        Invalidate();
    }

    public void DeselectAll()
    {
        ElementoSeleccionado = null;
        SeleccionCambiada?.Invoke(null);
        Invalidate();
    }

    // ── Conversión px↔dmm con offset de regla ────────────────
    private int PxADmm_X(int px) => Unidades.PxADmm(px - RULER_PX, Escala);
    private int PxADmm_Y(int py) => Unidades.PxADmm(py - RULER_PX, Escala);
    private int DmmAPx_X(int dmm) => Unidades.DmmAPx(dmm, Escala) + RULER_PX;
    private int DmmAPx_Y(int dmm) => Unidades.DmmAPx(dmm, Escala) + RULER_PX;

    private Rectangle ElementoEnPx(ElementoCanvas el) => new(
        DmmAPx_X(el.X), DmmAPx_Y(el.Y),
        Unidades.DmmAPx(el.Ancho, Escala),
        Unidades.DmmAPx(el.Alto,  Escala));

    // ── Pintura ───────────────────────────────────────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        DibujarAreaBlanca(g);
        DibujarGrid(g);
        DibujarRegla(g);

        foreach (var el in Elementos.OrderBy(x => x.ZOrder))
            PintarElemento(g, el, el == ElementoSeleccionado);
    }

    private void DibujarAreaBlanca(Graphics g)
    {
        // Área de la hoja (zona imprimible)
        int areaX = RULER_PX;
        int areaW = Unidades.DmmAPx(AnchoHojaDmm, Escala);
        int areaH = Height;
        g.FillRectangle(new SolidBrush(C_AREA_BG), areaX, RULER_PX, areaW, areaH);
        // Borde de la hoja
        using var pen = new Pen(Color.FromArgb(180, 180, 200), 1);
        g.DrawRectangle(pen, areaX, RULER_PX, areaW - 1, areaH - RULER_PX - 1);
    }

    private void DibujarGrid(Graphics g)
    {
        if (Escala < 0.3) return; // demasiado pequeño para ver grid

        int areaX = RULER_PX;
        int areaY = RULER_PX;
        int areaW = Unidades.DmmAPx(AnchoHojaDmm, Escala);
        int areaH = Height - RULER_PX;

        // Grid menor: cada 1mm = 10 dmm
        using var penMm = new Pen(C_GRID_MM, 1) { DashStyle = DashStyle.Custom, DashPattern = new[] { 1f, 3f } };
        // Grid mayor: cada 10mm = 100 dmm
        using var penCm = new Pen(C_GRID_CM, 1) { DashStyle = DashStyle.Dot };

        int pasoDmm   = 10;   // 1mm
        int pasoMayDmm = 100; // 10mm

        for (int dmm = 0; dmm <= AnchoHojaDmm; dmm += pasoDmm)
        {
            int px = DmmAPx_X(dmm);
            bool mayor = dmm % pasoMayDmm == 0;
            g.DrawLine(mayor ? penCm : penMm, px, areaY, px, areaY + areaH);
        }
        for (int dmm = 0; dmm <= AltoZonaDmm + 100; dmm += pasoDmm)
        {
            int py = DmmAPx_Y(dmm);
            if (py > Height) break;
            bool mayor = dmm % pasoMayDmm == 0;
            g.DrawLine(mayor ? penCm : penMm, areaX, py, areaX + areaW, py);
        }
    }

    private void DibujarRegla(Graphics g)
    {
        // Fondo de la regla
        using var bgBrush = new SolidBrush(C_RULER);
        g.FillRectangle(bgBrush, 0, 0, Width, RULER_PX);            // horizontal
        g.FillRectangle(bgBrush, 0, 0, RULER_PX, Height);           // vertical
        // Esquina
        g.FillRectangle(bgBrush, 0, 0, RULER_PX, RULER_PX);

        using var pen   = new Pen(C_RULERBD, 1);
        g.DrawLine(pen, 0, RULER_PX, Width, RULER_PX);              // borde inferior horizontal
        g.DrawLine(pen, RULER_PX, 0, RULER_PX, Height);             // borde derecho vertical

        using var font  = new Font("Segoe UI", 6f);
        using var brush = new SolidBrush(Color.FromArgb(100, 100, 120));

        // Marcas horizontales (cada 10mm = 1cm)
        for (int dmm = 0; dmm <= AnchoHojaDmm; dmm += 100)
        {
            int px = DmmAPx_X(dmm);
            int cm = dmm / 100;
            // Marca
            g.DrawLine(pen, px, RULER_PX - 5, px, RULER_PX);
            // Etiqueta cada 2cm para no amontonar
            if (cm % 2 == 0 && px + 2 < Width)
                g.DrawString($"{cm}", font, brush, px + 1, 2);
        }
        // Marcas intermedias (cada 5mm)
        for (int dmm = 50; dmm <= AnchoHojaDmm; dmm += 100)
        {
            int px = DmmAPx_X(dmm);
            g.DrawLine(pen, px, RULER_PX - 3, px, RULER_PX);
        }

        // Marcas verticales (cada 10mm = 1cm)
        var sfV = new StringFormat { FormatFlags = StringFormatFlags.DirectionVertical };
        for (int dmm = 0; dmm <= AltoZonaDmm + 100; dmm += 100)
        {
            int py = DmmAPx_Y(dmm);
            if (py > Height) break;
            int cm = dmm / 100;
            g.DrawLine(pen, RULER_PX - 5, py, RULER_PX, py);
            if (cm % 2 == 0 && py + 2 < Height)
                g.DrawString($"{cm}", font, brush, 2, py + 1);
        }
    }

    private void PintarElemento(Graphics g, ElementoCanvas el, bool sel)
    {
        var r = ElementoEnPx(el);
        if (r.Width < 1 || r.Height < 1) return;

        if (el.Tipo == TipoElemento.Imagen)
        {
            var img = Cache.ObtenerImagen(el);
            if (img != null) g.DrawImage(img, r);
            else
            {
                g.FillRectangle(Brushes.LightGray, r);
                using var fnt = new Font("Segoe UI", 7.5f, FontStyle.Italic);
                g.DrawString("Sin imagen", fnt, Brushes.Gray, r,
                    new StringFormat { Alignment = StringAlignment.Center,
                                       LineAlignment = StringAlignment.Center });
            }
        }
        else
        {
            if (el.Tipo == TipoElemento.EtiquetaDinamica)
            {
                g.FillRectangle(new SolidBrush(C_ETQ_BG), r);
                using var bdr = new Pen(C_ETQ_BD, 1) { DashStyle = DashStyle.Dash };
                g.DrawRectangle(bdr, r);
            }
            var fs = (el.Negrita ? FontStyle.Bold   : FontStyle.Regular)
                   | (el.Cursiva ? FontStyle.Italic : FontStyle.Regular);
            using var font  = new Font(el.Fuente, el.TamanoFuente, fs);
            using var brush = new SolidBrush(el.ColorTexto);
            var sf = new StringFormat
            {
                LineAlignment = el.AlineacionEnum is
                    ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight
                    ? StringAlignment.Near : el.AlineacionEnum is
                    ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight
                    ? StringAlignment.Far : StringAlignment.Center,
                Alignment = el.AlineacionEnum is
                    ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter
                    ? StringAlignment.Center : el.AlineacionEnum is
                    ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight
                    ? StringAlignment.Far : StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter
            };
            g.DrawString(el.Contenido, font, brush, r, sf);
        }

        // Marco
        if (sel)
        {
            using var pen = new Pen(C_SEL, 1.5f) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(pen, r);
            foreach (var (hr, _) in Handles(r))
            {
                g.FillRectangle(Brushes.White, hr);
                g.DrawRectangle(new Pen(C_SEL), hr);
            }
        }
        else
        {
            using var pen = new Pen(Color.FromArgb(50, 0, 0, 0), 1);
            g.DrawRectangle(pen, r);
        }
    }

    // ── Handles ───────────────────────────────────────────────
    private List<(Rectangle rect, Cursor cur)> Handles(Rectangle r)
    {
        int h = HANDLE_PX, hh = h / 2;
        return new()
        {
            (new Rectangle(r.Left-hh,            r.Top-hh,            h,h), Cursors.SizeNWSE),
            (new Rectangle(r.Left+r.Width/2-hh,  r.Top-hh,            h,h), Cursors.SizeNS),
            (new Rectangle(r.Right-hh,           r.Top-hh,            h,h), Cursors.SizeNESW),
            (new Rectangle(r.Right-hh,           r.Top+r.Height/2-hh, h,h), Cursors.SizeWE),
            (new Rectangle(r.Right-hh,           r.Bottom-hh,         h,h), Cursors.SizeNWSE),
            (new Rectangle(r.Left+r.Width/2-hh,  r.Bottom-hh,         h,h), Cursors.SizeNS),
            (new Rectangle(r.Left-hh,            r.Bottom-hh,         h,h), Cursors.SizeNESW),
            (new Rectangle(r.Left-hh,            r.Top+r.Height/2-hh, h,h), Cursors.SizeWE),
        };
    }

    private int HitHandle(Point p)
    {
        if (ElementoSeleccionado == null) return -1;
        var hh = Handles(ElementoEnPx(ElementoSeleccionado));
        for (int i = 0; i < hh.Count; i++) if (hh[i].rect.Contains(p)) return i;
        return -1;
    }

    private ElementoCanvas? HitEl(Point p)
    {
        foreach (var el in Elementos.OrderByDescending(x => x.ZOrder))
            if (ElementoEnPx(el).Contains(p)) return el;
        return null;
    }

    // ── Mouse ─────────────────────────────────────────────────
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (!EsActivo) return;   // zona inactiva — solo el dropdown puede activarla
        Focus();
        if (e.Button != MouseButtons.Left) return;

        int hi = HitHandle(e.Location);
        if (hi >= 0 && ElementoSeleccionado != null)
        {
            _resize   = true; _handleIdx = hi;
            _r0       = (ElementoSeleccionado.X, ElementoSeleccionado.Y,
                         ElementoSeleccionado.Ancho, ElementoSeleccionado.Alto);
            _resizePt0px = e.Location;
            return;
        }

        var hit = HitEl(e.Location);
        if (hit != null)
        {
            ElementoSeleccionado = hit;
            SeleccionCambiada?.Invoke(hit);
            _drag = true;
            // Offset entre el puntero y la esquina del elemento, en dmm
            _dragOffDmm_X = PxADmm_X(e.X) - hit.X;
            _dragOffDmm_Y = PxADmm_Y(e.Y) - hit.Y;
        }
        else
        {
            ElementoSeleccionado = null;
            SeleccionCambiada?.Invoke(null);
        }
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_resize && ElementoSeleccionado != null)
        {
            AplicarResize(e.Location);
            Invalidate(); CanvasModificado?.Invoke(); return;
        }
        if (_drag && ElementoSeleccionado != null)
        {
            int maxX = Math.Max(0, AnchoHojaDmm - ElementoSeleccionado.Ancho);
            int maxY = Math.Max(0, AltoZonaDmm  - ElementoSeleccionado.Alto);
            int nx = Math.Clamp(PxADmm_X(e.X) - _dragOffDmm_X, 0, maxX);
            int ny = Math.Clamp(PxADmm_Y(e.Y) - _dragOffDmm_Y, 0, maxY);
            ElementoSeleccionado.X = nx;
            ElementoSeleccionado.Y = ny;
            Invalidate(); CanvasModificado?.Invoke(); return;
        }

        int h = HitHandle(e.Location);
        Cursor = h >= 0 && ElementoSeleccionado != null
            ? Handles(ElementoEnPx(ElementoSeleccionado))[h].cur
            : HitEl(e.Location) != null ? Cursors.SizeAll : Cursors.Default;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        // Snap al soltar
        if ((_drag || _resize) && ElementoSeleccionado != null && Cmd.SnapActivo)
        {
            Cmd.AplicarSnap(ElementoSeleccionado);
            Invalidate();
        }
        _drag = _resize = false; _handleIdx = -1;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (!EsActivo) return;
        var hit = HitEl(e.Location);
        if (hit == null || hit.Tipo == TipoElemento.Imagen) return;
        EditarInPlace(hit);
    }

    // ── Teclado ───────────────────────────────────────────────
    protected override bool IsInputKey(Keys keyData)
    {
        if (keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down) return true;
        return base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (ElementoSeleccionado == null) { base.OnKeyDown(e); return; }
        switch (e.KeyCode)
        {
            case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down:
                Cmd.MoverConTeclado(ElementoSeleccionado, e.KeyCode, e.Shift, AnchoHojaDmm, AltoZonaDmm);
                Invalidate(); CanvasModificado?.Invoke(); e.Handled = true; break;
            case Keys.Delete:
                EliminarSeleccionado(); e.Handled = true; break;
        }
        base.OnKeyDown(e);
    }

    // ── Edición in-place ──────────────────────────────────────
    private void EditarInPlace(ElementoCanvas el)
    {
        var r = ElementoEnPx(el);
        var fs = (el.Negrita ? FontStyle.Bold   : FontStyle.Regular)
               | (el.Cursiva ? FontStyle.Italic : FontStyle.Regular);
        var tb = new TextBox
        {
            Bounds      = r,
            Text        = el.Contenido,
            Font        = new Font(el.Fuente, el.TamanoFuente, fs),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor   = Color.FromArgb(255, 255, 215),
            Multiline   = true,
        };
        Controls.Add(tb); tb.BringToFront(); tb.Focus(); tb.SelectAll();
        tb.LostFocus += (_, _) =>
        {
            el.Contenido = tb.Text;
            Controls.Remove(tb); tb.Dispose();
            CanvasModificado?.Invoke(); Invalidate();
        };
        tb.KeyDown += (_, ke) =>
        {
            if (ke.KeyCode == Keys.Escape)             { tb.Text = el.Contenido; tb.Parent?.Focus(); }
            if (ke.KeyCode == Keys.Enter && !ke.Shift)   tb.Parent?.Focus();
        };
    }

    // ── Resize en dmm ────────────────────────────────────────
    private void AplicarResize(Point p)
    {
        if (ElementoSeleccionado == null) return;
        // Delta en dmm
        int dx = Unidades.PxADmm(p.X - _resizePt0px.X, Escala);
        int dy = Unidades.PxADmm(p.Y - _resizePt0px.Y, Escala);
        var (x0, y0, w0, h0) = _r0;
        const int MIN_DMM = 80; // mínimo 8mm = 80dmm

        (int x1, int y1, int x2, int y2) = _handleIdx switch
        {
            0 => (Math.Min(x0+dx, x0+w0-MIN_DMM), Math.Min(y0+dy, y0+h0-MIN_DMM), x0+w0, y0+h0),
            1 => (x0,                              Math.Min(y0+dy, y0+h0-MIN_DMM), x0+w0, y0+h0),
            2 => (x0,                              Math.Min(y0+dy, y0+h0-MIN_DMM), Math.Max(x0+w0+dx, x0+MIN_DMM), y0+h0),
            3 => (x0, y0,                           Math.Max(x0+w0+dx, x0+MIN_DMM), y0+h0),
            4 => (x0, y0,                           Math.Max(x0+w0+dx, x0+MIN_DMM), Math.Max(y0+h0+dy, y0+MIN_DMM)),
            5 => (x0, y0,                           x0+w0,                          Math.Max(y0+h0+dy, y0+MIN_DMM)),
            6 => (Math.Min(x0+dx, x0+w0-MIN_DMM), y0, x0+w0,                      Math.Max(y0+h0+dy, y0+MIN_DMM)),
            7 => (Math.Min(x0+dx, x0+w0-MIN_DMM), y0, x0+w0,                      y0+h0),
            _ => (x0, y0, x0+w0, y0+h0)
        };

        ElementoSeleccionado.X     = x1;
        ElementoSeleccionado.Y     = y1;
        ElementoSeleccionado.Ancho = x2 - x1;
        ElementoSeleccionado.Alto  = y2 - y1;
    }
}
