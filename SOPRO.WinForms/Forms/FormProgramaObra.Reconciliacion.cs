using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Reporting;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Ciclo de vida de las operaciones atómicas del programa: preflight de mutación,
    /// clasificación del resultado, suspensión del historial y publicación con lectura
    /// fresca de sólo lectura. La UI nunca escribe a través del contexto compartido
    /// después de un comando: relee desde un contexto NUEVO.
    /// </summary>
    public partial class FormProgramaObra
    {
        /// <summary>
        /// La vista está bloqueada para CUALQUIER mutación del programa: un resultado
        /// incierto de programa o de formato exige una lectura fresca completa antes de
        /// volver a operar. No se apoya sólo en los stacks de undo.
        /// </summary>
        internal bool EstaBloqueadoParaOperar
            => _programaOperacionIncierta || _requiereLecturaFresca || _undoManager.RequiresReconciliation;

        /// <summary>
        /// Seam de pruebas: el contexto compartido no tiene cambios pendientes. Un
        /// handler bloqueado no debe dejar entidades modificadas sin guardar.
        /// </summary>
        internal bool HayCambiosPendientesEnContexto => _context.ChangeTracker.HasChanges();

        /// <summary>
        /// Bloqueo común de mutación. Devuelve el motivo por el que no se puede operar o
        /// null si la operación es segura. No confirma ni descarta escrituras ajenas: si
        /// el contexto compartido tiene cambios pendientes, la operación se rechaza.
        /// </summary>
        private string? ObtenerBloqueoMutacionPrograma()
        {
            if (_programaOperacionIncierta)
                return "No se pudo confirmar la última operación del programa. Presione «Recargar» antes de continuar.";

            if (_requiereLecturaFresca)
                return "La vista no refleja la última operación ya guardada. Presione «Recargar» antes de continuar.";

            if (_undoManager.RequiresReconciliation)
                return "El formato quedó en estado incierto. Presione «Recargar» antes de continuar.";

            if (_context.ChangeTracker.HasChanges())
                return "Hay cambios sin guardar en esta ventana. Guárdelos o descártelos antes de operar el programa.";

            return null;
        }

        /// <summary>Preflight común: informa el motivo y devuelve false si hay bloqueo.</summary>
        private bool PuedeMutarPrograma(string accion)
        {
            var bloqueo = ObtenerBloqueoMutacionPrograma();
            if (bloqueo is null)
                return true;

            lblEstado.Text = bloqueo;
            Notificador.Advertir($"No se puede {accion}. {bloqueo}");
            ActualizarHabilitacionPrograma();
            return false;
        }

        /// <summary>
        /// Cancela la edición de grillas mientras el programa está bloqueado. Se usa en
        /// CellBeginEdit/validating para que el bloqueo no dependa de Undo.
        /// </summary>
        internal bool BloqueaEdicionPrograma()
        {
            if (_ocupado || _cargando)
            {
                lblEstado.Text = _ocupado
                    ? "Hay una operación del programa en curso. Espere a que termine antes de editar otra celda."
                    : "La vista del programa se está actualizando. Espere antes de editar otra celda.";
                return true;
            }

            if (!EstaBloqueadoParaOperar)
                return false;

            lblEstado.Text = "El programa está bloqueado hasta recargar desde la base. Presione «Recargar».";
            return true;
        }

        /// <summary>
        /// Habilita/deshabilita TODOS los disparadores de mutación del formulario según el
        /// bloqueo y el estado ocupado. «Recargar» permanece siempre disponible: es la
        /// salida de reconciliación.
        /// </summary>
        private void ActualizarHabilitacionPrograma()
        {
            var bloqueado = EstaBloqueadoParaOperar;
            var habilitado = !_ocupado && !bloqueado;

            btnRecalcular.Enabled = habilitado;
            btnSincronizar.Enabled = habilitado;
            btnReconstruir.Enabled = habilitado;
            btnBorrarPrograma.Enabled = habilitado;
            btnCalendario.Enabled = habilitado;
            cmbTipoPeriodo.Enabled = habilitado;

            // Mientras corre una operación asíncrona no se edita NINGUNA celda: la
            // celda editada y el grid quedarían con datos que la recarga va a reemplazar.
            var edicionBloqueada = bloqueado || _ocupado;
            dgvActividades.ReadOnly = edicionBloqueada;
            dgvDependencias.ReadOnly = edicionBloqueada;
            dgvDependencias.AllowUserToAddRows = !edicionBloqueada;
            dgvDependencias.AllowUserToDeleteRows = !edicionBloqueada;
        }

        /// <summary>
        /// Clasifica el resultado de un comando atómico. Sólo un commit CON cambios
        /// autoriza a descartar historial y recargar; un no-op confirmado no invalida
        /// nada. Failed/Conflict no recargan ni mutan cachés. OutcomeUnknown suspende
        /// undo y operaciones de programa sin reintentar ni recargar por su cuenta.
        /// </summary>
        /// <returns>true si la vista debe publicar una lectura fresca.</returns>
        private bool ProcesarResultadoPrograma(ProgramSyncResult result, bool descartarFormato)
        {
            lblEstado.Text = result.Message;

            switch (result.Status)
            {
                case BudgetOperationStatus.Committed:
                    if (result.SinCambios)
                        return false;

                    if (descartarFormato)
                    {
                        InvalidarCachesFormatoProgramaObra();
                        _undoManager.DescartarPorFamilia(UndoFamily.Formato);
                    }

                    _undoManager.DescartarPorFamilia(UndoFamily.Programa);
                    return true;

                case BudgetOperationStatus.OutcomeUnknown:
                    SuspenderPorResultadoIncierto(result.Message);
                    return false;

                case BudgetOperationStatus.Conflict:
                    Notificador.Advertir(result.Message);
                    return false;

                default:
                    Notificador.Error(result.Message);
                    return false;
            }
        }

        /// <summary>
        /// Suspende undo/redo y toda operación de programa/mutación hasta una lectura
        /// fresca completa. No revierte, no reintenta y no descarta historial.
        /// </summary>
        private void SuspenderPorResultadoIncierto(string mensaje)
        {
            _programaOperacionIncierta = true;
            _requiereLecturaFresca = false;
            _undoManager.SuspendForReconciliation();
            ActualizarHabilitacionPrograma();
            lblEstado.Text = "El programa quedó en estado incierto. Presione «Recargar» para reconciliar.";
            Notificador.Advertir(
                $"{mensaje} No se pudo confirmar el resultado. Presione «Recargar» antes de continuar.");
        }

        /// <summary>
        /// Todo lo que la publicación necesita de la base, leído en UNA sola fase con
        /// contexto FRESCO de sólo lectura ANTES de tocar la UI: filas de Curva S, modelo
        /// del Gantt de la vista y plazo en días hábiles. Si cualquiera de esas lecturas
        /// falla no se publica nada (estado previo + bloqueo intactos).
        /// </summary>
        internal sealed class PublicacionProgramaSnapshot
        {
            public IReadOnlyList<CurvaSRowDto> CurvaS { get; init; } = Array.Empty<CurvaSRowDto>();

            public GanttRenderModel? Gantt { get; init; }

            public int? PlazoDiasHabiles { get; init; }
        }

        /// <summary>
        /// Construye el snapshot de publicación leyendo SÓLO a través del contexto
        /// recibido (nunca el contexto compartido de la vista). Se invoca dentro de la
        /// fase de lectura fresca; cualquier excepción la maneja el llamador, que deja
        /// la vista bloqueada.
        /// </summary>
        private PublicacionProgramaSnapshot PrepararPublicacionPrograma(ProgramLoadDto? programa, SOPROContext contexto)
        {
            if (programa is null)
                return new PublicacionProgramaSnapshot();

            var vista = ObtenerVistaCurvaSeleccionada();
            var tipoPeriodo = programa.TipoPeriodo;

            return new PublicacionProgramaSnapshot
            {
                CurvaS = _curvaSService.BuildFinancialCurve(contexto, programa.ProgramaObraId),
                Gantt = vista switch
                {
                    CurvaSViewMode.Financiera => _ganttService.BuildFinancialGantt(contexto, programa, tipoPeriodo),
                    CurvaSViewMode.Ambas => _ganttService.BuildFinancialGantt(contexto, programa, tipoPeriodo, mixedMode: true),
                    _ => _ganttService.BuildProgramGantt(programa, tipoPeriodo, GanttViewMode.ProgramaObra)
                },
                PlazoDiasHabiles = CalcularPlazoDiasHabiles(programa, contexto)
            };
        }

        /// <summary>Plazo total en días hábiles del programa leído (o null sin fechas).</summary>
        private int? CalcularPlazoDiasHabiles(ProgramLoadDto programa, SOPROContext contexto)
        {
            var inicio = programa.Actividades
                .Where(a => !a.EsResumen && a.FechaInicioProgramada.HasValue)
                .Select(a => a.FechaInicioProgramada!.Value.Date)
                .DefaultIfEmpty()
                .Min();
            var fin = programa.Actividades
                .Where(a => !a.EsResumen && a.FechaFinProgramada.HasValue)
                .Select(a => a.FechaFinProgramada!.Value.Date)
                .DefaultIfEmpty()
                .Max();

            if (inicio == default || fin == default)
                return null;

            return _calculationService.CalculateBusinessDaysInclusive(contexto, programa.ProgramaObraId, inicio, fin);
        }

        /// <summary>
        /// Publica una lectura FRESCA de sólo lectura (contexto NUEVO) después de un
        /// comando confirmado. No recalcula ni guarda: la operación atómica ya dejó la
        /// base consistente. TODAS las lecturas (programa, Curva S, Gantt, plazo) ocurren
        /// antes de cambiar estado de UI y sobre el contexto fresco; si cualquiera falla
        /// la vista queda BLOQUEADA (cachés/grid obsoletos) hasta que «Recargar» obtenga
        /// una lectura completa.
        /// <paramref name="escrituraConfirmada"/> clasifica el fallo: un write confirmado
        /// (commit) se informa como «se guardó»; un write rechazado (Ok=false, nada se
        /// escribió) NO puede afirmar que se guardó. En ambos casos se conserva el lock.
        /// </summary>
        private bool RecargarProgramaDesdeContextoFresco(
            DataGridViewStateSnapshot? stateToRestore = null,
            bool escrituraConfirmada = true)
        {
            if (EstaBloqueadoParaOperar)
                return false;

            ProgramLoadDto? programa;
            PublicacionProgramaSnapshot publicacion;
            UltimoErrorLecturaFresca = null;
            try
            {
                using var contextoFresco = _factory.Create(_context.DatabasePath);
                programa = LeerProgramaParaPublicacion(contextoFresco);
                publicacion = PrepararSnapshotParaPublicacion(programa, contextoFresco);
            }
            catch (Exception ex)
            {
                // Si la lectura del programa o el snapshot ya registraron la causa, se
                // conserva ese detalle (fase exacta); si no, el fallo fue al abrir el
                // contexto de lectura.
                var detalle = UltimoErrorLecturaFresca
                    ?? RegistrarErrorLecturaFresca("apertura del contexto de lectura", ex);

                // Un write confirmado NO es un OutcomeUnknown de persistencia; uno
                // rechazado (Ok=false) no escribió nada. En ambos casos la vista no puede
                // operar con cachés que no reflejan la base hasta «Recargar».
                BloquearPorPublicacionFallida(
                    escrituraConfirmada
                        ? $"La operación se guardó, pero la vista no se pudo actualizar. Presione «Recargar». Detalle: {detalle}"
                        : $"La operación fue rechazada y la vista no se pudo actualizar. Presione «Recargar». Detalle: {detalle}",
                    escrituraConfirmada
                        ? $"La operación se guardó, pero no se pudo releer el programa. La vista quedó bloqueada: presione «Recargar» antes de continuar. Detalle: {detalle}"
                        : $"La operación fue rechazada (nada se escribió) y no se pudo refrescar la vista. La vista quedó bloqueada: presione «Recargar» antes de continuar. Detalle: {detalle}");
                return false;
            }

            if (!PublicarProgramaDesdeSnapshot(programa, publicacion, stateToRestore, escrituraConfirmada))
                return false;

            ActualizarHabilitacionPrograma();
            return true;
        }

        /// <summary>
        /// Recarga verificada tras una edición del programa (actividad, dependencia o
        /// undo/redo): publica una lectura FRESCA de sólo lectura sobre un contexto
        /// nuevo y devuelve true SÓLO si la vista quedó publicada sin bloqueo. Con la
        /// vista ya bloqueada reconcilia programa + formato. Cualquier fallo deja la
        /// vista bloqueada e informada; nunca propaga la excepción al bucle de UI.
        /// <paramref name="escrituraConfirmada"/> evita afirmar «se guardó» cuando el
        /// write fue rechazado (Ok=false).
        /// </summary>
        private bool RecargarProgramaVerificado(
            DataGridViewStateSnapshot? stateToRestore = null,
            bool escrituraConfirmada = true)
        {
            if (IsDisposed)
                return false;

            try
            {
                CargarPrograma(stateToRestore, escrituraConfirmada);
                return !EstaBloqueadoParaOperar;
            }
            catch (Exception ex)
            {
                var detalle = RegistrarErrorLecturaFresca("RecargarProgramaVerificado", ex);
                _requiereLecturaFresca = true;
                _undoManager.SuspendForReconciliation();
                ActualizarHabilitacionPrograma();
                lblEstado.Text = escrituraConfirmada
                    ? $"La operación se guardó, pero la vista no se pudo actualizar. Presione «Recargar». Detalle: {detalle}"
                    : $"La operación fue rechazada y la vista no se pudo actualizar. Presione «Recargar». Detalle: {detalle}";
                return false;
            }
        }

        /// <summary>
        /// Lectura del programa con la fase identificada: si falla, registra si el error
        /// fue al leer el programa (no al construir Curva S/Gantt). El commit ya está
        /// confirmado, así que el diagnóstico debe distinguir la fase exacta.
        /// </summary>
        private ProgramLoadDto? LeerProgramaParaPublicacion(SOPROContext contextoFresco)
        {
            try
            {
                return _loadService.LoadProgramReadOnly(contextoFresco, _proyecto.Id);
            }
            catch (Exception ex)
            {
                RegistrarErrorLecturaFresca("lectura del programa", ex);
                throw;
            }
        }

        /// <summary>Snapshot de publicación (Curva S, Gantt, plazo) con fase identificada.</summary>
        private PublicacionProgramaSnapshot PrepararSnapshotParaPublicacion(
            ProgramLoadDto? programa,
            SOPROContext contextoFresco)
        {
            try
            {
                return PrepararPublicacionPrograma(programa, contextoFresco);
            }
            catch (Exception ex)
            {
                RegistrarErrorLecturaFresca("snapshot de publicación (Curva S/Gantt/plazo)", ex);
                throw;
            }
        }

        /// <summary>
        /// Aplica en la UI el snapshot ya leído. Si la publicación falla, la vista queda
        /// marcada para lectura fresca (nunca se libera el bloqueo con caches parciales).
        /// <paramref name="escrituraConfirmada"/> evita afirmar «se guardó» si el write
        /// fue rechazado.
        /// </summary>
        private bool PublicarProgramaDesdeSnapshot(
            ProgramLoadDto? programa,
            PublicacionProgramaSnapshot publicacion,
            DataGridViewStateSnapshot? stateToRestore,
            bool escrituraConfirmada = true)
        {
            try
            {
                AplicarProgramaEnUi(programa, stateToRestore, publicacion);
                return true;
            }
            catch (Exception ex)
            {
                var detalle = RegistrarErrorLecturaFresca("PublicarProgramaDesdeSnapshot", ex);
                BloquearPorPublicacionFallida(
                    escrituraConfirmada
                        ? $"La vista no se pudo actualizar. Presione «Recargar» antes de continuar. Detalle: {detalle}"
                        : $"La operación fue rechazada y la vista no se pudo actualizar. Presione «Recargar» antes de continuar. Detalle: {detalle}",
                    escrituraConfirmada
                        ? $"No se pudo publicar la vista del programa. La vista quedó bloqueada: presione «Recargar» antes de continuar. Detalle: {detalle}"
                        : $"La operación fue rechazada (nada se escribió) y no se pudo publicar la vista del programa. La vista quedó bloqueada: presione «Recargar» antes de continuar. Detalle: {detalle}");
                return false;
            }
        }

        /// <summary>
        /// Registra el error de la última lectura/publicación fresca fallida. Antes se
        /// descartaba en <c>Debug.WriteLine</c> (invisible en Release), así que un fallo
        /// real de lectura posterior al commit no dejaba rastro de su causa. El detalle
        /// se muestra al usuario y queda accesible para diagnóstico/pruebas.
        /// </summary>
        private string RegistrarErrorLecturaFresca(string origen, Exception ex)
        {
            var detalle = $"{ex.GetType().Name}: {ex.Message}";
            UltimoErrorLecturaFresca = $"{origen}: {detalle}";
            System.Diagnostics.Debug.WriteLine(
                $"[ProgramaObra] {origen} falló: {ex}");
            return detalle;
        }

        /// <summary>
        /// Causa del último fallo de lectura/publicación fresca (null si no hubo). Seam
        /// de diagnóstico: antes el error sólo iba a Debug y era imposible saber por qué
        /// la vista quedó bloqueada.
        /// </summary>
        internal string? UltimoErrorLecturaFresca { get; private set; }

        /// <summary>
        /// Estado seguro cuando la lectura/publicación fresca no termina: se conserva el
        /// bloqueo, no se reintenta, no se descarta historial y no se publican cachés.
        /// </summary>
        private void BloquearPorPublicacionFallida(string estado, string mensaje)
        {
            _requiereLecturaFresca = true;
            _undoManager.SuspendForReconciliation();
            ActualizarHabilitacionPrograma();
            lblEstado.Text = estado;
            Notificador.Advertir(mensaje);
        }

        /// <summary>
        /// Reconciliación completa tras un resultado INCIERTO (programa o formato):
        /// lee programa, overrides de celda, estilos de fila Y el snapshot de publicación
        /// (Curva S, Gantt, plazo) con un contexto NUEVO a temporales y SÓLO si TODAS las
        /// lecturas tienen éxito publica todo junto, limpia pendientes, descarta las
        /// familias obsoletas y libera el bloqueo. Si cualquiera falla no se publica
        /// nada: cachés, historial y bloqueo siguen intactos (no hay desbloqueo parcial
        /// por leer sólo el formato).
        /// </summary>
        internal bool ReconciliarProgramaYFormatoDesdeBase(IProjectDbContextFactory? factory = null)
        {
            ProgramLoadDto? programa;
            PublicacionProgramaSnapshot publicacion;
            Dictionary<ReportCellAddress, ReportStyleOverlay> overlays;
            Dictionary<string, ReportStyleOverlay> estilosFila;

            try
            {
                UltimoErrorLecturaFresca = null;
                using var contextoFresco = (factory ?? _factory).Create(_context.DatabasePath);
                programa = LeerProgramaParaPublicacion(contextoFresco);
                publicacion = PrepararSnapshotParaPublicacion(programa, contextoFresco);

                var servicioFormato = new FormatoReporteService(contextoFresco);
                overlays = new Dictionary<ReportCellAddress, ReportStyleOverlay>(
                    servicioFormato.CargarCeldas(_proyecto.Id, FormatoTipoReporte));
                estilosFila = new Dictionary<string, ReportStyleOverlay>(
                    servicioFormato.CargarEstilosFila(_proyecto.Id, FormatoTipoReporte),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                // Sin lectura verificada no hay reconciliación: se conserva el bloqueo
                // y las cachés anteriores (no se publica un estado parcial).
                if (UltimoErrorLecturaFresca is null)
                    RegistrarErrorLecturaFresca("ReconciliarProgramaYFormatoDesdeBase", ex);
                return false;
            }

            // La vista del programa se publica ANTES de liberar nada: si falla, el
            // bloqueo sigue vigente y las cachés de formato no se tocan.
            try
            {
                AplicarProgramaEnUi(programa, null, publicacion);
            }
            catch (Exception ex)
            {
                RegistrarErrorLecturaFresca("ReconciliarProgramaYFormatoDesdeBase (publicación)", ex);
                return false;
            }

            _overlaysFormato = overlays;
            _estilosFilaFormato = estilosFila;
            _estilosFilaFormatoProyectoId = _proyecto.Id;

            // Los pendientes eran parte del estado incierto: no se vuelcan.
            _pendientesFormato.Clear();

            // Programa y formato se releyeron completos: ambas familias son obsoletas.
            _undoManager.DescartarPorFamilia(UndoFamily.Programa);
            _undoManager.DescartarPorFamilia(UndoFamily.Formato);
            _programaOperacionIncierta = false;
            _requiereLecturaFresca = false;

            // Recién con TODO releído y publicado se descarta el historial restante y se
            // desbloquea.
            FormatoCeldasPersistence.ReconciliarSiCorresponde(_undoManager);
            ActualizarHabilitacionPrograma();

            if (dgvActividades is not null && !dgvActividades.IsDisposed)
                dgvActividades.Invalidate();

            lblEstado.Text = programa is null
                ? "No existe un programa de obra generado para este proyecto."
                : "Programa y formato recargados desde la base.";
            return true;
        }
    }
}
