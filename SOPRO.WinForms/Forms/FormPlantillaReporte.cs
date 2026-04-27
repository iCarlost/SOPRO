using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Services;

namespace SOPRO.WinForms.Forms
{
    public partial class FormPlantillaReporte : Form
    {
        private readonly SOPROContext  _ctx;
        private readonly Proyecto      _proyecto;
        private readonly ReporteService _svc;
        private PlantillaReporte       _plantilla;
        private bool _cargando = false;

        // Zonas: 6 zonas (Enc Izq/Cen/Der, Pie Izq/Cen/Der)
        private string _zonaActual = "EncIzq";

        // Diseñador PDF — instanciación lazy al primer acceso al tab
        private UcDisenador _ucDisenador;

        public FormPlantillaReporte(SOPROContext ctx, Proyecto proyecto)
        {
            _ctx     = ctx;
            _proyecto = proyecto;
            _svc     = new ReporteService(ctx);
            InitializeComponent();
            CargarPlantilla();

            // El diseñador PDF es el primer tab, pero se inicializa hasta que
            // el formulario ya está mostrado para que UcDisenador calcule su
            // escala real y restaure correctamente las alturas persistidas.
            Shown += (_, _) => BeginInvoke(new Action(() =>
            {
                tabConfig.SelectedTab = tabDisenadorPdf;
                InicializarDisenadorPdf();
            }));
        }

        private void CargarPlantilla()
        {
            _cargando = true;
            _plantilla = _svc.ObtenerOCrearPlantilla(_proyecto.Id);

            // Cargar lista de campos disponibles
            lstCampos.Items.Clear();
            foreach (var kv in ReporteService.CamposDisponibles)
                lstCampos.Items.Add(kv.Value);

            // Datos dinámicos
            txtElabaro.Text        = _plantilla.CampoElabaro        ?? "";
            txtReviso.Text         = _plantilla.CampoReviso         ?? "";
            txtAutorizo.Text       = _plantilla.CampoAutorizo       ?? "";
            txtDependencia.Text    = _plantilla.CampoDependencia    ?? "";
            txtNumContrato.Text    = _plantilla.CampoNumeroContrato ?? "";
            txtLicitacion.Text     = _plantilla.CampoLicitacion     ?? "";
            txtTextoLibre1.Text    = _plantilla.CampoTextoLibre1    ?? "";
            txtTextoLibre2.Text    = _plantilla.CampoTextoLibre2    ?? "";

            // Alturas
            nudAltEncabezado.Value = _plantilla.EncabezadoAltura;
            nudAltPie.Value        = _plantilla.PiePaginaAltura;

            // Cargar zona por defecto
            SeleccionarZona("EncCen");
            _cargando = false;

            ActualizarPreview();
        }

        private void SeleccionarZona(string zona)
        {
            _zonaActual = zona;
            _cargando = true;

            var (tipo, contenido, fuente, tamaño, negrita, cursiva, alineacion) = ObtenerZona(zona);

            cboTipoZona.SelectedItem = tipo;
            txtContenidoZona.Text    = contenido;
            cboFuente.Text           = fuente;
            nudTamaño.Value          = (decimal)tamaño;
            chkNegrita.Checked       = negrita;
            chkCursiva.Checked       = cursiva;
            cboAlineacion.SelectedItem = alineacion;

            // Mostrar/ocultar panel según tipo
            panelTexto.Visible  = (tipo == "Texto");
            panelImagen.Visible = (tipo == "Imagen");
            if (tipo == "Imagen")
                lblRutaImagen.Text = contenido;

            // Resaltar botón activo
            foreach (var btn in new[] { btnEncIzq, btnEncCen, btnEncDer, btnPieIzq, btnPieCen, btnPieDer })
                btn.BackColor = SystemColors.Control;

            var btnActivo = zona switch
            {
                "EncIzq" => btnEncIzq, "EncCen" => btnEncCen, "EncDer" => btnEncDer,
                "PieIzq" => btnPieIzq, "PieCen" => btnPieCen, "PieDer" => btnPieDer,
                _ => btnEncCen
            };
            btnActivo.BackColor = Color.FromArgb(187, 222, 251);

            _cargando = false;
        }

