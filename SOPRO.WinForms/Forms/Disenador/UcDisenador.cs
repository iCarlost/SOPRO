// ============================================================
// SOPRO — UcDisenador.cs
// UserControl del diseñador WYSIWYG de Encabezado/Pie de Página PDF.
// Se aloja en el tercer tab de FormPlantillaReporte.
//
// Diferencias respecto al POC:
//   - Recibe SOPROContext + Proyecto (no ProyectoStore)
//   - Carga/guarda elementos desde PlantillasReporteElementos en BD
//   - AlturaEncabezadoDmm y AlturaPieDmm se persisten en PlantillaReporte
// ============================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms.Disenador;
using SOPRO.WinForms.Services;

namespace SOPRO.WinForms.Forms
{
    public partial class UcDisenador : UserControl
    {
        // ── Constantes de layout ──────────────────────────────
        private const int LABEL_H   = 28;
        private const int DIVISOR_H = 10;
        private const int DETALLE_H = 88;
        private const int MIN_ENC_PX = 60;
        private const int MIN_PIE_PX = 40;
        private const int DEF_ENC_PX = 180;
        private const int DEF_PIE_PX = 90;

        // ── Dependencias SOPRO ────────────────────────────────
        private readonly SOPROContext  _ctx;
        private readonly Proyecto      _proyecto;
        private PlantillaReporte       _plantilla;

        // ── Estado ────────────────────────────────────────────
        private int        _altEncPx = DEF_ENC_PX;
        private int        _altPiePx = DEF_PIE_PX;
        private ZonaCanvas _zona     = ZonaCanvas.Encabezado;
        private bool       _upd      = false;

        private readonly CanvasCommandService _cmd   = new();
        private readonly ImagenCache          _cache = new();

        private CanvasPanel          CanvasActivo => _zona == ZonaCanvas.Encabezado ? cvEnc : cvPie;
        private List<ElementoCanvas> ListaActiva  => _zona == ZonaCanvas.Encabezado
            ? cvEnc.Elementos : cvPie.Elementos;
        private ElementoCanvas?      Sel          => CanvasActivo.ElementoSeleccionado;

        // ── Colores ───────────────────────────────────────────
        private static readonly Color C_DARK    = Color.FromArgb(40,  40,  65);
        private static readonly Color C_MID     = Color.FromArgb(55,  55,  85);
        private static readonly Color C_PROPS   = Color.FromArgb(245, 248, 252);
        private static readonly Color C_FRANJA  = Color.FromArgb(172, 195, 232);
        private static readonly Color C_IND     = Color.FromArgb(135, 180, 225);
        private static readonly Color C_FIELD   = Color.FromArgb(80,  80,  108);
        private static readonly Color C_SINSEL  = Color.FromArgb(140, 140, 165);
        private static readonly Color C_SEP     = Color.FromArgb(200, 200, 220);
        private static readonly Color C_POS     = Color.FromArgb(110, 110, 135);
        private static readonly Color C_DETALLE = Color.FromArgb(238, 241, 250);
        private static readonly Color C_VISTA   = Color.FromArgb(185, 190, 208);
        private static readonly Color C_SNAP_ON = Color.FromArgb(0,   140,  60);
        private static readonly Color C_SNAP_OFF= Color.FromArgb(140, 140, 165);

        // ── Constructor ───────────────────────────────────────
        public UcDisenador(SOPROContext ctx, Proyecto proyecto)
        {
            _ctx      = ctx;
            _proyecto = proyecto;
            InitializeComponent();
            AplicarEstilos();
            InyectarDependencias();
        }

