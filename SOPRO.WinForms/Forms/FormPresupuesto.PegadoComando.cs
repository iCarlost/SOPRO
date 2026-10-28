using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>Clasificación del último pegado procesado por el formulario.</summary>
    internal enum BudgetPasteOutcomeKind
    {
        Rejected,
        Cancelled,
        NoChanges,
        Committed,
        Failed,
        OutcomeUnknown,
        CommittedViewNeedsReload
    }

    /// <summary>
    /// Resultado del pegado SIN diálogos: la capa de UI decide qué mostrar. Permite
    /// probar el flujo completo (incluida la cancelación del reemplazo duplicado) sin
    /// abrir MessageBox.
    /// </summary>
    internal sealed class BudgetPasteOutcome
    {
        private BudgetPasteOutcome(BudgetPasteOutcomeKind kind, string title, string message, int skippedCells)
        {
            Kind = kind;
            Title = title;
            Message = message;
            SkippedCells = skippedCells;
        }

        public BudgetPasteOutcomeKind Kind { get; }

        public string Title { get; }

        public string Message { get; }

        public int SkippedCells { get; }

        public static BudgetPasteOutcome Rejected(string message)
            => new(BudgetPasteOutcomeKind.Rejected, "Pegado no permitido", message ?? string.Empty, 0);

        public static BudgetPasteOutcome Cancelled(string message)
            => new(BudgetPasteOutcomeKind.Cancelled, "Pegado cancelado", message ?? string.Empty, 0);

        /// <summary>
        /// El bloque se procesó pero todas sus celdas se omitieron: no hay nada que
        /// escribir, así que no se intenta ningún comando. Se conserva el conteo de
        /// celdas omitidas para la notificación de pegado parcial.
        /// </summary>
        public static BudgetPasteOutcome NoChanges(int skippedCells)
            => new(BudgetPasteOutcomeKind.NoChanges, "Pegado parcial", string.Empty, skippedCells);

        public static BudgetPasteOutcome Failed(string message)
            => new(BudgetPasteOutcomeKind.Failed, "Pegado no confirmado", message ?? string.Empty, 0);

        /// <summary>
        /// El commit pudo haber ocurrido aunque la operación no se pueda confirmar. No se
        /// afirma que la base quedó sin cambios ni se reintenta automáticamente: el
        /// usuario debe inspeccionar, reabrir o conciliar antes de repetir el pegado.
        /// </summary>
        public static BudgetPasteOutcome OutcomeUnknown(string message)
            => new(BudgetPasteOutcomeKind.OutcomeUnknown, "Pegado sin confirmar", message ?? string.Empty, 0);

        /// <summary>
        /// El commit ya ocurrió (la base quedó guardada) pero la vista no se pudo
        /// actualizar de forma fiable: o se recargó desde la base, o se pide reapertura.
        /// Nunca afirma que la base quedó sin cambios.
        /// </summary>
        public static BudgetPasteOutcome CommittedViewNeedsReload(string message)
            => new(BudgetPasteOutcomeKind.CommittedViewNeedsReload, "Pegado guardado", message ?? string.Empty, 0);

        public static BudgetPasteOutcome Committed(int skippedCells)
            => new(BudgetPasteOutcomeKind.Committed, string.Empty, string.Empty, skippedCells);
    }

    /// <summary>
    /// Pegado como UNA operación inmutable (ADR-0001): el bloque se simula y normaliza
    /// contra el grid, la clave se resuelve (matriz APU, copia desde otra fila o edición
    /// simple) y la intención viaja completa a
    /// <see cref="BudgetPersistenceService.ExecutePasteCommand"/>. La vista NO muta
    /// celdas ni Tags antes de conocer el commit y solo aplica la instantánea confirmada,
    /// con los guards de reentrada y dejando las entidades <c>Unchanged</c>. No se
    /// invocan comandos por celda ni el autoguardado heredado.
    /// </summary>
    public partial class FormPresupuesto
    {
        private const string MensajePegadoResultadoIncierto =
            "No se pudo confirmar si el pegado quedó guardado en la base; el commit pudo haber ocurrido. " +
            "Inspeccione o vuelva a abrir el presupuesto para verificar el estado real y concilie con la base " +
            "antes de reintentar. No se reintentó automáticamente.";

        private const string MensajePegadoVistaRecargada =
            "El pegado quedó guardado en la base, pero la pantalla no se pudo actualizar directamente. " +
            "El presupuesto se recargó desde la base: verifique los valores mostrados antes de seguir.";

        private const string MensajePegadoVistaSinRecargar =
            "El pegado quedó guardado en la base, pero la pantalla no se actualizó y no se pudo recargar. " +
            "Cierre y vuelva a abrir el presupuesto para ver los datos reales antes de volver a pegar.";

        /// <summary>
        /// Conector de prueba: número de filas de la instantánea confirmada que SÍ se
        /// aplican a la vista antes de forzar el fallo (0 = sin fallo). Permite ejercitar
        /// la recuperación con una vista YA parcialmente aplicada, no en pre-aplicación.
        /// </summary>
        internal int PruebaFilasAplicadasAntesDeFallo { get; set; }

        /// <summary>
        /// Conector de prueba: fuerza el fallo del preflight de la recarga desde la base
        /// después de un commit, sin tocar la base ni mostrar diálogos.
        /// </summary>
        internal bool PruebaForzarFalloDeRecarga { get; set; }

        /// <summary>
        /// Conector de prueba: acción a ejecutar justo antes de lanzar el fallo simulado
        /// de la vista. Solo se invoca cuando la vista va a fallar; encapsulado para no
        /// exponer rutas de mutación fuera del escenario de recuperación.
        /// </summary>
        internal Action? PruebaAccionAntesDelFalloDeVista { get; set; }

        /// <summary>
        /// Cuando la vista quedó desincronizada respecto a la base y la recarga falló,
        /// se bloquean pegados posteriores hasta que el usuario reabra el presupuesto.
        /// </summary>
        private bool _pegadoBloqueadoPorVistaDesincronizada;

        /// <summary>
        /// Clasificación del último pegado ejecutado como comando, o <c>null</c> si no
        /// llegó a enviarse (plan inválido, fila no persistida, cancelación o trabajo
        /// pendiente en el contexto compartido). Observabilidad interna para pruebas.
        /// </summary>
        internal BudgetOperationStatus? UltimoEstadoPegado { get; private set; }

        /// <summary>
        /// Traduce el estado de un comando no confirmado a la clasificación de la UI.
        /// Cualquier estado distinto de conflicto/fallo se trata como incierto: nunca se
        /// afirma que la base quedó sin cambios cuando el commit pudo haber ocurrido.
        /// </summary>
        internal static BudgetPasteOutcome ClasificarResultadoPegado(
            BudgetOperationStatus status,
            string? message)
        {
            switch (status)
            {
                case BudgetOperationStatus.Committed:
                    return BudgetPasteOutcome.Committed(0);

                case BudgetOperationStatus.Conflict:
                case BudgetOperationStatus.Failed:
                    return BudgetPasteOutcome.Failed(
                        string.IsNullOrWhiteSpace(message)
                            ? "No se pudo guardar el pegado; no se modificó nada."
                            : message);

                default:
                    return BudgetPasteOutcome.OutcomeUnknown(MensajePegadoResultadoIncierto);
            }
        }

        private void HandleClipboardPaste()
        {
            var outcome = ProcesarPegadoPresupuesto(
                Clipboard.GetText(),
                mensaje => MessageBox.Show(
                    mensaje,
                    "Clave Duplicada - Confirmar Reemplazo",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes);

            if (outcome.Kind == BudgetPasteOutcomeKind.Committed
                || outcome.Kind == BudgetPasteOutcomeKind.NoChanges)
            {
                if (outcome.SkippedCells > 0)
                {
                    MessageBox.Show(
                        $"Se omitieron {outcome.SkippedCells} celdas por restricciones de edición o valores inválidos.",
                        "Pegado parcial",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(outcome.Message))
                return;

            MessageBox.Show(
                outcome.Message,
                outcome.Title,
                MessageBoxButtons.OK,
                outcome.Kind == BudgetPasteOutcomeKind.Failed
                    || outcome.Kind == BudgetPasteOutcomeKind.OutcomeUnknown
                    ? MessageBoxIcon.Warning
                    : MessageBoxIcon.Information);
        }

        /// <summary>
        /// Procesa un bloque de texto como pegado atómico. No muestra diálogos: la
        /// confirmación de reemplazo duplicado se delega en
        /// <paramref name="confirmarReemplazo"/> y el mensaje resultante se devuelve para
        /// que el llamador decida. Expuesto a las pruebas del ensamblado.
        /// </summary>
        internal BudgetPasteOutcome ProcesarPegadoPresupuesto(
            string? clipboardText,
            Func<string, bool>? confirmarReemplazo)
        {
            UltimoEstadoPegado = null;

            if (_pegadoBloqueadoPorVistaDesincronizada)
            {
                return BudgetPasteOutcome.Rejected(
                    "Hay un pegado anterior guardado en la base cuya vista no se pudo recargar. " +
                    "Cierre y vuelva a abrir el presupuesto antes de pegar de nuevo. No se modificó nada.");
            }

            if (dgvPresupuesto.CurrentCell == null)
                return BudgetPasteOutcome.Rejected(string.Empty);

            var currentColumn = dgvPresupuesto.Columns[dgvPresupuesto.CurrentCell.ColumnIndex];
            string? startInternalName = (currentColumn.Tag as ColumnaPersonalizada)?.NombreInterno;

            var plan = BudgetClipboardPasteService.BuildPlan(startInternalName, clipboardText);
            if (!plan.IsValid)
                return BudgetPasteOutcome.Rejected(plan.ErrorMessage);

            int startRowIndex = dgvPresupuesto.CurrentCell.RowIndex;
            int rowCount = plan.Rows.Max(r => r.RowOffset) + 1;

            // Toda la decisión se toma sobre instantáneas del grid ANTES de mutar nada:
            // destinos (Tipo/clave/cantidad/Tag) y filas de origen por Tag de solo lectura.
            var destinos = ConstruirDestinosPegado(startRowIndex, rowCount);
            var operationsPlan = BudgetClipboardPasteService.BuildOperations(plan, startRowIndex, destinos);
            if (!operationsPlan.IsValid)
                return BudgetPasteOutcome.Rejected(operationsPlan.ErrorMessage);

            // Todas las celdas del bloque se omitieron (restricción por tipo o valor
            // inválido): la ruta heredada no guardaba nada. No se construye un comando
            // vacío ni se toca el contexto; solo se conserva la notificación de omisión.
            if (operationsPlan.Operations.Count == 0)
                return BudgetPasteOutcome.NoChanges(operationsPlan.SkippedCells);

            // Conservador: si el contexto compartido arrastra trabajo pendiente de otras
            // rutas, no se envía el lote y tampoco se ejecuta ningún guardado heredado.
            _context.ChangeTracker.DetectChanges();
            if (_context.ChangeTracker.HasChanges())
            {
                return BudgetPasteOutcome.Rejected(
                    "Hay cambios pendientes sin guardar en el presupuesto. " +
                    "Guarde o recargue antes de pegar; no se modificó nada.");
            }

            var snapshots = BuildKeyRowSnapshotsFromTags();
            var rowCommands = new List<BudgetPasteRowCommand>(operationsPlan.Operations.Count);
            var confirmaciones = new List<string>();

            foreach (var operacion in operationsPlan.Operations)
            {
                var target = BudgetPasteKeyTarget.None;

                if (string.Equals(operacion.Tipo, "Concepto", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(operacion.Clave))
                {
                    var assignment = BudgetConceptAssignmentService.ResolveByKey(
                        _context,
                        _proyecto,
                        operacion.RowIndex,
                        operacion.Clave!,
                        operacion.Target.Concept,
                        operacion.Target.CurrentQuantityText,
                        snapshots);

                    if (assignment.IsInvalidMatrixTypeSelection)
                    {
                        return BudgetPasteOutcome.Rejected(
                            string.IsNullOrWhiteSpace(assignment.InvalidSelectionMessage)
                                ? "Este tipo de matriz no se puede asignar directamente a un concepto del presupuesto. " +
                                  "Seleccione una matriz de tipo APU. No se modificó nada."
                                : assignment.InvalidSelectionMessage);
                    }

                    if (assignment.HasAssignment && assignment.Draft is not null)
                    {
                        if (assignment.RequiresConfirmation)
                            confirmaciones.Add(assignment.ConfirmationMessage);

                        if (assignment.SourceRowIndex >= 0 && assignment.SourceConcept is { Id: > 0 } origen)
                            target = BudgetPasteKeyTarget.ForSourceConcept(
                                BudgetPasteSourceConceptSnapshot.FromConcepto(origen));
                        else if (assignment.Draft.MatrizId is int matrizId && matrizId > 0)
                            target = BudgetPasteKeyTarget.ForMatrix(matrizId);
                    }
                }

                rowCommands.Add(new BudgetPasteRowCommand(
                    operacion.Target.Concept!.Id,
                    BudgetConceptAssignmentBase.FromConcepto(operacion.Target.Concept!),
                    operacion.Clave,
                    operacion.Descripcion,
                    operacion.Unidad,
                    operacion.Cantidad,
                    target));
            }

            // Confirmación de reemplazo duplicado ANTES de escribir: cancelar deja el
            // lote completo sin cambios (no hay clave restaurada a medias).
            foreach (var mensaje in confirmaciones)
            {
                if (confirmarReemplazo is null || !confirmarReemplazo(mensaje))
                    return BudgetPasteOutcome.Cancelled("Pegado cancelado. No se modificó nada.");
            }

            BudgetPasteResult resultado;
            try
            {
                resultado = BudgetPersistenceService.ExecutePasteCommand(
                    _factory,
                    _context.DatabasePath,
                    new BudgetPasteCommand(
                        _proyecto.Id,
                        BudgetProjectCalculationBase.FromProyecto(_proyecto),
                        rowCommands));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Pegado: {ex.Message}");
                return BudgetPasteOutcome.Failed(
                    "No se pudo procesar el pegado; no se modificó nada. Puede reintentar.");
            }

            UltimoEstadoPegado = resultado.Status;

            if (!resultado.IsCommitted || resultado.Confirmed is null)
            {
                // Conflicto, fallo o resultado incierto: la vista conserva exactamente lo
                // que tenía (no se mutó ninguna celda antes del comando) y el tracker
                // compartido queda intacto. Un estado incierto NO autoriza a decir que no
                // se modificó nada: el commit pudo haber ocurrido.
                System.Diagnostics.Debug.WriteLine(
                    $"Pegado no confirmado ({resultado.Status}): {resultado.Diagnostic}");

                // Un "Committed" sin instantánea no permite reconciliar ni afirmar el
                // éxito; se trata como incierto en lugar de asumir que no hubo cambios.
                if (resultado.IsCommitted)
                    return BudgetPasteOutcome.OutcomeUnknown(MensajePegadoResultadoIncierto);

                return ClasificarResultadoPegado(resultado.Status, resultado.Message);
            }

            bool aplicadoEnVista = false;
            try
            {
                AplicarPegadoConfirmado(resultado.Confirmed);
                aplicadoEnVista = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Pegado confirmado pero la vista no aplicó: {ex.Message}");
            }

            if (aplicadoEnVista)
                return BudgetPasteOutcome.Committed(operationsPlan.SkippedCells);

            // El commit YA ocurrió: la base está guardada aunque la vista quede parcial.
            // Nunca se dice que no se modificó nada y nunca se reenvía el comando: solo
            // lectura de la base para reconstruir la vista.
            bool recargada = IntentarRecargaSeguraDesdeBase(
                resultado.Confirmed.Rows.Select(r => r.ConceptId)
                    .Concat(resultado.Confirmed.Aggregators.Select(a => a.ConceptId))
                    .Distinct()
                    .ToList());
            _pegadoBloqueadoPorVistaDesincronizada = !recargada;

            return BudgetPasteOutcome.CommittedViewNeedsReload(
                recargada ? MensajePegadoVistaRecargada : MensajePegadoVistaSinRecargar);
        }

        /// <summary>
        /// Reconstruye la vista desde la base sin escribir nada: primero se valida con
        /// una consulta de solo lectura y solo entonces se recarga el grid. Solo se
        /// relean las entidades rastreadas afectadas por el pegado confirmado
        /// (<paramref name="conceptIdsAfectados"/>): la edición en memoria de cualquier
        /// concepto ajeno al lote se preserva intacta. El contexto compartido queda sin
        /// trabajo pendiente porque <c>CargarPresupuesto</c> es solo de visualización.
        /// </summary>
        private bool IntentarRecargaSeguraDesdeBase(IReadOnlyCollection<int> conceptIdsAfectados)
        {
            var idsAfectados = new HashSet<int>(conceptIdsAfectados);

            try
            {
                if (PruebaForzarFalloDeRecarga)
                    throw new InvalidOperationException("Fallo simulado de la recarga (prueba).");

                // El comando confirmó por otro contexto: solo las entidades afectadas
                // por el lote quedaron desactualizadas en el contexto compartido. Se
                // relean esas (Reload solo consulta, no escribe); una entidad Modified
                // ajena al pegado conserva su edición en memoria y nunca se toca.
                foreach (var entrada in _context.ChangeTracker
                             .Entries<ConceptoPresupuesto>()
                             .ToList())
                {
                    if ((entrada.State == EntityState.Unchanged
                            || entrada.State == EntityState.Modified)
                        && idsAfectados.Contains(entrada.Entity.Id))
                    {
                        entrada.Reload();
                    }
                }

                BudgetLoadService.BuildLoadState(_context, _proyecto);
                return CargarPresupuestoCore();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Recarga tras pegado fallida: {ex.Message}");
                return false;
            }
        }

        private Dictionary<int, BudgetClipboardPasteTarget?> ConstruirDestinosPegado(
            int startRowIndex,
            int rowCount)
        {
            var destinos = new Dictionary<int, BudgetClipboardPasteTarget?>(rowCount);

            for (int i = 0; i < rowCount; i++)
            {
                int rowIndex = startRowIndex + i;
                if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count)
                {
                    destinos[rowIndex] = null;
                    continue;
                }

                destinos[rowIndex] = new BudgetClipboardPasteTarget(
                    rowIndex,
                    ObtenerCeldaTexto(rowIndex, "Tipo"),
                    ObtenerCeldaTexto(rowIndex, "Clave"),
                    ObtenerCeldaTexto(rowIndex, "Cantidad"),
                    dgvPresupuesto.Rows[rowIndex].Tag as ConceptoPresupuesto);
            }

            return destinos;
        }

        /// <summary>
        /// Filas de origen para resolver copias por clave, construidas SOLO desde los
        /// Tags persistidos (sin leer ni escribir borradores de celdas). Es una vista de
        /// solo lectura: la resolución no muta las entidades.
        /// </summary>
        private static List<BudgetConceptKeyRowSnapshot> BuildKeyRowSnapshotsFromTags(DataGridView grid)
        {
            var rows = new List<BudgetConceptKeyRowSnapshot>();

            for (int i = 0; i < grid.Rows.Count; i++)
            {
                if (grid.Rows[i].Tag is not ConceptoPresupuesto concepto || concepto.Id <= 0)
                    continue;

                rows.Add(new BudgetConceptKeyRowSnapshot
                {
                    RowIndex = i,
                    Key = concepto.Clave ?? string.Empty,
                    Description = concepto.Descripcion ?? string.Empty,
                    Concept = concepto
                });
            }

            return rows;
        }

        private List<BudgetConceptKeyRowSnapshot> BuildKeyRowSnapshotsFromTags()
            => BuildKeyRowSnapshotsFromTags(dgvPresupuesto);

        /// <summary>
        /// Vuelca la instantánea CONFIRMADA del lote: Tags, celdas propias y totales de
        /// agrupadores, todo bajo el guard de reentrada. Las entidades quedan
        /// <c>Unchanged</c> para que ningún guardado heredado posterior las reescriba.
        /// </summary>
        private void AplicarPegadoConfirmado(BudgetPasteSnapshot confirmado)
        {
            var filasPorConcepto = ConstruirIndiceFilasPorConcepto();
            bool actualizandoPrevio = _actualizandoDesdeComando;
            _actualizandoDesdeComando = true;

            try
            {
                int filasRestantesAntesDeFallo = PruebaFilasAplicadasAntesDeFallo;

                foreach (var fila in confirmado.Rows)
                {
                    if (!filasPorConcepto.TryGetValue(fila.ConceptId, out int rowIndex))
                        continue;

                    if (dgvPresupuesto.Rows[rowIndex].Tag is ConceptoPresupuesto entidad)
                        SincronizarEntidadPegado(entidad, fila);

                    var claveCell = ObtenerCeldaPorNombreInterno(rowIndex, "Clave");
                    if (claveCell != null)
                        claveCell.Value = fila.Clave;

                    if (fila.EsAgrupador)
                    {
                        var importeAgrupador = ObtenerCeldaPorNombreInterno(rowIndex, "Importe");
                        if (importeAgrupador != null)
                            importeAgrupador.Value = fila.ImporteTotal.ToStringImporte();
                        LanzarFalloSimuladoDeVistaSiProcede(ref filasRestantesAntesDeFallo);
                        continue;
                    }

                    var descCell = ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion");
                    if (descCell != null) descCell.Value = fila.Descripcion;

                    var unidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Unidad");
                    if (unidadCell != null) unidadCell.Value = fila.Unidad;

                    var cantidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Cantidad");
                    if (cantidadCell != null) cantidadCell.Value = fila.Cantidad;

                    var puCell = ObtenerCeldaPorNombreInterno(rowIndex, "PrecioUnitario");
                    if (puCell != null) puCell.Value = fila.PrecioUnitario.ToStringImporte();

                    var importeCell = ObtenerCeldaPorNombreInterno(rowIndex, "Importe");
                    if (importeCell != null) importeCell.Value = fila.ImporteTotal.ToStringImporte();

                    LanzarFalloSimuladoDeVistaSiProcede(ref filasRestantesAntesDeFallo);
                }

                foreach (var agrupador in confirmado.Aggregators)
                {
                    var entidad = ResolverEntidadCompartida(filasPorConcepto, agrupador.ConceptId);
                    if (entidad is not null)
                        SincronizarEntidadCompartida(entidad, agrupador);

                    if (!filasPorConcepto.TryGetValue(agrupador.ConceptId, out int filaAgrupador))
                        continue;

                    var importeCell = ObtenerCeldaPorNombreInterno(filaAgrupador, "Importe");
                    if (importeCell != null)
                        importeCell.Value = agrupador.ImporteTotal.ToStringImporte();
                }
            }
            finally
            {
                _actualizandoDesdeComando = actualizandoPrevio;
            }

            foreach (var fila in confirmado.Rows)
            {
                if (fila.EsAgrupador) continue;
                if (filasPorConcepto.TryGetValue(fila.ConceptId, out int rowIndex))
                    _panelMatricesEmbebido?.NotificarFilaCambiada(rowIndex);
            }

            ActualizarEstadisticas();
            dgvPresupuesto.Refresh();
        }

        /// <summary>
        /// Conector de prueba: tras aplicar una fila completa de la instantánea, fuerza el
        /// fallo de la vista una vez agotado el cupo de
        /// <see cref="PruebaFilasAplicadasAntesDeFallo"/> (0 = nunca lanza).
        /// </summary>
        private void LanzarFalloSimuladoDeVistaSiProcede(ref int filasRestantes)
        {
            if (filasRestantes <= 0)
                return;

            filasRestantes--;
            if (filasRestantes == 0)
            {
                PruebaAccionAntesDelFalloDeVista?.Invoke();
                throw new InvalidOperationException("Fallo simulado de la vista (prueba).");
            }
        }

        private void SincronizarEntidadPegado(ConceptoPresupuesto entidad, BudgetPasteRowSnapshot confirmado)
        {
            entidad.Clave = confirmado.Clave;
            entidad.Descripcion = confirmado.Descripcion;
            entidad.Unidad = confirmado.Unidad;
            entidad.Cantidad = confirmado.Cantidad;
            entidad.CostoDirectoUnitario = confirmado.CostoDirectoUnitario;
            entidad.CostoDirectoTotal = confirmado.CostoDirectoTotal;
            entidad.PrecioUnitario = confirmado.PrecioUnitario;
            entidad.ImporteTotal = confirmado.ImporteTotal;
            entidad.MatrizId = confirmado.MatrizId;
            entidad.FechaModificacion = confirmado.FechaModificacion;

            var entry = _context.Entry(entidad);
            if (entry.State == EntityState.Detached) return;

            entry.OriginalValues.SetValues(entry.CurrentValues);
            entry.State = EntityState.Unchanged;
        }
    }
}