        private (string tipo, string contenido, string fuente, float tamaño,
                 bool negrita, bool cursiva, string alineacion) ObtenerZona(string zona)
        {
            return zona switch
            {
                "EncIzq" => (_plantilla.EncabezadoIzqTipo,    _plantilla.EncabezadoIzqContenido,
                             _plantilla.EncabezadoIzqFuente,  _plantilla.EncabezadoIzqTamaño,
                             _plantilla.EncabezadoIzqNegrita, _plantilla.EncabezadoIzqCursiva,
                             _plantilla.EncabezadoIzqAlineacion),
                "EncCen" => (_plantilla.EncabezadoCenTipo,    _plantilla.EncabezadoCenContenido,
                             _plantilla.EncabezadoCenFuente,  _plantilla.EncabezadoCenTamaño,
                             _plantilla.EncabezadoCenNegrita, _plantilla.EncabezadoCenCursiva,
                             _plantilla.EncabezadoCenAlineacion),
                "EncDer" => (_plantilla.EncabezadoDerTipo,    _plantilla.EncabezadoDerContenido,
                             _plantilla.EncabezadoDerFuente,  _plantilla.EncabezadoDerTamaño,
                             _plantilla.EncabezadoDerNegrita, _plantilla.EncabezadoDerCursiva,
                             _plantilla.EncabezadoDerAlineacion),
                "PieIzq" => (_plantilla.PiePaginaIzqTipo,     _plantilla.PiePaginaIzqContenido,
                             _plantilla.PiePaginaIzqFuente,   _plantilla.PiePaginaIzqTamaño,
                             _plantilla.PiePaginaIzqNegrita,  _plantilla.PiePaginaIzqCursiva,
                             _plantilla.PiePaginaIzqAlineacion),
                "PieCen" => (_plantilla.PiePaginaCenTipo,     _plantilla.PiePaginaCenContenido,
                             _plantilla.PiePaginaCenFuente,   _plantilla.PiePaginaCenTamaño,
                             _plantilla.PiePaginaCenNegrita,  _plantilla.PiePaginaCenCursiva,
                             _plantilla.PiePaginaCenAlineacion),
                "PieDer" => (_plantilla.PiePaginaDerTipo,     _plantilla.PiePaginaDerContenido,
                             _plantilla.PiePaginaDerFuente,   _plantilla.PiePaginaDerTamaño,
                             _plantilla.PiePaginaDerNegrita,  _plantilla.PiePaginaDerCursiva,
                             _plantilla.PiePaginaDerAlineacion),
                _ => ("Texto", "", "Segoe UI", 9f, false, false, "Izquierda")
            };
        }