        // ── Carga inicial (llamar al activar el tab) ──────────
        public void Inicializar()
        {
            if (_plantilla != null) return;   // ya inicializado

            // Obtener o crear plantilla
            _plantilla = _ctx.PlantillasReporte
                .FirstOrDefault(p => p.ProyectoId == _proyecto.Id);
            if (_plantilla == null)
            {
                _plantilla = new PlantillaReporte { ProyectoId = _proyecto.Id };
                _ctx.PlantillasReporte.Add(_plantilla);
                _ctx.SaveChanges();
            }

            // Cargar elementos desde BD
            var elementos = _ctx.PlantillasReporteElementos
                .Where(e => e.PlantillaReporteId == _plantilla.Id)
                .OrderBy(e => e.ZOrder)
                .ToList();

            cvEnc.Elementos.Clear();
            cvPie.Elementos.Clear();

            foreach (var ent in elementos)
            {
                var el = EntidadAModelo(ent);
                if (ent.Zona == "Encabezado") cvEnc.Elementos.Add(el);
                else                          cvPie.Elementos.Add(el);
            }

            cvEnc.AnchoHojaDmm = Unidades.ANCHO_BASE_DMM;
            cvPie.AnchoHojaDmm = Unidades.ANCHO_BASE_DMM;
            cvEnc.EsActivo = true;  cvEnc.Cursor = Cursors.Default;
            cvPie.EsActivo = false; cvPie.Cursor = Cursors.No;

            // Primer layout para que CanvasPanel calcule Escala real antes de restaurar alturas
            ReLayout();

            // Restaurar alturas persistidas usando la escala actual del canvas
            RestaurarAlturas();

            // Relayout final con alturas restauradas
            ReLayout();
            cvEnc.Invalidate();
            cvPie.Invalidate();
        }

        // ── Persistencia ──────────────────────────────────────
        public void Guardar()
        {
            if (_plantilla == null) return;

            // Actualizar alturas en la plantilla
            _plantilla.AlturaEncabezadoDmm = Unidades.PxADmm(
                Math.Max(1, _altEncPx - 18), Math.Max(0.1, cvEnc.Escala));
            _plantilla.AlturaPieDmm = Unidades.PxADmm(
                Math.Max(1, _altPiePx - 18), Math.Max(0.1, cvPie.Escala));

            // Borrar elementos existentes y reinsertar
            var viejos = _ctx.PlantillasReporteElementos
                .Where(e => e.PlantillaReporteId == _plantilla.Id)
                .ToList();
            _ctx.PlantillasReporteElementos.RemoveRange(viejos);

            foreach (var el in cvEnc.Elementos)
                _ctx.PlantillasReporteElementos.Add(ModeloAEntidad(el, "Encabezado"));
            foreach (var el in cvPie.Elementos)
                _ctx.PlantillasReporteElementos.Add(ModeloAEntidad(el, "PieDePagina"));

            _ctx.SaveChanges();
        }

        // ── Conversión modelo ↔ entidad ───────────────────────
        private static ElementoCanvas EntidadAModelo(PlantillaReporteElemento ent) => new()
        {
            Id            = Guid.NewGuid(),
            Tipo          = ent.Tipo switch
            {
                "EtiquetaDinamica" => TipoElemento.EtiquetaDinamica,
                "Imagen"           => TipoElemento.Imagen,
                _                  => TipoElemento.TextoLibre,
            },
            X = ent.X, Y = ent.Y, Ancho = ent.Ancho, Alto = ent.Alto,
            Contenido     = ent.Contenido ?? "",
            Fuente        = ent.Fuente ?? "Segoe UI",
            TamanoFuente  = (float)ent.TamanoFuente,
            Negrita       = ent.Negrita,
            Cursiva       = ent.Cursiva,
            ColorTextoHex = ent.ColorTextoHex ?? "#000000",
            Alineacion    = ent.Alineacion ?? "MiddleLeft",
            ZOrder        = ent.ZOrder,
            ImagenBytes   = ent.ImagenBytes,
            ImagenNombreOrigen = ent.ImagenNombreOrigen,
            ImagenRutaOrigen   = ent.ImagenRutaOrigen,
            ImagenMimeType     = ent.ImagenMimeType,
        };

