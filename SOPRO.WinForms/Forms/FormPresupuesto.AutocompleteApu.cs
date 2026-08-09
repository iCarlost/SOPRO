using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Autocompletado y preview de APU al editar la descripción en la grilla del presupuesto.
    /// </summary>
    public partial class FormPresupuesto
    {

        private void InicializarAutocompleteApu()
        {
            _lstApuAutocomplete = new ListBox
            {
                Name = "lstApuAutocomplete",
                Visible = false,
                IntegralHeight = true,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.FixedSingle,
                TabStop = true
            };

            _lstApuAutocomplete.MouseDoubleClick += (s, e) => ConfirmarSeleccionAutocompleteApu(true);
            _lstApuAutocomplete.MouseDown += LstApuAutocomplete_MouseDown;
            _lstApuAutocomplete.MouseUp += LstApuAutocomplete_MouseUp;
            _lstApuAutocomplete.MouseClick += LstApuAutocomplete_MouseClick;
            _lstApuAutocomplete.KeyDown += LstApuAutocomplete_KeyDown;
            _lstApuAutocomplete.Format += LstApuAutocomplete_Format;
            _lstApuAutocomplete.SelectedIndexChanged += (s, e) => ActualizarPreviewApuSeleccionada();

            Controls.Add(_lstApuAutocomplete);
            _lstApuAutocomplete.BringToFront();
        }

        private void InicializarPreviewApu()
        {
            _pnlApuPreview = new Panel
            {
                Name = "pnlApuPreview",
                Visible = false,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(255, 250, 225),
                Padding = new Padding(10)
            };

            _lblApuPreviewTitulo = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 9.75F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 42,
                TextAlign = ContentAlignment.TopLeft
            };

            _lblApuPreviewProyecto = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(70, 70, 70),
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _lblApuPreviewMeta = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                Dock = DockStyle.Top,
                Height = 42,
                TextAlign = ContentAlignment.TopLeft
            };

            _lblApuPreviewCosto = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 78, 121),
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _txtApuPreviewComponentes = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(255, 250, 225),
                Font = new Font("Consolas", 8.5F, FontStyle.Regular),
                TabStop = false
            };

            var lblComponentes = new Label
            {
                Text = "Componentes:",
                AutoSize = false,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _pnlApuPreview.Controls.Add(_txtApuPreviewComponentes);
            _pnlApuPreview.Controls.Add(lblComponentes);
            _pnlApuPreview.Controls.Add(_lblApuPreviewCosto);
            _pnlApuPreview.Controls.Add(_lblApuPreviewMeta);
            _pnlApuPreview.Controls.Add(_lblApuPreviewProyecto);
            _pnlApuPreview.Controls.Add(_lblApuPreviewTitulo);

            Controls.Add(_pnlApuPreview);
            _pnlApuPreview.BringToFront();
        }


        private void ActualizarPreviewApuSeleccionada()
        {
            if (_lstApuAutocomplete?.SelectedItem is not CatalogSearchResultDto item || _pnlApuPreview == null)
            {
                if (_pnlApuPreview != null)
                    _pnlApuPreview.Visible = false;
                return;
            }

            try
            {
                var preview = ObtenerPreviewApu(item);
                if (_lblApuPreviewTitulo != null) _lblApuPreviewTitulo.Text = preview.Titulo;
                if (_lblApuPreviewProyecto != null) _lblApuPreviewProyecto.Text = $"Proyecto: {preview.Proyecto}";
                if (_lblApuPreviewMeta != null) _lblApuPreviewMeta.Text = preview.Meta;
                if (_lblApuPreviewCosto != null) _lblApuPreviewCosto.Text = preview.Costo;
                if (_txtApuPreviewComponentes != null) _txtApuPreviewComponentes.Text = preview.Componentes;
                _pnlApuPreview.Visible = true;
                _pnlApuPreview.BringToFront();
                PosicionarPreviewApu();
            }
            catch
            {
                _pnlApuPreview.Visible = false;
            }
        }

        private MatrixPreviewInfo ObtenerPreviewApu(CatalogSearchResultDto item)
        {
            string key = $"{SelectorUiDefaults.NormalizePath(item.RutaProyecto)}|{item.ElementoId}";
            if (_cachePreviewApu.TryGetValue(key, out var cached))
                return cached;

            MatrixPreviewInfo preview = CargarPreviewApu(item);
            _cachePreviewApu[key] = preview;
            return preview;
        }

        private MatrixPreviewInfo CargarPreviewApu(CatalogSearchResultDto item)
        {
            if (item.EsActual || string.Equals(SelectorUiDefaults.NormalizePath(item.RutaProyecto), SelectorUiDefaults.NormalizePath(_context.DatabasePath), StringComparison.OrdinalIgnoreCase))
                return ConstruirPreviewApuDesdeContexto(_context, item, _proyecto.Nombre);

            using var externalContext = new SOPROContext(item.RutaProyecto);
            var nombreProyecto = externalContext.Proyectos.Select(p => p.Nombre).FirstOrDefault();
            return ConstruirPreviewApuDesdeContexto(externalContext, item, nombreProyecto ?? item.NombreProyecto);
        }

        private static MatrixPreviewInfo ConstruirPreviewApuDesdeContexto(SOPROContext context, CatalogSearchResultDto item, string? nombreProyecto)
        {
            var matriz = context.Matrices
                .AsNoTracking()
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .FirstOrDefault(m => m.Id == item.ElementoId && m.Tipo == TipoMatriz.APU);

            if (matriz == null)
            {
                return new MatrixPreviewInfo
                {
                    Titulo = string.IsNullOrWhiteSpace(item.Descripcion) ? item.Clave : item.Descripcion,
                    Proyecto = string.IsNullOrWhiteSpace(nombreProyecto) ? item.NombreProyecto : nombreProyecto,
                    Meta = $"Clave: {item.Clave}\r\nUnidad: {item.Unidad}",
                    Costo = $"Costo directo: {item.PrecioOCosto:C4}",
                    Componentes = string.Empty
                };
            }

            decimal totalMateriales = 0m, totalMo = 0m, totalHerr = 0m, totalMaq = 0m, totalAux = 0m;
            var lineas = new List<string>();

            foreach (var comp in matriz.Componentes.OrderBy(c => c.Orden).Take(8))
            {
                string tipo = comp.TipoComponente switch
                {
                    TipoComponenteMatriz.Material => "MAT",
                    TipoComponenteMatriz.ManoDeObra => "MO ",
                    TipoComponenteMatriz.Maquinaria => "MAQ",
                    TipoComponenteMatriz.Herramienta => "HER",
                    TipoComponenteMatriz.Auxiliar => "AUX",
                    _ => "---"
                };

                string descripcion = comp.TipoComponente switch
                {
                    TipoComponenteMatriz.Material => comp.Material?.Descripcion ?? "Material",
                    TipoComponenteMatriz.ManoDeObra => comp.ManoDeObra?.Descripcion ?? "Mano de obra",
                    TipoComponenteMatriz.Maquinaria => comp.Maquinaria?.Descripcion ?? "Maquinaria",
                    TipoComponenteMatriz.Herramienta => comp.Herramienta?.Descripcion ?? "Herramienta",
                    TipoComponenteMatriz.Auxiliar => comp.Auxiliar?.Descripcion ?? "Auxiliar",
                    _ => "Componente"
                };

                decimal importe = comp.Importe;
                switch (comp.TipoComponente)
                {
                    case TipoComponenteMatriz.Material: totalMateriales += importe; break;
                    case TipoComponenteMatriz.ManoDeObra: totalMo += importe; break;
                    case TipoComponenteMatriz.Maquinaria: totalMaq += importe; break;
                    case TipoComponenteMatriz.Herramienta: totalHerr += importe; break;
                    case TipoComponenteMatriz.Auxiliar: totalAux += importe; break;
                }

                lineas.Add($"{tipo}  {descripcion}");
                lineas.Add($"     Cant: {comp.Cantidad:N4}    Imp: {importe:C4}");
            }

            if (matriz.Componentes.Count > 8)
                lineas.Add($"... {matriz.Componentes.Count - 8} componente(s) más");

            string meta = $"Clave: {matriz.Clave}\r\nUnidad: {matriz.Unidad}   Tipo: {matriz.Tipo}";

            return new MatrixPreviewInfo
            {
                Titulo = string.IsNullOrWhiteSpace(matriz.Descripcion) ? matriz.Clave : matriz.Descripcion,
                Proyecto = string.IsNullOrWhiteSpace(nombreProyecto) ? item.NombreProyecto : nombreProyecto!,
                Meta = meta,
                Costo = $"Costo directo: {matriz.CostoDirecto:C4}",
                Componentes = string.Join(Environment.NewLine, lineas)
            };
        }

        private void PosicionarPreviewApu()
        {
            if (_lstApuAutocomplete is not { Visible: true } || _pnlApuPreview == null)
                return;

            const int separacion = 8;
            const int anchoPreferido = 420;
            const int margen = 6;

            int ancho = Math.Min(anchoPreferido, Math.Max(280, ClientSize.Width - (_lstApuAutocomplete.Width + separacion + (margen * 2))));
            int alto = Math.Max(230, _lstApuAutocomplete.Height + 70);
            int top = _lstApuAutocomplete.Top;

            int espacioDerecha = ClientSize.Width - (_lstApuAutocomplete.Right + separacion) - margen;
            int espacioIzquierda = _lstApuAutocomplete.Left - separacion - margen;
            int left;

            if (espacioDerecha >= 280 || espacioDerecha >= espacioIzquierda)
            {
                ancho = Math.Min(ancho, Math.Max(240, espacioDerecha));
                left = _lstApuAutocomplete.Right + separacion;
            }
            else
            {
                ancho = Math.Min(ancho, Math.Max(240, espacioIzquierda));
                left = Math.Max(margen, _lstApuAutocomplete.Left - separacion - ancho);
            }

            if (left + ancho > ClientSize.Width - margen)
                left = Math.Max(margen, ClientSize.Width - ancho - margen);
            if (top + alto > ClientSize.Height - margen)
                top = Math.Max(margen, ClientSize.Height - alto - margen);

            _pnlApuPreview.Left = left;
            _pnlApuPreview.Top = top;
            _pnlApuPreview.Width = ancho;
            _pnlApuPreview.Height = alto;
        }

        private void RefrescarFuenteAutocompleteApu()
        {
            _apuAutocompleteSource = new List<CatalogSearchResultDto>();
        }

        private void LstApuAutocomplete_Format(object? sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is CatalogSearchResultDto item)
            {
                var clave = string.IsNullOrWhiteSpace(item.Clave) ? string.Empty : item.Clave.Trim();
                var descripcion = string.IsNullOrWhiteSpace(item.Descripcion) ? string.Empty : item.Descripcion.Trim();
                var proyecto = item.EsActual ? "Actual" : item.NombreProyecto;
                var etiquetas = new List<string>();
                if (item.EsActual) etiquetas.Add("Actual");
                else
                {
                    if (item.EsFavorito) etiquetas.Add("Favorito");
                    else if (item.EsReciente) etiquetas.Add("Reciente");
                }

                string sufijo = etiquetas.Count == 0
                    ? $"[{proyecto}]"
                    : $"[{proyecto} · {string.Join(" · ", etiquetas)}]";

                e.Value = string.IsNullOrWhiteSpace(clave)
                    ? $"{descripcion}  {sufijo}"
                    : $"{clave} — {descripcion}  {sufijo}";
            }
        }

        private void LstApuAutocomplete_MouseDown(object? sender, MouseEventArgs e)
        {
            if (_lstApuAutocomplete == null) return;

            _mouseDownEnAutocomplete = true;
            _autocompleteUserNavigated = true;
            try { _lstApuAutocomplete.Focus(); } catch { }
            int index = _lstApuAutocomplete.IndexFromPoint(e.Location);
            if (index >= 0)
                _lstApuAutocomplete.SelectedIndex = index;
        }

        private void LstApuAutocomplete_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_lstApuAutocomplete == null)
            {
                _mouseDownEnAutocomplete = false;
                return;
            }

            int index = _lstApuAutocomplete.IndexFromPoint(e.Location);
            if (e.Button == MouseButtons.Left && index >= 0)
            {
                _lstApuAutocomplete.SelectedIndex = index;
                BeginInvoke(new Action(() =>
                {
                    _mouseDownEnAutocomplete = false;
                    ConfirmarSeleccionAutocompleteApu(true);
                }));
                return;
            }

            _mouseDownEnAutocomplete = false;
        }

        private void LstApuAutocomplete_MouseClick(object? sender, MouseEventArgs e)
        {
            if (_lstApuAutocomplete == null) return;

            int index = _lstApuAutocomplete.IndexFromPoint(e.Location);
            if (e.Button == MouseButtons.Left && index >= 0)
            {
                _autocompleteUserNavigated = true;
                _lstApuAutocomplete.SelectedIndex = index;
                BeginInvoke(new Action(() => ConfirmarSeleccionAutocompleteApu(true)));
            }
        }

        private void LstApuAutocomplete_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_lstApuAutocomplete == null) return;

            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                ConfirmarSeleccionAutocompleteApu(true);
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                OcultarAutocompleteApu(true);
            }
        }

        private void MostrarAutocompleteApuParaTexto(string texto)
        {
            if (_lstApuAutocomplete == null) return;
            if (_autocompleteRowIndex < 0 || _autocompleteColumnIndex < 0)
            {
                OcultarAutocompleteApu(false);
                return;
            }

            string filtro = NormalizarTextoAutocomplete(texto);
            if (string.IsNullOrWhiteSpace(filtro))
            {
                OcultarAutocompleteApu(false);
                return;
            }

            try
            {
                _apuAutocompleteSource = _catalogSearchService.SearchMatrices(
                    _context,
                    _proyecto.Id,
                    filtro,
                    includeCurrentProject: true,
                    includeRecentProjects: true,
                    includeFavoriteProjects: true,
                    includeAuxiliaries: false,
                    tipoFiltro: TipoMatriz.APU,
                    maxResults: 12)
                    .Where(x => x.TipoMatriz == TipoMatriz.APU)
                    .ToList();
            }
            catch
            {
                _apuAutocompleteSource = new List<CatalogSearchResultDto>();
            }

            if (_apuAutocompleteSource.Count == 0)
            {
                OcultarAutocompleteApu(false);
                return;
            }

            _autocompleteUserNavigated = false;
            _lstApuAutocomplete.BeginUpdate();
            _lstApuAutocomplete.DataSource = null;
            _lstApuAutocomplete.DataSource = _apuAutocompleteSource;
            _lstApuAutocomplete.DisplayMember = nameof(CatalogSearchResultDto.Descripcion);
            _lstApuAutocomplete.ClearSelected();
            _lstApuAutocomplete.SelectedIndex = -1;
            _lstApuAutocomplete.EndUpdate();

            PosicionarAutocompleteApu();
            _lstApuAutocomplete.Visible = true;
            _lstApuAutocomplete.BringToFront();
            ActualizarPreviewApuSeleccionada();
        }

        private static string NormalizarTextoAutocomplete(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            var normalized = texto.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
            var chars = normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
            return string.Join(' ', new string(chars).Normalize(System.Text.NormalizationForm.FormC)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static int CalcularScoreAutocomplete(string filtro, string clave, string descripcion)
        {
            int score = 0;

            if (!string.IsNullOrWhiteSpace(descripcion))
            {
                if (descripcion.Equals(filtro, StringComparison.OrdinalIgnoreCase)) score += 1200;
                else if (descripcion.StartsWith(filtro, StringComparison.OrdinalIgnoreCase)) score += 900;
                else if (descripcion.Contains(filtro, StringComparison.OrdinalIgnoreCase)) score += 650;
            }

            if (!string.IsNullOrWhiteSpace(clave))
            {
                if (clave.Equals(filtro, StringComparison.OrdinalIgnoreCase)) score += 1000;
                else if (clave.StartsWith(filtro, StringComparison.OrdinalIgnoreCase)) score += 800;
                else if (clave.Contains(filtro, StringComparison.OrdinalIgnoreCase)) score += 500;
            }

            foreach (var term in filtro.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (descripcion.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 40;
                if (clave.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 20;
            }

            return score;
        }

        private void PosicionarAutocompleteApu()
        {
            if (_lstApuAutocomplete == null || dgvPresupuesto.CurrentCell == null)
                return;

            AsegurarAnclaAutocompleteVisible();

            var rect = dgvPresupuesto.GetCellDisplayRectangle(_autocompleteColumnIndex, _autocompleteRowIndex, true);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                OcultarAutocompleteApu(false);
                return;
            }

            const int margenPantalla = 6;
            const int alturaMinima = 80;
            const int alturaMaxima = 220;
            const int anchoMinimo = 320;
            const int maxElementosVisibles = 8;

            var topLeft = PointToClient(dgvPresupuesto.PointToScreen(new Point(rect.Left, rect.Top)));
            var bottomLeft = PointToClient(dgvPresupuesto.PointToScreen(new Point(rect.Left, rect.Bottom)));

            int alturaDeseada = Math.Max(
                alturaMinima,
                Math.Min(
                    alturaMaxima,
                    (_lstApuAutocomplete.ItemHeight * Math.Max(1, Math.Min(_lstApuAutocomplete.Items.Count, maxElementosVisibles))) + 6));

            int espacioAbajo = ClientSize.Height - bottomLeft.Y - margenPantalla;
            int espacioArriba = topLeft.Y - margenPantalla;
            int alturaFinal;
            int topFinal;

            if (espacioAbajo >= alturaDeseada || espacioAbajo >= espacioArriba)
            {
                alturaFinal = Math.Max(Math.Min(alturaDeseada, Math.Max(espacioAbajo, alturaMinima)), Math.Min(alturaMinima, Math.Max(espacioAbajo, 0)));
                topFinal = bottomLeft.Y;
            }
            else
            {
                alturaFinal = Math.Max(Math.Min(alturaDeseada, Math.Max(espacioArriba, alturaMinima)), Math.Min(alturaMinima, Math.Max(espacioArriba, 0)));
                topFinal = topLeft.Y - alturaFinal;
            }

            if (alturaFinal <= 0)
                alturaFinal = Math.Min(alturaDeseada, Math.Max(alturaMinima, ClientSize.Height - (margenPantalla * 2)));

            int widthFinal = Math.Max(rect.Width, anchoMinimo);
            if (widthFinal > ClientSize.Width - (margenPantalla * 2))
                widthFinal = Math.Max(rect.Width, ClientSize.Width - (margenPantalla * 2));

            int leftFinal = topLeft.X;
            if (leftFinal + widthFinal > ClientSize.Width - margenPantalla)
                leftFinal = Math.Max(margenPantalla, ClientSize.Width - widthFinal - margenPantalla);
            if (leftFinal < margenPantalla)
                leftFinal = margenPantalla;

            if (topFinal + alturaFinal > ClientSize.Height - margenPantalla)
                topFinal = Math.Max(margenPantalla, ClientSize.Height - alturaFinal - margenPantalla);
            if (topFinal < margenPantalla)
                topFinal = margenPantalla;

            _lstApuAutocomplete.Left = leftFinal;
            _lstApuAutocomplete.Top = topFinal;
            _lstApuAutocomplete.Width = widthFinal;
            _lstApuAutocomplete.Height = alturaFinal;
            PosicionarPreviewApu();
        }

        private void AsegurarAnclaAutocompleteVisible()
        {
            if (_autocompleteRowIndex < 0 || _autocompleteRowIndex >= dgvPresupuesto.Rows.Count)
                return;

            var rect = dgvPresupuesto.GetCellDisplayRectangle(_autocompleteColumnIndex, _autocompleteRowIndex, true);
            if (rect.Height > 0 && rect.Bottom > 0 && rect.Top < dgvPresupuesto.ClientSize.Height)
                return;

            try
            {
                int rowObjetivo = Math.Max(0, _autocompleteRowIndex - 3);
                dgvPresupuesto.FirstDisplayedScrollingRowIndex = rowObjetivo;
            }
            catch
            {
                // Ignorar si el grid todavía no puede desplazar la fila.
            }
        }

        private void OcultarAutocompleteApu(bool focusEditor)
        {
            if (_lstApuAutocomplete != null)
            {
                _lstApuAutocomplete.Visible = false;
                _lstApuAutocomplete.DataSource = null;
            }
            _autocompleteUserNavigated = false;

            if (_pnlApuPreview != null)
                _pnlApuPreview.Visible = false;

            if (focusEditor && _txtDescripcionEnEdicion != null && !_txtDescripcionEnEdicion.IsDisposed && (_lstApuAutocomplete == null || !_lstApuAutocomplete.Visible))
            {
                _txtDescripcionEnEdicion.Focus();
                _txtDescripcionEnEdicion.SelectionStart = _txtDescripcionEnEdicion.TextLength;
                _txtDescripcionEnEdicion.SelectionLength = 0;
            }
        }

        private bool ConfirmarSeleccionAutocompleteApu(bool moveToNextRow)
        {
            if (_lstApuAutocomplete?.SelectedItem is not CatalogSearchResultDto selected)
                return false;

            _confirmandoSeleccionAutocomplete = true;
            try
            {
                int rowIndex = _autocompleteRowIndex;
                decimal cantidadActual = 1m;
            var cantidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Cantidad");
            if (cantidadCell?.Value != null)
                decimal.TryParse(cantidadCell.Value.ToString(), out cantidadActual);

            if (cantidadActual <= 0)
                cantidadActual = 1m;

                try
                {
                    if (dgvPresupuesto.IsCurrentCellInEditMode)
                        dgvPresupuesto.EndEdit();
                }
                catch { }

                Matriz? matriz = null;

            if (selected.EsActual || string.Equals(SelectorUiDefaults.NormalizePath(selected.RutaProyecto), SelectorUiDefaults.NormalizePath(_context.DatabasePath), StringComparison.OrdinalIgnoreCase))
            {
                matriz = _context.Matrices
                    .FirstOrDefault(m => m.ProyectoId == _proyecto.Id && m.Id == selected.ElementoId && m.Tipo == TipoMatriz.APU);
            }
            else
            {
                try
                {
                    ExternalMatrixImportConflictPolicy policy = ExternalMatrixImportConflictPolicy.KeepBothWithTempKey;
                    bool hayConflictoPorClave = _context.Matrices.Any(m => m.ProyectoId == _proyecto.Id && m.Clave == selected.Clave);

                    if (hayConflictoPorClave)
                    {
                        var preview = _externalMatrixImportService.BuildPreview(
                            _context, _proyecto.Id, selected.RutaProyecto, selected.ElementoId);
                        using var previewDialog = new FormPreviewImportacionMatrices(preview);
                        if (previewDialog.ShowDialog(this) != DialogResult.OK || previewDialog.SelectedPolicy == null)
                        {
                            OcultarAutocompleteApu(true);
                            return false;
                        }

                        policy = previewDialog.SelectedPolicy.Value;
                    }

                    var result = _externalMatrixImportService.ImportMatrixTree(
                        _context, _proyecto.Id, selected.RutaProyecto, selected.ElementoId, policy);

                    matriz = _context.Matrices.Find(result.RootMatrixId);
                    _projectIndexService.RegisterProjectOpened(selected.RutaProyecto, selected.NombreProyecto);
                    try { _catalogSearchService.RebuildProjectCatalogIndex(selected.RutaProyecto); } catch { }
                    _projectUsageService.RegisterMatrixSelection(selected.RutaProyecto, selected.ElementoId);
                    if (matriz != null)
                        _projectUsageService.RegisterMatrixSelection(_context.DatabasePath, matriz.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al importar la matriz externa: " + "\n" +
                        $"{ex.Message}",
                        "Importación", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    OcultarAutocompleteApu(true);
                    return false;
                }
            }

            if (matriz == null)
                return false;

                OcultarAutocompleteApu(false);
                AsignarMatrizAPresupuesto(
                    rowIndex,
                    matriz,
                    cantidadActual,
                    preserveCurrentTexts: false,
                    forceMatrixDescription: false,
                    preserveKeyIfPresent: true,
                    preserveUnitIfPresent: true,
                    preserveDescriptionIfPresent: true);
                _projectUsageService.RegisterMatrixSelection(_context.DatabasePath, matriz.Id);

                if (moveToNextRow)
                    PrepararSiguienteFilaConcepto(rowIndex + 1);

                return true;
            }
            finally
            {
                _confirmandoSeleccionAutocomplete = false;
                _mouseDownEnAutocomplete = false;
                _autocompleteUserNavigated = false;
            }
        }

        private void PrepararSiguienteFilaConcepto(int rowIndex)
        {
            if (rowIndex < 0) return;

            while (rowIndex >= dgvPresupuesto.Rows.Count)
                dgvPresupuesto.Rows.Add();

            var tipoCell = ObtenerCeldaPorNombreInterno(rowIndex, "Tipo");
            if (tipoCell != null && string.IsNullOrWhiteSpace(tipoCell.Value?.ToString()))
                tipoCell.Value = "Concepto";

            var descripcionCell = ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion");
            if (descripcionCell == null) return;

            dgvPresupuesto.CurrentCell = descripcionCell;
            dgvPresupuesto.BeginEdit(true);

            if (dgvPresupuesto.EditingControl is TextBox tb)
            {
                tb.SelectionStart = 0;
                tb.SelectionLength = tb.TextLength;
            }
        }
    }
}