        private void AplicarCambiosZona()
        {
            if (_cargando) return;

            string tipo       = cboTipoZona.SelectedItem?.ToString() ?? "Texto";
            string contenido  = tipo == "Imagen" ? lblRutaImagen.Text : txtContenidoZona.Text;
            string fuente     = cboFuente.Text;
            float  tamaño     = (float)nudTamaño.Value;
            bool   negrita    = chkNegrita.Checked;
            bool   cursiva    = chkCursiva.Checked;
            string alineacion = cboAlineacion.SelectedItem?.ToString() ?? "Izquierda";

            switch (_zonaActual)
            {
                case "EncIzq":
                    _plantilla.EncabezadoIzqTipo=tipo; _plantilla.EncabezadoIzqContenido=contenido;
                    _plantilla.EncabezadoIzqFuente=fuente; _plantilla.EncabezadoIzqTamaño=tamaño;
                    _plantilla.EncabezadoIzqNegrita=negrita; _plantilla.EncabezadoIzqCursiva=cursiva;
                    _plantilla.EncabezadoIzqAlineacion=alineacion; break;
                case "EncCen":
                    _plantilla.EncabezadoCenTipo=tipo; _plantilla.EncabezadoCenContenido=contenido;
                    _plantilla.EncabezadoCenFuente=fuente; _plantilla.EncabezadoCenTamaño=tamaño;
                    _plantilla.EncabezadoCenNegrita=negrita; _plantilla.EncabezadoCenCursiva=cursiva;
                    _plantilla.EncabezadoCenAlineacion=alineacion; break;
                case "EncDer":
                    _plantilla.EncabezadoDerTipo=tipo; _plantilla.EncabezadoDerContenido=contenido;
                    _plantilla.EncabezadoDerFuente=fuente; _plantilla.EncabezadoDerTamaño=tamaño;
                    _plantilla.EncabezadoDerNegrita=negrita; _plantilla.EncabezadoDerCursiva=cursiva;
                    _plantilla.EncabezadoDerAlineacion=alineacion; break;
                case "PieIzq":
                    _plantilla.PiePaginaIzqTipo=tipo; _plantilla.PiePaginaIzqContenido=contenido;
                    _plantilla.PiePaginaIzqFuente=fuente; _plantilla.PiePaginaIzqTamaño=tamaño;
                    _plantilla.PiePaginaIzqNegrita=negrita; _plantilla.PiePaginaIzqCursiva=cursiva;
                    _plantilla.PiePaginaIzqAlineacion=alineacion; break;
                case "PieCen":
                    _plantilla.PiePaginaCenTipo=tipo; _plantilla.PiePaginaCenContenido=contenido;
                    _plantilla.PiePaginaCenFuente=fuente; _plantilla.PiePaginaCenTamaño=tamaño;
                    _plantilla.PiePaginaCenNegrita=negrita; _plantilla.PiePaginaCenCursiva=cursiva;
                    _plantilla.PiePaginaCenAlineacion=alineacion; break;
                case "PieDer":
                    _plantilla.PiePaginaDerTipo=tipo; _plantilla.PiePaginaDerContenido=contenido;
                    _plantilla.PiePaginaDerFuente=fuente; _plantilla.PiePaginaDerTamaño=tamaño;
                    _plantilla.PiePaginaDerNegrita=negrita; _plantilla.PiePaginaDerCursiva=cursiva;
                    _plantilla.PiePaginaDerAlineacion=alineacion; break;
            }
            ActualizarPreview();
        }

        private void ActualizarPreview()
        {
            // Preview encabezado
            panelPreviewEnc.Invalidate();
            panelPreviewPie.Invalidate();
        }

        // ── PREVIEW PAINT ────────────────────────────────────────────────────
        private void panelPreviewEnc_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
            => DibujarPreviewZonas(e.Graphics, panelPreviewEnc.Width, panelPreviewEnc.Height,
                _plantilla.EncabezadoIzqContenido, _plantilla.EncabezadoCenContenido, _plantilla.EncabezadoDerContenido,
                _plantilla.EncabezadoIzqTipo, _plantilla.EncabezadoCenTipo, _plantilla.EncabezadoDerTipo,
                _plantilla.EncabezadoIzqFuente, _plantilla.EncabezadoCenFuente, _plantilla.EncabezadoDerFuente,
                _plantilla.EncabezadoIzqTamaño, _plantilla.EncabezadoCenTamaño, _plantilla.EncabezadoDerTamaño,
                _plantilla.EncabezadoIzqNegrita, _plantilla.EncabezadoCenNegrita, _plantilla.EncabezadoDerNegrita,
                _plantilla.EncabezadoIzqCursiva, _plantilla.EncabezadoCenCursiva, _plantilla.EncabezadoDerCursiva);

        private void panelPreviewPie_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
            => DibujarPreviewZonas(e.Graphics, panelPreviewPie.Width, panelPreviewPie.Height,
                _plantilla.PiePaginaIzqContenido, _plantilla.PiePaginaCenContenido, _plantilla.PiePaginaDerContenido,
                _plantilla.PiePaginaIzqTipo, _plantilla.PiePaginaCenTipo, _plantilla.PiePaginaDerTipo,
                _plantilla.PiePaginaIzqFuente, _plantilla.PiePaginaCenFuente, _plantilla.PiePaginaDerFuente,
                _plantilla.PiePaginaIzqTamaño, _plantilla.PiePaginaCenTamaño, _plantilla.PiePaginaDerTamaño,
                _plantilla.PiePaginaIzqNegrita, _plantilla.PiePaginaCenNegrita, _plantilla.PiePaginaDerNegrita,
                _plantilla.PiePaginaIzqCursiva, _plantilla.PiePaginaCenCursiva, _plantilla.PiePaginaDerCursiva);