        private PlantillaReporteElemento ModeloAEntidad(ElementoCanvas el, string zona) => new()
        {
            PlantillaReporteId = _plantilla.Id,
            Zona      = zona,
            Tipo      = el.Tipo.ToString(),
            X = el.X, Y = el.Y, Ancho = el.Ancho, Alto = el.Alto,
            Contenido = el.Contenido,
            Fuente    = el.Fuente,
            TamanoFuente  = el.TamanoFuente,
            Negrita   = el.Negrita,
            Cursiva   = el.Cursiva,
            ColorTextoHex = el.ColorTextoHex,
            Alineacion    = el.Alineacion,
            ZOrder    = el.ZOrder,
            ImagenBytes   = el.ImagenBytes,
            ImagenNombreOrigen = el.ImagenNombreOrigen,
            ImagenRutaOrigen   = el.ImagenRutaOrigen,
            ImagenMimeType     = el.ImagenMimeType,
        };

        // ── Restaurar alturas ─────────────────────────────────
        private void RestaurarAlturas()
        {
            double escEnc = Math.Max(0.1, cvEnc.Escala);
            double escPie = Math.Max(0.1, cvPie.Escala);

            if (_plantilla.AlturaEncabezadoDmm > 0)
            {
                _altEncPx = Math.Clamp(Unidades.DmmAPx(_plantilla.AlturaEncabezadoDmm, escEnc) + 18, MIN_ENC_PX, 400);
            }
            if (_plantilla.AlturaPieDmm > 0)
            {
                _altPiePx = Math.Clamp(Unidades.DmmAPx(_plantilla.AlturaPieDmm, escPie) + 18, MIN_PIE_PX, 200);
            }
        }

        // ── Layout ────────────────────────────────────────────
        private void ReLayout()
        {
            int anchoDisp   = pnlVista.ClientSize.Width - 48 - 20;
            int anchoCanvas = Math.Max(400, anchoDisp);

            int y = 0;
            Pos(lblFranjaEnc, 0, y, anchoCanvas, LABEL_H);   y += LABEL_H;
            Pos(cvEnc,        0, y, anchoCanvas, _altEncPx);  y += _altEncPx;
            Pos(divEnc,       0, y, anchoCanvas, DIVISOR_H);  y += DIVISOR_H;
            Pos(pnlDetalle,   0, y, anchoCanvas, DETALLE_H);  y += DETALLE_H;
            Pos(lblFranjaPie, 0, y, anchoCanvas, LABEL_H);   y += LABEL_H;
            Pos(cvPie,        0, y, anchoCanvas, _altPiePx);  y += _altPiePx;
            Pos(divPie,       0, y, anchoCanvas, DIVISOR_H);  y += DIVISOR_H;

            cvEnc.AltoZonaDmm = Unidades.PxADmm(Math.Max(1, _altEncPx - 18), Math.Max(0.1, cvEnc.Escala));
            cvPie.AltoZonaDmm = Unidades.PxADmm(Math.Max(1, _altPiePx - 18), Math.Max(0.1, cvPie.Escala));

            pnlContenedor.Width    = anchoCanvas;
            pnlContenedor.Height   = y;
            pnlContenedor.Location = new Point(24, 24);

            double encMm = Unidades.DmmAMm(cvEnc.AltoZonaDmm);
            double pieMm = Unidades.DmmAMm(cvPie.AltoZonaDmm);
            lblIndicadorEnc.Text     = $"{encMm:F0} mm";
            lblIndicadorEnc.Location = new Point(anchoCanvas - 104, lblFranjaEnc.Top + 4);
            lblIndicadorPie.Text     = $"{pieMm:F0} mm";
            lblIndicadorPie.Location = new Point(anchoCanvas - 104, lblFranjaPie.Top + 4);

            pnlContenedor.Invalidate();
        }

        private static void Pos(Control c, int x, int y, int w, int h)
            => c.SetBounds(x, y, w, h);

