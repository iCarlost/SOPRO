using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Application.UseCases.Catalog;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Menú contextual: búsqueda de uso en matrices y configuración de columnas.
    /// </summary>
    public partial class FormCatalogoMateriales
    {

        private void DgvMateriales_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;

            var clickedRow = dgvMateriales.Rows[e.RowIndex];
            // Bajo CellSelect `clickedRow.Selected` es FALSO aunque haya celdas de esa
            // fila seleccionadas; con el helper detectamos si el clic cayó DENTRO de
            // la selección para no destruirla (regresión H7a).
            if (!DgvCeldaHelper.ClicDentroDeSeleccion(dgvMateriales, e.RowIndex))
            {
                dgvMateriales.ClearSelection();
                // Bajo CellSelect `clickedRow.Selected = true` es un no-op; hay que
                // seleccionar la celda clicada (o la fila completa si se pulsó el
                // encabezado) para que la copia del menú tenga contenido.
                if (e.ColumnIndex >= 0)
                    clickedRow.Cells[e.ColumnIndex].Selected = true;
                else
                    DgvCeldaHelper.SeleccionarFilaCompleta(dgvMateriales, e.RowIndex);
            }

            if (e.ColumnIndex >= 0)
                dgvMateriales.CurrentCell = clickedRow.Cells[e.ColumnIndex];

            _materialSeleccionado = clickedRow.DataBoundItem as MaterialListItem;
            if (_materialSeleccionado == null) return;

            // La elegibilidad se decide por alcance, no por Origen: ListMaterials
            // solo devuelve filas del proyecto de la sesión (ProyectoId = proyecto),
            // incluidas las copias importadas que conservan Origen = Maestro;
            // el Origen desactualizado no deshabilita Editar.
            bool puedeEditar = PuedeEditar(_materialSeleccionado);

            var menu = new ContextMenuStrip();
            var menuCts = new CancellationTokenSource();
            ReemplazarMenuContextual(menu, menuCts);

            var itemEditar = menu.Items.Add("✏️  Editar");
            itemEditar.Enabled = puedeEditar;
            itemEditar.Click += (_, __) => btnEditar_Click(sender, EventArgs.Empty);

            var idsEliminar = ObtenerIdsSeleccionados();
            var itemEliminar = menu.Items.Add($"🗑️  {CatalogoBatchDeletionHelper.EtiquetaEliminar(CatalogItemKind.Material, idsEliminar.Count)}");
            // La eliminación no se filtra por Origen: la elegibilidad real la decide
            // la previsualización con el ProyectoId de cada fila.
            itemEliminar.Enabled = idsEliminar.Count > 0;
            itemEliminar.Click += (_, __) => btnEliminar_Click(sender, EventArgs.Empty);

            menu.Items.Add(new ToolStripSeparator());

            var itemDonde = new ToolStripMenuItem("📋  Dónde se usa");
            // El submenú debe poder abrirse desde el primer clic: un DropDownItems
            // vacío no dispara DropDownOpening.
            AgregarPlaceholderConsultando(itemDonde);
            // El Id se captura al construir el menú: si la selección cambia
            // mientras la consulta está pendiente, no debe consultarse otra fila.
            int materialId = _materialSeleccionado.Id;
            int solicitud = 0;
            itemDonde.DropDownOpening += async (_, __) =>
            {
                int version = ++solicitud;
                await CargarDondeSeUsaAsync(
                    itemDonde, materialId, menuCts.Token, () => version == solicitud);
            };
            menu.Items.Add(itemDonde);
            menu.Items.Add(new ToolStripSeparator());

            // H8: copia tipo Excel (bloque exacto de celdas o fila[s] completa[s] por
            // encabezado). La etiqueta refleja la selección actual.
            string etiquetaCopiar = DgvCeldaHelper.EtiquetaCopiar(dgvMateriales);
            if (etiquetaCopiar != "Copiar celda")
            {
                var itemCopiarCelda = menu.Items.Add("📋  Copiar celda");
                itemCopiarCelda.Click += (_, __) =>
                {
                    var cell = dgvMateriales.CurrentCell;
                    if (cell != null)
                    {
                        string txt = cell.FormattedValue?.ToString() ?? "";
                        if (txt.Length > 0) Clipboard.SetText(txt);
                    }
                };
            }

            var itemCopiar = menu.Items.Add($"📄  {etiquetaCopiar}");
            itemCopiar.Click += (_, __) => DgvCeldaHelper.CopiarSeleccionAlPortapapeles(dgvMateriales);

            menu.Closed += (_, __) =>
            {
                if (ReferenceEquals(_menuContextual, menu))
                    menuCts.Cancel();
            };

            menu.Show(dgvMateriales, dgvMateriales.PointToClient(Cursor.Position));
        }

        private void ReemplazarMenuContextual(ContextMenuStrip menu, CancellationTokenSource cts)
        {
            var menuAnterior = _menuContextual;
            var ctsAnterior = _menuCts;
            _menuContextual = menu;
            _menuCts = cts;
            ctsAnterior?.Cancel();
            menuAnterior?.Dispose();
        }

        private void CerrarMenuContextual()
        {
            var menu = _menuContextual;
            var cts = _menuCts;
            _menuContextual = null;
            _menuCts = null;
            cts?.Cancel();
            menu?.Dispose();
        }

        /// <summary>
        /// Elegibilidad de edición del material del menú contextual: alcance de
        /// la sesión, no Origen (ListMaterials filtra por ProyectoId del
        /// proyecto; una copia importada conserva Origen = Maestro y es
        /// editable).
        /// </summary>
        internal bool PuedeEditar(MaterialListItem? material)
            => material != null;

        /// <summary>Texto seguro cuando la consulta del menú falla de forma inesperada.</summary>
        internal const string MensajeErrorDondeSeUsa = "No se pudo consultar dónde se usa el material.";

        /// <summary>Placeholder deshabilitado mientras la consulta está en curso.</summary>
        internal const string TextoConsultandoUsos = "(Consultando usos…)";

        /// <summary>
        /// "Dónde se usa": la consulta vive en el caso de uso headless
        /// (FindMatricesUsingMaterial) y devuelve DTOs, no entidades rastreadas.
        /// </summary>
        internal Task CargarDondeSeUsaAsync(
            ToolStripMenuItem itemDonde, int materialId, CancellationToken cancellationToken)
            => CargarDondeSeUsaAsync(itemDonde, materialId, cancellationToken, () => true);

        internal async Task CargarDondeSeUsaAsync(
            ToolStripMenuItem itemDonde,
            int materialId,
            CancellationToken cancellationToken,
            Func<bool> sigueVigente)
        {
            try
            {
                if (!PuedeActualizarItemDonde(itemDonde, cancellationToken, sigueVigente)) return;

                RestablecerPlaceholderConsultando(itemDonde);

                var result = await new FindMatricesUsingMaterial(_factory).ExecuteDetailed(
                    _sessionInfo, new FindMatricesUsingMaterialRequest(materialId), cancellationToken);

                if (!PuedeActualizarItemDonde(itemDonde, cancellationToken, sigueVigente)) return;

                if (!result.IsSuccess)
                {
                    itemDonde.DropDownItems.Clear();
                    AgregarItemDeshabilitado(itemDonde, result.Error!.Message);
                    return;
                }

                var detail = result.Value!;
                itemDonde.DropDownItems.Clear();
                var matricesMenu = new ToolStripMenuItem("Matrices");
                var machineryMenu = new ToolStripMenuItem("Maquinaria (costo horario)");
                itemDonde.DropDownItems.Add(matricesMenu);
                itemDonde.DropDownItems.Add(machineryMenu);
                if (detail.Matrices.Count == 0)
                {
                    var sinUso = matricesMenu.DropDownItems.Add("(Sin uso en matrices)");
                    sinUso.Enabled = false;
                }
                foreach (var uso in detail.Matrices)
                {
                    string label = uso.Clave + " — " + (uso.Descripcion?.Length > 50
                        ? uso.Descripcion[..50] + "…" : uso.Descripcion ?? "");
                    var item = matricesMenu.DropDownItems.Add(label);
                    int matrizId = uso.MatrizId;
                    item.Click += (_, __) =>
                    {
                        var matriz = _context.Matrices.Find(matrizId);
                        if (matriz != null)
                            AbrirEditorMatriz(matriz);
                    };
                }

                if (detail.MachineryUsages.Count == 0)
                {
                    var sinUso = machineryMenu.DropDownItems.Add("(Sin vínculo con maquinaria)");
                    sinUso.Enabled = false;
                }
                foreach (string label in CrearEtiquetasMaquinaria(detail.MachineryUsages))
                {
                    ToolStripItem item = machineryMenu.DropDownItems.Add(label);
                    item.Enabled = false;
                }
            }
            catch (OperationCanceledException)
            {
                // Cierre/reemplazo del menú o cancelación del formulario: silencio.
            }
            catch (Exception)
            {
                // El detalle técnico no se muestra en la UI: solo un ítem genérico.
                if (PuedeActualizarItemDonde(itemDonde, cancellationToken, sigueVigente))
                {
                    itemDonde.DropDownItems.Clear();
                    AgregarItemDeshabilitado(itemDonde, MensajeErrorDondeSeUsa);
                }
            }
        }

        private bool PuedeActualizarItemDonde(
            ToolStripMenuItem itemDonde, CancellationToken cancellationToken, Func<bool> sigueVigente)
        {
            if (cancellationToken.IsCancellationRequested || !sigueVigente()) return false;
            if (IsDisposed || Disposing) return false;
            if (itemDonde.IsDisposed) return false;
            return itemDonde.Owner is null || !itemDonde.Owner.IsDisposed;
        }

        private static void AgregarItemDeshabilitado(ToolStripMenuItem itemDonde, string texto)
        {
            ToolStripItem item = itemDonde.DropDownItems.Add(texto);
            item.Enabled = false;
        }

        private static void RestablecerPlaceholderConsultando(ToolStripMenuItem itemDonde)
        {
            itemDonde.DropDownItems.Clear();
            AgregarPlaceholderConsultando(itemDonde);
        }

        private static ToolStripItem AgregarPlaceholderConsultando(ToolStripMenuItem itemDonde)
        {
            ToolStripItem placeholder = itemDonde.DropDownItems.Add(TextoConsultandoUsos);
            placeholder.Enabled = false;
            return placeholder;
        }

        internal static IReadOnlyList<string> CrearEtiquetasMaquinaria(IEnumerable<MaterialMachineryUsage> usages)
        {
            return usages.Select(uso => $"{uso.Clave} — {uso.Descripcion} · {uso.Rol}").ToList();
        }

        private void AbrirEditorMatriz(Matriz matriz)
        {
            using var form = new FormEditarMatriz(_context, _proyectoId ?? 0, matriz);
            form.ShowDialog(this);
        }

        private static double CalcularAlturaFilaCatalogo(List<ColumnaMaterial> colsVis, int fila, IXLWorksheet ws, double alturaBase)
        {
            if (colsVis == null || colsVis.Count == 0) return alturaBase;

            double altura = alturaBase;
            for (int i = 0; i < colsVis.Count; i++)
            {
                var col = colsVis[i];
                if (!col.WrapTexto) continue;

                var valor = ws.Cell(fila, i + 1).GetFormattedString();
                if (string.IsNullOrWhiteSpace(valor)) continue;

                using var font = new Font(
                    string.IsNullOrWhiteSpace(col.NombreFuente) ? "Segoe UI" : col.NombreFuente,
                    Math.Max(8f, col.TamanoFuente > 0 ? col.TamanoFuente : 9f),
                    col.Negrita ? FontStyle.Bold : FontStyle.Regular);

                int anchoPx = Math.Max(24, (int)Math.Round(col.AnchoColumna - 8d));
                var proposed = new Size(anchoPx, int.MaxValue);
                var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
                var measured = TextRenderer.MeasureText(valor, font, proposed, flags);
                double alturaPts = Math.Max(alturaBase, measured.Height * 72.0 / 96.0 + 6);
                if (alturaPts > altura) altura = alturaPts;
            }

            return altura;
        }
    }
}