        private void DibujarPreviewZonas(Graphics g, int w, int h,
            string cIzq, string cCen, string cDer,
            string tIzq, string tCen, string tDer,
            string fIzq, string fCen, string fDer,
            float  szIzq,float  szCen,float  szDer,
            bool nIzq, bool nCen, bool nDer,
            bool cuIzq, bool cuCen, bool cuDer)
        {
            g.Clear(Color.White);
            g.DrawRectangle(Pens.LightGray, 0, 0, w - 1, h - 1);

            int zw = w / 3;

            // Dibujar divisores
            g.DrawLine(Pens.LightGray, zw,     0, zw,     h);
            g.DrawLine(Pens.LightGray, zw * 2, 0, zw * 2, h);

            void DibujarZona(string contenido, string tipo, string fuente, float sz,
                             bool neg, bool cur, int x, int ancho)
            {
                var estilo = (neg ? FontStyle.Bold : FontStyle.Regular) |
                             (cur ? FontStyle.Italic : FontStyle.Regular);
                try
                {
                    using var f = new Font(fuente, Math.Max(sz, 7f), estilo);
                    var texto = tipo == "Imagen" ? $"[Imagen: {System.IO.Path.GetFileName(contenido)}]"
                                                 : ResolverPreview(contenido);
                    var rect  = new RectangleF(x + 4, 4, ancho - 8, h - 8);
                    var sf    = new StringFormat { Alignment = StringAlignment.Center,
                                                   LineAlignment = StringAlignment.Center,
                                                   Trimming = StringTrimming.EllipsisCharacter };
                    g.DrawString(texto, f, Brushes.Black, rect, sf);
                }
                catch { }
            }

            DibujarZona(cIzq, tIzq, fIzq, szIzq, nIzq, cuIzq, 0,      zw);
            DibujarZona(cCen, tCen, fCen, szCen, nCen, cuCen, zw,     zw);
            DibujarZona(cDer, tDer, fDer, szDer, nDer, cuDer, zw * 2, w - zw * 2);
        }

        private string ResolverPreview(string texto)
        {
            // Sustitucion simplificada para el preview
            return _svc.ResolverCampos(texto, _proyecto, _plantilla)
                       .Replace("{pagina}", "1")
                       .Replace("{total_paginas}", "N");
        }

        // ── EVENTOS DE ZONA ──────────────────────────────────────────────────
        private void btnEncIzq_Click(object s, EventArgs e) { AplicarCambiosZona(); SeleccionarZona("EncIzq"); }
        private void btnEncCen_Click(object s, EventArgs e) { AplicarCambiosZona(); SeleccionarZona("EncCen"); }
        private void btnEncDer_Click(object s, EventArgs e) { AplicarCambiosZona(); SeleccionarZona("EncDer"); }
        private void btnPieIzq_Click(object s, EventArgs e) { AplicarCambiosZona(); SeleccionarZona("PieIzq"); }
        private void btnPieCen_Click(object s, EventArgs e) { AplicarCambiosZona(); SeleccionarZona("PieCen"); }
        private void btnPieDer_Click(object s, EventArgs e) { AplicarCambiosZona(); SeleccionarZona("PieDer"); }

        private void cboTipoZona_SelectedIndexChanged(object s, EventArgs e)
        {
            if (_cargando) return;
            panelTexto.Visible  = cboTipoZona.SelectedItem?.ToString() == "Texto";
            panelImagen.Visible = cboTipoZona.SelectedItem?.ToString() == "Imagen";
            AplicarCambiosZona();
        }

        private void txtContenidoZona_TextChanged(object s, EventArgs e) => AplicarCambiosZona();
        private void cboFuente_SelectedIndexChanged(object s, EventArgs e) => AplicarCambiosZona();
        private void nudTamaño_ValueChanged(object s, EventArgs e) => AplicarCambiosZona();
        private void chkNegrita_CheckedChanged(object s, EventArgs e) => AplicarCambiosZona();
        private void chkCursiva_CheckedChanged(object s, EventArgs e) => AplicarCambiosZona();
        private void cboAlineacion_SelectedIndexChanged(object s, EventArgs e) => AplicarCambiosZona();