        // ── Dependencias ──────────────────────────────────────
        private void InyectarDependencias()
        {
            cvEnc.Cmd = _cmd; cvEnc.Cache = _cache;
            cvPie.Cmd = _cmd; cvPie.Cache = _cache;
            cvEnc.SeleccionCambiada += OnSel;
            cvEnc.CanvasModificado  += OnCanvasModificado;
            cvPie.SeleccionCambiada += OnSel;
            cvPie.CanvasModificado  += OnCanvasModificado;
            pnlVista.Resize += (_, _) => ReLayout();
        }

        // ── Canvas modificado → auto-save ─────────────────────
        private void OnCanvasModificado()
        {
            ActualizarInfo();
            try { Guardar(); } catch { }
        }

        // ── Zona ─────────────────────────────────────────────
        private void TsBtnEncabezado_Click(object s, EventArgs e) => SetZona(ZonaCanvas.Encabezado);
        private void TsBtnPie_Click(object s, EventArgs e)        => SetZona(ZonaCanvas.PieDePagina);

        private void SetZona(ZonaCanvas z)
        {
            if (z == ZonaCanvas.PieDePagina) cvEnc.DeselectAll();
            else                              cvPie.DeselectAll();
            _zona = z;
            cvEnc.EsActivo  = z == ZonaCanvas.Encabezado;
            cvPie.EsActivo  = z == ZonaCanvas.PieDePagina;
            cvEnc.Cursor    = cvEnc.EsActivo ? Cursors.Default : Cursors.No;
            cvPie.Cursor    = cvPie.EsActivo ? Cursors.Default : Cursors.No;
            tsDdZona.Text   = z == ZonaCanvas.Encabezado
                ? "\u25b2 Encabezado \u25be"
                : "\u25bc Pie de p\u00e1gina \u25be";
            cvEnc.BackColor = z == ZonaCanvas.Encabezado ? Color.White : Color.FromArgb(248, 248, 252);
            cvPie.BackColor = z == ZonaCanvas.PieDePagina ? Color.White : Color.FromArgb(248, 248, 252);
            OnSel(null);
        }

        // ── Divisores ─────────────────────────────────────────
        private void DivEnc_DeltaY(int d)
        {
            _altEncPx = Math.Max(MIN_ENC_PX, _altEncPx + d);
            ReLayout();
            try { Guardar(); } catch { }
        }
        private void DivPie_DeltaY(int d)
        {
            _altPiePx = Math.Max(MIN_PIE_PX, _altPiePx + d);
            ReLayout();
            try { Guardar(); } catch { }
        }

        private void PnlDetalle_MouseDown(object s, MouseEventArgs e)
        { cvEnc.DeselectAll(); cvPie.DeselectAll(); }

        // ── Pintura ───────────────────────────────────────────
        private void PnlContenedor_Paint(object s, PaintEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(100, 0, 0, 0), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlContenedor.Width - 1, pnlContenedor.Height - 1);
        }

        private void PnlDetalle_Paint(object s, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var r = pnlDetalle.ClientRectangle;
            using (var h = new HatchBrush(HatchStyle.LightUpwardDiagonal,
                                          Color.FromArgb(10, 0, 0, 160), Color.Transparent))
                g.FillRectangle(h, r);
            using (var pen = new Pen(Color.FromArgb(170, 55, 55, 85), 1))
            { g.DrawLine(pen, 0, 0, r.Width, 0); g.DrawLine(pen, 0, r.Height - 1, r.Width, r.Height - 1); }
            using var fI = new Font("Segoe UI", 18f);
            using var fT = new Font("Segoe UI", 9f, FontStyle.Italic);
            using var br = new SolidBrush(Color.FromArgb(115, 60, 60, 110));
            const string ICO  = "\u2261";
            const string TEXT = "Contenido del reporte  \u2014  generado autom\u00e1ticamente";
            var sI = g.MeasureString(ICO, fI); var sT = g.MeasureString(TEXT, fT);
            float sx = (r.Width - sI.Width - 8 - sT.Width) / 2f, cy = r.Height / 2f;
            g.DrawString(ICO,  fI, br, sx,                cy - sI.Height / 2f);
            g.DrawString(TEXT, fT, br, sx + sI.Width + 8, cy - sT.Height / 2f);
        }

