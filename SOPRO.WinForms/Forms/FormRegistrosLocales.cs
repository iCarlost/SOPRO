using SOPRO.Application.Services;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    internal sealed class FormRegistrosLocales : Form
    {
        private readonly DiagnosticsLogService _service = new();
        private readonly ListBox _files = new() { Dock = DockStyle.Fill, AccessibleName = "Archivos de registro" };
        private readonly TextBox _preview = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, AccessibleName = "Vista previa completa del archivo" };
        private readonly Label _summary = new() { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 8) };
        private readonly Label _status = new() { AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(0, 8, 0, 0), AccessibleRole = AccessibleRole.StatusBar };
        private readonly Button _export = new() { Text = "Exportar archivo…", AutoSize = true };
        private readonly Button _delete = new() { Text = "Borrar todos…", AutoSize = true };
        private readonly Button _refresh = new() { Text = "Actualizar", AutoSize = true };
        private DiagnosticsLogSnapshot? _snapshot;
        private CancellationTokenSource? _operationCancellation;
        private bool _busy;
        private bool _closeAfterOperation;

        public FormRegistrosLocales()
        {
            Text = "Registros locales"; StartPosition = FormStartPosition.CenterParent; MinimumSize = new Size(760, 480); Size = new Size(900, 600);
            FormClosing += (_, e) =>
            {
                if (!_busy) return;
                e.Cancel = true;
                _closeAfterOperation = true;
                _status.Text = "Cancelando operación…";
                _operationCancellation?.Cancel();
            };
            AccessibleDescription = "Revisión y exportación manual de registros de diagnóstico locales.";
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 250, FixedPanel = FixedPanel.Panel1 };
            var previewPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            previewPanel.Controls.Add(_preview);
            var listPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            listPanel.Controls.Add(_files);
            split.Panel1.Controls.Add(listPanel); split.Panel2.Controls.Add(previewPanel);
            var heading = new Label { Text = "Selecciona un archivo para ver su contenido completo. La exportación guarda exactamente el archivo seleccionado.", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            buttons.Controls.AddRange(new Control[] { _delete, _export, _refresh });
            Controls.Add(split); Controls.Add(buttons); Controls.Add(_status); Controls.Add(_summary); Controls.Add(heading);
            _files.SelectedIndexChanged += (_, _) => { if (_files.SelectedItem is DiagnosticsLogEntry entry) _preview.Text = entry.PreviewText; else _preview.Clear(); UpdateButtons(); };
            _files.Format += (_, e) => { if (e.ListItem is DiagnosticsLogEntry entry) e.Value = $"{entry.FileName}  ·  {entry.LastWriteTimeUtc.ToLocalTime():g}  ·  {entry.Length:N0} bytes"; };
            _refresh.Click += async (_, _) => await LoadSnapshotAsync();
            _export.Click += async (_, _) => await ExportSelectedAsync();
            _delete.Click += async (_, _) => await DeleteAsync();
            Shown += async (_, _) => await LoadSnapshotAsync();
        }

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