        private void nudAltEncabezado_ValueChanged(object s, EventArgs e)
        {
            if (!_cargando) _plantilla.EncabezadoAltura = (int)nudAltEncabezado.Value;
        }
        private void nudAltPie_ValueChanged(object s, EventArgs e)
        {
            if (!_cargando) _plantilla.PiePaginaAltura = (int)nudAltPie.Value;
        }

        // ── INSERTAR CAMPO DINÁMICO ──────────────────────────────────────────
        private void btnInsertarCampo_Click(object s, EventArgs e)
        {
            if (lstCampos.SelectedItem == null) return;
            // Extraer el token {campo} de la descripción seleccionada
            string campo = ReporteService.CamposDisponibles
                .FirstOrDefault(kv => kv.Value == lstCampos.SelectedItem.ToString()).Key;
            if (string.IsNullOrEmpty(campo)) return;
            int pos = txtContenidoZona.SelectionStart;
            txtContenidoZona.Text = txtContenidoZona.Text.Insert(pos, campo);
            txtContenidoZona.SelectionStart = pos + campo.Length;
            txtContenidoZona.Focus();
        }

        // ── IMAGEN ───────────────────────────────────────────────────────────
        private void btnSeleccionarImagen_Click(object s, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "Seleccionar imagen",
                Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos|*.*"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                lblRutaImagen.Text = dlg.FileName;
                AplicarCambiosZona();
            }
        }

        // ── DATOS DINÁMICOS ──────────────────────────────────────────────────
        private void txtElabaro_TextChanged(object s, EventArgs e)
        { if (!_cargando) { _plantilla.CampoElabaro = txtElabaro.Text; ActualizarPreview(); } }
        private void txtReviso_TextChanged(object s, EventArgs e)
        { if (!_cargando) { _plantilla.CampoReviso = txtReviso.Text; ActualizarPreview(); } }
        private void txtAutorizo_TextChanged(object s, EventArgs e)
        { if (!_cargando) _plantilla.CampoAutorizo = txtAutorizo.Text; }
        private void txtDependencia_TextChanged(object s, EventArgs e)
        { if (!_cargando) { _plantilla.CampoDependencia = txtDependencia.Text; ActualizarPreview(); } }
        private void txtNumContrato_TextChanged(object s, EventArgs e)
        { if (!_cargando) _plantilla.CampoNumeroContrato = txtNumContrato.Text; }
        private void txtLicitacion_TextChanged(object s, EventArgs e)
        { if (!_cargando) _plantilla.CampoLicitacion = txtLicitacion.Text; }
        private void txtTextoLibre1_TextChanged(object s, EventArgs e)
        { if (!_cargando) { _plantilla.CampoTextoLibre1 = txtTextoLibre1.Text; ActualizarPreview(); } }
        private void txtTextoLibre2_TextChanged(object s, EventArgs e)
        { if (!_cargando) { _plantilla.CampoTextoLibre2 = txtTextoLibre2.Text; ActualizarPreview(); } }

        // ── GUARDAR / CERRAR ─────────────────────────────────────────────────
        private void btnGuardar_Click(object s, EventArgs e)
        {
            AplicarCambiosZona();
            _svc.GuardarPlantilla(_plantilla);
            _ucDisenador?.Guardar();
            MessageBox.Show("Plantilla guardada correctamente.", "SOPRO",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── TAB DISEÑADOR PDF ─────────────────────────────────────────────────
        private void TabConfig_SelectedIndexChanged(object s, EventArgs e)
        {
            if (tabConfig.SelectedTab == tabDisenadorPdf)
                InicializarDisenadorPdf();
        }

        private void InicializarDisenadorPdf()
        {
            // El UserControl ya trae Dock=Fill en su diseñador, pero se reafirma
            // aquí para evitar cambios de layout si se mueve de tab.
            if (_ucDisenador == null)
            {
                _ucDisenador = new UcDisenador(_ctx, _proyecto)
                {
                    Dock = DockStyle.Fill
                };
                tabDisenadorPdf.Controls.Add(_ucDisenador);
            }

            // No inicializar antes de que el tab tenga tamaño real.
            if (!IsHandleCreated || tabDisenadorPdf.ClientSize.Width <= 0)
                return;

            _ucDisenador.Inicializar();
        }

        private void btnCerrar_Click(object s, EventArgs e) => Close();
    }
}
