using SOPRO.Application.Services;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Gestor de registros locales de diagnóstico. Code-only (sin diseñador),
    /// con el cromo compartido de dialogos SOPRO. No altera los flujos async,
    /// la semántica de operaciones ni los mensajes de estado.
    /// </summary>
    internal sealed class FormRegistrosLocales : Form
    {
        private readonly DiagnosticsLogService _service = new();
        private readonly ListBox _files = new() { Dock = DockStyle.Fill, AccessibleName = "Archivos de registro" };
        private readonly TextBox _preview = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, AccessibleName = "Vista previa completa del archivo" };
        private readonly Label _summary = new() { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 8) };
        private readonly Label _status = new() { AutoSize = false, Dock = DockStyle.Fill, AccessibleRole = AccessibleRole.StatusBar, AutoEllipsis = true };
        private readonly SoproButton _export;
        private readonly SoproButton _delete;
        private readonly SoproButton _refresh;
        private readonly Panel _pie;
        private DiagnosticsLogSnapshot? _snapshot;
        private CancellationTokenSource? _operationCancellation;
        private bool _busy;
        private bool _closeAfterOperation;

        public FormRegistrosLocales()
        {
            Text = "Registros locales"; StartPosition = FormStartPosition.CenterParent; MinimumSize = new Size(760, 480); Size = new Size(900, 600);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = SoproTheme.Colors.CenterBg;
            SoproDialogTheme.AplicarIconoSOPRO(this);
            FormClosing += (_, e) =>
            {
                if (!_busy) return;
                e.Cancel = true;
                _closeAfterOperation = true;
                _status.Text = "Cancelando operación…";
                _operationCancellation?.Cancel();
            };
            AccessibleDescription = "Revisión y exportación manual de registros de diagnóstico locales.";

            _export = SoproDialogTheme.CrearBotonSecundario("Exportar archivo…", SoproIconType.Copiar);
            _delete = SoproDialogTheme.CrearBotonSecundario("Borrar todos…", SoproIconType.Papelera);
            _refresh = SoproDialogTheme.CrearBotonSecundario("Actualizar", SoproIconType.Recalcular);

            var encabezado = SoproDialogTheme.CrearEncabezado(
                "Registros locales",
                "Revisión y exportación manual de registros de diagnóstico locales.");
            _pie = SoproDialogTheme.CrearPie();

            _status.ForeColor = SoproTheme.Colors.Muted;
            _status.Font = SoproTheme.Fonts.Subtitle;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Margin = new Padding(SoproDialogTheme.MargenHorizontalContenido, 0, 12, 0);

            // Pie con TableLayoutPanel: columna de estado (100%) y columna de
            // botones (AutoSize) en CELDAS DISJUNTAS. Sin superposiciones ni
            // recalculos manuales: la etiqueta nunca cubre a los botones y los
            // botones quedan siempre dentro del pie.
            var flujoBotones = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0),
                Padding = new Padding(0, 0, SoproDialogTheme.MargenDerechoPie, 0)
            };
            _refresh.Margin = new Padding(0, 0, SoproDialogTheme.SeparacionBotones, 0);
            _export.Margin = new Padding(0, 0, SoproDialogTheme.SeparacionBotones, 0);
            _delete.Margin = new Padding(0);
            flujoBotones.Controls.Add(_refresh);
            flujoBotones.Controls.Add(_export);
            flujoBotones.Controls.Add(_delete);

            var tablaPie = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = SoproTheme.Colors.HeaderBg,
                Margin = new Padding(0)
            };
            tablaPie.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tablaPie.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tablaPie.RowStyles.Add(new RowStyle(SizeType.Absolute, SoproDialogTheme.AltoPie));
            tablaPie.Controls.Add(_status, 0, 0);
            tablaPie.Controls.Add(flujoBotones, 1, 0);
            _pie.Controls.Add(tablaPie);

            // Tamaño explícito ANTES de SplitterDistance: sobre el tamaño por
            // defecto del SplitContainer (150px) la distancia se recortaba a
            // ~121px y Panel1 quedaba congelado en ese ancho (FixedPanel).
            var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(900, 400), SplitterDistance = 250, FixedPanel = FixedPanel.Panel1, BackColor = SoproTheme.Colors.GridLine };

            var tarjetaLista = SoproDialogTheme.CrearTarjeta();
            tarjetaLista.Controls.Add(_files);
            var listPanel = new Panel { Dock = DockStyle.Fill, Padding = SoproUiMetrics.PanelPadding, BackColor = SoproTheme.Colors.CenterBg };
            listPanel.Controls.Add(tarjetaLista);

            var tarjetaVista = SoproDialogTheme.CrearTarjeta();
            tarjetaVista.Controls.Add(_preview);
            var previewPanel = new Panel { Dock = DockStyle.Fill, Padding = SoproUiMetrics.PanelPadding, BackColor = SoproTheme.Colors.CenterBg };
            previewPanel.Controls.Add(tarjetaVista);
            split.Panel1.Controls.Add(listPanel); split.Panel2.Controls.Add(previewPanel);

            ConfigurarLista();

            var heading = new Label { Text = "Selecciona un archivo para ver su contenido completo. La exportación guarda exactamente el archivo seleccionado.", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(SoproDialogTheme.MargenHorizontalContenido, 14, SoproDialogTheme.MargenHorizontalContenido, 6), ForeColor = SoproTheme.Colors.Muted, Font = SoproTheme.Fonts.Subtitle };
            _summary.Padding = new Padding(SoproDialogTheme.MargenHorizontalContenido, 0, SoproDialogTheme.MargenHorizontalContenido, 8);
            _summary.ForeColor = SoproTheme.Colors.Ink2;
            _summary.Font = SoproTheme.Fonts.GridHeader;
            _summary.AccessibleName = "Resumen de registros";

            _files.SelectedIndexChanged += (_, _) => { if (_files.SelectedItem is DiagnosticsLogEntry entry) _preview.Text = entry.PreviewText; else _preview.Clear(); UpdateButtons(); };
            _refresh.Click += async (_, _) => await LoadSnapshotAsync();
            _export.Click += async (_, _) => await ExportSelectedAsync();
            _delete.Click += async (_, _) => await DeleteAsync();
            Shown += async (_, _) => await LoadSnapshotAsync();

            Controls.Add(split); Controls.Add(_pie); Controls.Add(_summary); Controls.Add(heading); Controls.Add(encabezado);
            UpdateButtons();
        }

        private void ConfigurarLista()
        {
            _files.BorderStyle = BorderStyle.None;
            _files.BackColor = SoproTheme.Colors.Surface;
            _files.ForeColor = SoproTheme.Colors.Ink2;
            _files.Font = SoproTheme.Fonts.GridCell;
            _files.IntegralHeight = false;
            _files.DrawMode = DrawMode.OwnerDrawFixed;
            _files.ItemHeight = SoproDialogTheme.CalcularAltoItemLista(_files);
            _files.FontChanged += (_, _) => _files.ItemHeight = SoproDialogTheme.CalcularAltoItemLista(_files);
            _files.DpiChangedAfterParent += (_, _) => _files.ItemHeight = SoproDialogTheme.CalcularAltoItemLista(_files);
            _files.DrawItem += DibujarItemLista;
        }

        private void DibujarItemLista(object? sender, DrawItemEventArgs e)
        {
            var graphics = e.Graphics;
            if (graphics == null || e.Index < 0 || e.Index >= _files.Items.Count) return;

            bool seleccionado = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color fondo = seleccionado
                ? SoproTheme.Colors.SelBg
                : (e.Index % 2 == 1 ? SoproTheme.Colors.RowAlt : SoproTheme.Colors.Surface);

            using (var brush = new SolidBrush(fondo))
                graphics.FillRectangle(brush, e.Bounds);

            using (var line = new Pen(SoproTheme.Colors.GridLine))
                graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right - 1, e.Bounds.Bottom - 1);

            if (seleccionado)
            {
                int anchoAcento = Math.Max(2, (int)Math.Round(3 * (_files.DeviceDpi / 96.0)));
                using var acento = new SolidBrush(SoproTheme.Colors.RibbonActive);
                graphics.FillRectangle(acento, new Rectangle(e.Bounds.Left, e.Bounds.Top, anchoAcento, e.Bounds.Height));
            }

            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus)
            {
                using var foco = new Pen(SoproTheme.Colors.BrandLt);
                graphics.DrawRectangle(foco, e.Bounds.Left + 1, e.Bounds.Top + 1, e.Bounds.Width - 3, e.Bounds.Height - 3);
            }

            if (_files.Items[e.Index] is not DiagnosticsLogEntry entrada) return;

            TextRenderer.DrawText(
                graphics,
                FormatearEntrada(entrada),
                _files.Font,
                new Rectangle(e.Bounds.Left + SoproDialogTheme.MargenLateralItemLista, e.Bounds.Top, Math.Max(1, e.Bounds.Width - SoproDialogTheme.MargenLateralItemLista - 4), e.Bounds.Height),
                seleccionado ? SoproTheme.Colors.Ink : SoproTheme.Colors.Ink2,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        /// <summary>Fila tal como se muestra en la lista (misma cadena que la vista previa de la lista nativa).</summary>
        internal static string FormatearEntrada(DiagnosticsLogEntry entrada)
            => $"{entrada.FileName}  ·  {entrada.LastWriteTimeUtc.ToLocalTime():g}  ·  {entrada.Length:N0} bytes";

        private async Task LoadSnapshotAsync()
        {
            var token = StartOperation("Cargando registros…");
            try
            {
                var result = await Task.Run(() => _service.CaptureSnapshot(token), token);
                if (IsDisposed || Disposing) return;
                _snapshot = result.Snapshot;
                _files.Items.Clear(); _preview.Clear();
                if (result.Succeeded && _snapshot is not null)
                {
                    foreach (var entry in _snapshot.Entries.OrderByDescending(e => e.LastWriteTimeUtc)) _files.Items.Add(entry);
                    _summary.Text = _snapshot.Entries.Count == 0 ? "No hay registros locales disponibles." : $"{_snapshot.Entries.Count} archivo(s) · {_snapshot.Entries.Sum(e => e.Length):N0} bytes";
                    _status.Text = "Los registros permanecen en este equipo y no se envían automáticamente.";
                }
                else { _summary.Text = "No se pudo preparar la vista de registros."; _status.Text = SafeMessage(result.Status); }
                UpdateButtons();
            }
            catch (OperationCanceledException) { if (!IsDisposed && !Disposing) _status.Text = "La operación se canceló."; }
            catch (Exception) { if (!IsDisposed && !Disposing) _status.Text = "No se pudieron cargar los registros. Inténtalo de nuevo."; }
            finally { FinishOperation(); }
        }

        private async Task ExportSelectedAsync()
        {
            if (_snapshot is null || _files.SelectedItem is not DiagnosticsLogEntry entry) return;
            using var dialog = new SaveFileDialog { Filter = "Registro de SOPRO (*.log)|*.log", FileName = entry.FileName, AddExtension = true, DefaultExt = "log", OverwritePrompt = false };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            var exists = System.IO.File.Exists(dialog.FileName);
            if (exists && MessageBox.Show(this, "Ya existe un archivo con ese nombre. ¿Quieres reemplazarlo?", "Confirmar reemplazo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            var snapshot = _snapshot;
            var path = dialog.FileName;
            var overwriteConfirmed = exists;
            var token = StartOperation("Guardando archivo…");
            try
            {
                var result = await Task.Run(() => _service.ExportEntry(snapshot, entry.Id, path, overwriteConfirmed, token), token);
                if (!IsDisposed && !Disposing) _status.Text = result.Succeeded ? "Archivo guardado." : SafeMessage(result.Status);
            }
            catch (OperationCanceledException) { if (!IsDisposed && !Disposing) _status.Text = "La operación se canceló."; }
            catch (Exception) { if (!IsDisposed && !Disposing) _status.Text = "No se pudo guardar el archivo. Elige otra ubicación o revisa los permisos."; }
            finally { FinishOperation(); }
        }

        private async Task DeleteAsync()
        {
            if (MessageBox.Show(this, "Se eliminarán los registros locales disponibles. Esta acción no se puede deshacer.", "¿Borrar todos los registros?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            var token = StartOperation("Borrando registros…");
            var partialDelete = false;
            try
            {
                var result = await Task.Run(() => _service.DeleteLogs(token), token);
                if (IsDisposed || Disposing) return;
                if (result.Succeeded) { _snapshot = null; _files.Items.Clear(); _preview.Clear(); _summary.Text = "No hay registros locales disponibles."; }
                if (result.Status == DiagnosticsOperationStatus.Partial)
                {
                    partialDelete = true;
                    _snapshot = null; _files.Items.Clear(); _preview.Clear();
                    _status.Text = "Algunos registros se borraron, pero no se pudieron eliminar todos. Actualizando la lista…";
                    var refreshed = await Task.Run(() => _service.CaptureSnapshot(token), token);
                    if (!IsDisposed && !Disposing)
                    {
                        if (refreshed.Succeeded && refreshed.Snapshot is not null)
                        {
                            _snapshot = refreshed.Snapshot;
                            foreach (var item in _snapshot.Entries.OrderByDescending(e => e.LastWriteTimeUtc)) _files.Items.Add(item);
                            _summary.Text = _snapshot.Entries.Count == 0 ? "No hay registros locales disponibles." : $"{_snapshot.Entries.Count} archivo(s) · {_snapshot.Entries.Sum(e => e.Length):N0} bytes";
                            _status.Text = "Algunos registros se borraron, pero no se pudieron eliminar todos. Se actualizó la lista.";
                        }
                        else _status.Text = "Algunos registros se borraron, pero no se pudieron eliminar todos.";
                    }
                }
                else _status.Text = result.Succeeded ? "Registros borrados." : SafeMessage(result.Status);
                UpdateButtons();
            }
            catch (OperationCanceledException) { if (!IsDisposed && !Disposing && !_closeAfterOperation) _status.Text = partialDelete ? "Algunos registros se borraron, pero no se pudieron eliminar todos." : "La operación se canceló."; }
            catch (Exception) { if (!IsDisposed && !Disposing && !_closeAfterOperation) _status.Text = partialDelete ? "Algunos registros se borraron, pero no se pudieron eliminar todos." : "No se pudieron borrar los registros. Inténtalo de nuevo."; }
            finally { FinishOperation(); }
        }

        private CancellationToken StartOperation(string message)
        {
            _operationCancellation?.Dispose();
            _operationCancellation = new CancellationTokenSource();
            _busy = true; _status.Text = message; _refresh.Enabled = _export.Enabled = _delete.Enabled = false; UseWaitCursor = true;
            return _operationCancellation.Token;
        }
        private void FinishOperation()
        {
            _operationCancellation?.Dispose(); _operationCancellation = null; _busy = false;
            if (IsDisposed || Disposing) return;
            UseWaitCursor = false; UpdateButtons(); _refresh.Enabled = true;
            if (_closeAfterOperation) { _closeAfterOperation = false; BeginInvoke(new Action(Close)); }
        }
        private void UpdateButtons() { _export.Enabled = _snapshot is not null && _files.SelectedItem is DiagnosticsLogEntry; _delete.Enabled = _snapshot is { Entries.Count: > 0 }; }
        internal static string SafeMessage(DiagnosticsOperationStatus status) => status switch
        {
            DiagnosticsOperationStatus.Busy => "Los registros están ocupados. Inténtalo de nuevo.",
            DiagnosticsOperationStatus.Cancelled => "La operación se canceló.",
            DiagnosticsOperationStatus.TooLarge => "Los registros superan el tamaño que se puede revisar.",
            DiagnosticsOperationStatus.InvalidData => "Un registro no se puede mostrar porque su contenido no es válido.",
            DiagnosticsOperationStatus.AlreadyExists => "El archivo de destino ya existe. Elige otro nombre.",
            DiagnosticsOperationStatus.InvalidDestination => "No se pudo usar el destino seleccionado.",
            DiagnosticsOperationStatus.NotFound => "El archivo seleccionado ya no está disponible.",
            DiagnosticsOperationStatus.Partial => "Algunos registros se borraron, pero no se pudieron eliminar todos.",
            _ => "No se pudo completar la operación. Inténtalo de nuevo."
        };
    }
}