        // ── Insertar ─────────────────────────────────────────
        private void TsBtnTexto_Click(object s, EventArgs e)
        {
            var el = new ElementoCanvas { Tipo=TipoElemento.TextoLibre, X=Unidades.MmADmm(5), Y=Unidades.MmADmm(5), Ancho=Unidades.MmADmm(50), Alto=Unidades.MmADmm(12), Contenido="Texto libre", TamanoFuente=10f };
            _cmd.Agregar(ListaActiva, el); CanvasActivo.AgregarElemento(el); OnCanvasModificado();
        }

        private void TsBtnEtiqueta_Click(object s, EventArgs e)
        {
            var menu = new ContextMenuStrip();

            foreach (var kv in ReporteService.CamposDisponibles)
            {
                // En PDF el texto libre ya tiene su propio botón en el toolbar.
                if (string.Equals(kv.Key, "{texto_libre}", StringComparison.OrdinalIgnoreCase))
                    continue;

                var token = kv.Key;
                var descripcion = kv.Value;
                var item = new ToolStripMenuItem(descripcion)
                {
                    ToolTipText = token,
                    Tag = token
                };
                item.Click += (_, _) =>
                {
                    var el = new ElementoCanvas
                    {
                        Tipo = TipoElemento.EtiquetaDinamica,
                        X = Unidades.MmADmm(5),
                        Y = Unidades.MmADmm(5),
                        Ancho = Unidades.MmADmm(50),
                        Alto = Unidades.MmADmm(10),
                        Contenido = token,
                        TamanoFuente = 9f,
                        ColorTextoHex = "#004E9E"
                    };
                    _cmd.Agregar(ListaActiva, el);
                    CanvasActivo.AgregarElemento(el);
                    OnCanvasModificado();
                };
                menu.Items.Add(item);
            }

            menu.Show(Cursor.Position);
        }

        private void TsBtnImagen_Click(object s, EventArgs e)
        {
            using var dlg = new OpenFileDialog { Title="Seleccionar imagen", Filter="Im\u00e1genes|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos|*.*" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            var el = new ElementoCanvas { Tipo=TipoElemento.Imagen, X=Unidades.MmADmm(5), Y=Unidades.MmADmm(3), Ancho=Unidades.MmADmm(30), Alto=Unidades.MmADmm(25) };
            if (!_cache.ImportarDesdeRuta(el, dlg.FileName))
            { MessageBox.Show("No se pudo cargar la imagen.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            _cmd.Agregar(ListaActiva, el); CanvasActivo.AgregarElemento(el); OnCanvasModificado();
        }

        // ── Editar ────────────────────────────────────────────
        private void TsBtnDuplicar_Click(object s, EventArgs e) { var n = _cmd.Duplicar(ListaActiva, Sel); if (n != null) { CanvasActivo.AgregarElemento(n); OnCanvasModificado(); } }
        private void TsBtnCopiar_Click(object s, EventArgs e)   { _cmd.Copiar(Sel); ActualizarBotones(); }
        private void TsBtnPegar_Click(object s, EventArgs e)    { var n = _cmd.Pegar(ListaActiva); if (n != null) { CanvasActivo.AgregarElemento(n); OnCanvasModificado(); } }
        private void TsBtnEliminar_Click(object s, EventArgs e) { CanvasActivo.EliminarSeleccionado(); OnCanvasModificado(); }
        private void TsBtnAlFrente_Click(object s, EventArgs e) { _cmd.TraerAlFrente(ListaActiva, Sel); CanvasActivo.Invalidate(); ActualizarInfo(); OnCanvasModificado(); }
        private void TsBtnAtras_Click(object s, EventArgs e)    { _cmd.EnviarAtras(ListaActiva, Sel);   CanvasActivo.Invalidate(); ActualizarInfo(); OnCanvasModificado(); }

        private void TsBtnSnap_Click(object s, EventArgs e)
        {
            _cmd.SnapActivo     = tsBtnSnap.Checked;
            tsBtnSnap.ForeColor = _cmd.SnapActivo ? C_SNAP_ON : C_SNAP_OFF;
            tsBtnSnap.Text      = _cmd.SnapActivo ? "Snap \u25a3" : "Snap \u25a2";
        }

        // ── Selección ─────────────────────────────────────────
        private void OnSel(ElementoCanvas? el)
        {
            _upd = true;
            bool hay = el != null;
            pnlCampos.Visible       = hay;
            lblSinSeleccion.Visible = !hay;
            if (el != null)
            {
                txtContenido.Text      = el.Contenido;
                txtContenido.ReadOnly  = el.Tipo == TipoElemento.Imagen;
                cboFuente.SelectedItem = el.Fuente;
                if (cboFuente.SelectedItem == null) cboFuente.SelectedIndex = 0;
                numTamano.Value    = (decimal)el.TamanoFuente;
                chkNegrita.Checked = el.Negrita; chkCursiva.Checked = el.Cursiva;
                btnColor.BackColor = el.ColorTexto;
                cboAlineacion.SelectedIndex = el.AlineacionEnum switch
                {
                    ContentAlignment.MiddleCenter=>1, ContentAlignment.MiddleRight=>2,
                    ContentAlignment.TopLeft=>3,      ContentAlignment.TopCenter=>4,
                    ContentAlignment.TopRight=>5,     ContentAlignment.BottomLeft=>6,
                    ContentAlignment.BottomCenter=>7, ContentAlignment.BottomRight=>8, _=>0,
                };
                ActualizarInfo();
            }
            _upd = false; ActualizarBotones();
        }

        private void ActualizarInfo()
        {
            var el = Sel; if (el == null) { lblPosicion.Text = "Posici\u00f3n: \u2014"; lblZOrder.Text = "Capa: \u2014"; return; }
            lblPosicion.Text = $"X: {Unidades.DmmAMm(el.X):F1}mm  Y: {Unidades.DmmAMm(el.Y):F1}mm\nAncho: {Unidades.DmmAMm(el.Ancho):F1}mm  Alto: {Unidades.DmmAMm(el.Alto):F1}mm";
            lblZOrder.Text   = $"Capa: {el.ZOrder}  (de {ListaActiva.Count - 1})";
        }

        private void ActualizarBotones()
        {
            bool sel = Sel != null;
            tsMenuDuplicar.Enabled = sel; tsMenuCopiar.Enabled = sel;
            tsMenuEliminar.Enabled = sel;
            tsMenuAlFrente.Enabled = sel && ListaActiva.Count > 1;
            tsMenuAtras.Enabled    = sel && ListaActiva.Count > 1;
            tsMenuPegar.Enabled    = _cmd.HayClipboard;
            tsDdEditar.Enabled     = sel || _cmd.HayClipboard;
            tsDdOrden.Enabled      = sel && ListaActiva.Count > 1;
        }

        // ── Handlers propiedades ──────────────────────────────
        private void TxtContenido_TextChanged(object s, EventArgs e)
        { if (_upd || Sel == null) return; Sel.Contenido = txtContenido.Text; CanvasActivo.Invalidate(); }

        private void PropsCambiadas(object s, EventArgs e)
        {
            if (_upd || Sel == null) return;
            Sel.Fuente       = cboFuente.SelectedItem?.ToString() ?? "Segoe UI";
            Sel.TamanoFuente = (float)numTamano.Value;
            Sel.Negrita      = chkNegrita.Checked; Sel.Cursiva = chkCursiva.Checked;
            Sel.AlineacionEnum = cboAlineacion.SelectedIndex switch
            {
                1=>ContentAlignment.MiddleCenter, 2=>ContentAlignment.MiddleRight,
                3=>ContentAlignment.TopLeft,      4=>ContentAlignment.TopCenter,
                5=>ContentAlignment.TopRight,     6=>ContentAlignment.BottomLeft,
                7=>ContentAlignment.BottomCenter, 8=>ContentAlignment.BottomRight,
                _=>ContentAlignment.MiddleLeft,
            };
            CanvasActivo.Invalidate();
        }

        private void BtnColor_Click(object s, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = btnColor.BackColor };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            btnColor.BackColor = dlg.Color;
            if (Sel != null) { Sel.ColorTexto = dlg.Color; CanvasActivo.Invalidate(); }
        }

        // ── Teclado ───────────────────────────────────────────
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            bool ctrl = (keyData & Keys.Control) == Keys.Control;
            var  key  = keyData & ~Keys.Control;
            if (ctrl && key == Keys.D) { TsBtnDuplicar_Click(this, EventArgs.Empty); return true; }
            if (ctrl && key == Keys.C) { TsBtnCopiar_Click(this,   EventArgs.Empty); return true; }
            if (ctrl && key == Keys.V) { TsBtnPegar_Click(this,    EventArgs.Empty); return true; }
            if (keyData == Keys.Delete && !txtContenido.Focused)
            { CanvasActivo.EliminarSeleccionado(); OnCanvasModificado(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ── Dispose ───────────────────────────────────────────
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _cache.Dispose(); components?.Dispose(); }
            base.Dispose(disposing);
        }

        // ── Estilos ───────────────────────────────────────────
        private void AplicarEstilos()
        {
            tsToolbar.BackColor = C_DARK;
            tsToolbar.Renderer  = new DarkToolStripRenderer();

            void L(ToolStripLabel l) { l.ForeColor = Color.FromArgb(155, 165, 200); l.Font = new Font("Segoe UI", 8f); }
            void B(ToolStripButton b) { b.ForeColor = Color.White; b.Font = new Font("Segoe UI", 9f); b.Padding = new Padding(6, 2, 6, 2); }
            void D(ToolStripDropDownButton d) { d.ForeColor = Color.White; d.Font = new Font("Segoe UI", 9f); d.Padding = new Padding(6, 2, 6, 2); }

            L(tsLblSecInsertar);
            foreach (var btn in new[] { tsBtnTexto, tsBtnEtiqueta, tsBtnImagen, tsBtnSnap }) B(btn);
            D(tsDdZona); D(tsDdEditar); D(tsDdOrden);

            pnlProps.BackColor       = C_PROPS;
            pnlPropsHeader.BackColor = C_MID;
            lblPropsTitle.ForeColor  = Color.White;
            lblPropsTitle.Font       = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblSinSeleccion.ForeColor = C_SINSEL;
            foreach (var l in new[] { lblContenido, lblFuente, lblTamano, lblColor, lblAlineacion })
            { l.ForeColor = C_FIELD; l.Font = new Font("Segoe UI", 8.5f); }
            chkNegrita.Font    = new Font("Segoe UI", 9f, FontStyle.Bold);
            chkCursiva.Font    = new Font("Segoe UI", 9f, FontStyle.Italic);
            btnColor.BackColor = Color.Black;
            lblSeparador.BackColor = C_SEP;
            lblPosicion.ForeColor = C_POS; lblPosicion.Font = new Font("Segoe UI", 8f);
            lblZOrder.ForeColor   = C_POS; lblZOrder.Font   = new Font("Segoe UI", 8f);

            pnlVista.BackColor   = Color.FromArgb(185, 190, 208);
            pnlDetalle.BackColor = C_DETALLE;

            foreach (var lbl in new[] { lblFranjaEnc, lblFranjaPie })
            { lbl.BackColor = C_DARK; lbl.ForeColor = C_FRANJA; lbl.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold); lbl.Padding = new Padding(10, 0, 0, 0); }
            foreach (var ind in new[] { lblIndicadorEnc, lblIndicadorPie })
            { ind.ForeColor = C_IND; ind.Font = new Font("Segoe UI", 7.5f); ind.BackColor = Color.Transparent; }
        }
    }
}
