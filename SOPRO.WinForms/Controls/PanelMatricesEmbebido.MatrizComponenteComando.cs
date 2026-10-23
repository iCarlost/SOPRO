using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Controls
{
    /// <summary>
    /// Ruta de comando (ADR-0001) para editar la Cantidad de un componente YA
    /// persistido, tanto desde la celda del grid como desde el diálogo de
    /// cantidad: la intención viaja inmutable a
    /// <see cref="BudgetPersistenceService.ExecuteMatrixComponentQuantityCommand"/>
    /// sobre un contexto NUEVO; el panel no escribe en su contexto compartido ni
    /// recalcula importes por su cuenta. Los componentes temporales y el resto
    /// de columnas siguen la ruta heredada a propósito.
    /// </summary>
    public partial class PanelMatricesEmbebido
    {
        /// <summary>
        /// Bases pre-edición capturadas antes de mutar la vista: construir el
        /// comando desde el estado tracked post-edición invalidaría la
        /// validación contra la base.
        /// </summary>
        internal sealed class IntentoCantidadComponente
        {
            public int ComponentId;
            public BudgetProjectCalculationBase Proyecto = null!;
            public BudgetMatrixCalculationBase Matriz = null!;
            public BudgetMatrixComponentFingerprint Componentes = null!;
            public BudgetMatrixComponentQuantityBase Componente = null!;
        }

        private readonly IProjectDbContextFactory _factoryComando = new ProjectDbContextFactory();
        private IntentoCantidadComponente? _intentoCantidadPendiente;

        /// <summary>
        /// Bloquea nuevas ediciones de cantidad de componente tras un resultado
        /// <see cref="BudgetOperationStatus.OutcomeUnknown"/>: reintentar sin
        /// reconcilar duplicaría un ciclo cuyo commit no se sabe si ocurrió.
        /// Solo una recarga segura del panel lo libera.
        /// </summary>
        private bool _edicionCantidadComponenteBloqueada;

        /// <summary>Punto de aislamiento de avisos para las pruebas; el panel no abre avisos modales propios en la ruta de comando.</summary>
        internal static Action<string, string, MessageBoxIcon> MostrarAvisoCantidadComponente { get; set; }
            = (titulo, mensaje, icono) => MessageBox.Show(mensaje, titulo, MessageBoxButtons.OK, icono);

        /// <summary>Detalle técnico del último despacho no enviado; nunca se muestra.</summary>
        internal string? UltimaRazonSinDespachar { get; private set; }

        /// <summary>
        /// Clasificación de la última edición de cantidad de componente ejecutada
        /// como comando, o <c>null</c> si no llegó a enviarse (trabajo pendiente
        /// en el contexto compartido o transición de celda ambigua).
        /// Observabilidad interna para las pruebas; no se muestra en la UI.
        /// </summary>
        internal BudgetOperationStatus? UltimoEstadoEdicionCantidadComponente { get; private set; }

        /// <summary>
        /// Bases pre-edición pendientes de despachar, conservadas cuando el
        /// contexto compartido estaba sucio o la transición de celda ambigua.
        /// </summary>
        internal IntentoCantidadComponente? IntentoCantidadPendiente => _intentoCantidadPendiente;

        /// <summary>
        /// Instantánea confirmada por la ruta de comando (matrices afectadas,
        /// componentes recalculados, hojas y agrupadores del presupuesto). La
        /// consume FormPresupuesto aplicando filas por Id; NO reemplaza a
        /// <see cref="MatrizActualizada"/>, que las rutas heredadas siguen usando
        /// para el refresco de precios.
        /// </summary>
        public event EventHandler<BudgetMatrixComponentQuantitySnapshot>? CantidadComponenteConfirmada;

        /// <summary>
        /// Ruta de comando para la celda Cantidad de un componente persistido.
        /// Devuelve <c>false</c> cuando la edición no corresponde a esta ruta
        /// (modo de creación, componente sin persistir, cantidad inválida) y el
        /// flujo heredado debe continuar.
        /// </summary>
        internal bool ProcesarEdicionCantidadComponenteCelda(int rowIndex, string rawValue)
        {
            if (_dgvComponentes == null)
                return false;
            if (rowIndex < 0 || rowIndex >= _dgvComponentes.Rows.Count)
                return false;
            if (UsaComponentesTemporales())
                return false;

            var comp = ObtenerComponentePorFilaTrabajo(rowIndex);
            if (comp == null)
                return false;

            return ProcesarCantidadComandoPersistida(
                comp,
                rowIndex,
                rawValueRellenar: rawValue,
                cantidadDialogo: null);
        }

        /// <summary>
        /// Ruta de comando para el diálogo de cantidad de un componente
        /// persistido; mismo comando que la celda.
        /// </summary>
        internal bool ProcesarEdicionCantidadComponenteDialogo(int rowIndex, decimal cantidad)
        {
            if (_dgvComponentes == null)
                return false;
            if (rowIndex < 0 || rowIndex >= _dgvComponentes.Rows.Count)
                return false;
            if (UsaComponentesTemporales())
                return false;

            var comp = ObtenerComponentePorFilaTrabajo(rowIndex);
            if (comp == null)
                return false;

            return ProcesarCantidadComandoPersistida(
                comp,
                rowIndex,
                rawValueRellenar: null,
                cantidadDialogo: cantidad);
        }

        /// <summary>
        /// Núcleo de la ruta de comando: valida la propuesta con el MISMO servicio
        /// canónico de captura pero sobre una sonda fuera del tracker (la entidad
        /// compartida nunca se muta en esta ruta: el borrador vive solo en la
        /// celda), captura las bases pre-edición y despacha el comando (única
        /// escritura, contexto nuevo). Devuelve <c>true</c> cuando la edición
        /// queda atendida aquí y el llamador NO debe ejecutar la ruta heredada.
        /// Con el bloqueo por resultado incierto activo, el borrador se conserva
        /// y no se despacha ni se guarda.
        /// </summary>
        private bool ProcesarCantidadComandoPersistida(
            ComponenteMatriz comp,
            int rowIndex,
            string? rawValueRellenar,
            decimal? cantidadDialogo)
        {
            if (comp.Id <= 0)
                return false;
            if (_edicionCantidadComponenteBloqueada)
                return true;

            // Objetivo reconocido: componente YA persistido. Cualquier incapacidad
            // de construir o despachar el comando se CONSUME aquí: la edición
            // fallida conserva el borrador y NUNCA cae a la ruta heredada (un
            // guardado ajeno involuntario no puede confirmar un borrador sin
            // validar).
            if (_matrizActual == null || comp.MatrizId != _matrizActual.Id)
            {
                MostrarAvisoCantidadComponente(
                    "Cantidad del componente",
                    "La matriz del componente cambió en el panel; la cantidad no se guardó.",
                    MessageBoxIcon.Warning);
                return true;
            }

            // Propuesta validada con el servicio canónico sobre una sonda SIN
            // registrar en el tracker: la misma lectura semántica que aplica el
            // comando, sin tocar la entidad compartida.
            var sonda = ClonarSondaCantidad(comp);
            var propuestaValidada = cantidadDialogo.HasValue
                ? MatrixComponentEditingService.UpdateQuantityFromDialog(sonda, cantidadDialogo.Value)
                : MatrixComponentEditingService.UpdateQuantity(sonda, rawValueRellenar ?? string.Empty);

            if (!propuestaValidada.Success)
            {
                MostrarAvisoCantidadComponente(
                    "Cantidad del componente",
                    propuestaValidada.ErrorMessage ?? "La cantidad del componente no es válida; no se guardó nada.",
                    MessageBoxIcon.Information);
                return true;
            }

            // Bases pre-edición: capturar ANTES de que la confirmación toque nada.
            var basesPreEdicion = PrepararIntencionCantidad(comp);
            if (basesPreEdicion == null
                && !(_intentoCantidadPendiente is { } pendiente && pendiente.ComponentId == comp.Id))
            {
                MostrarAvisoCantidadComponente(
                    "Cantidad del componente",
                    "La cantidad no se guardó: hay cambios pendientes en el contexto o el " +
                    "componente cambió; revise y reintente, o recargue la vista.",
                    MessageBoxIcon.Information);
                return true;
            }

            var bases = basesPreEdicion ?? _intentoCantidadPendiente!;

            return DespacharCantidadComponente(bases, comp, sonda.Cantidad, rowIndex);
        }

        /// <summary>
        /// Parsea la cantidad con el MISMO servicio canónico de captura (misma
        /// tolerancia de formatos) mediante una sonda fuera del tracker.
        /// Devuelve null si la cantidad no es válida.
        /// </summary>
        internal static decimal? ParsearCantidadConServicio(string rawValue)
        {
            if (rawValue == null)
                return null;

            var sonda = new ComponenteMatriz();
            var resultado = MatrixComponentEditingService.UpdateQuantity(sonda, rawValue);
            return resultado.Success ? sonda.Cantidad : null;
        }

        /// <summary>Sonda fuera del tracker para validar la propuesta sin mutar la entidad compartida.</summary>
        private static ComponenteMatriz ClonarSondaCantidad(ComponenteMatriz comp)
        {
            return new ComponenteMatriz
            {
                MatrizId = comp.MatrizId,
                TipoComponente = comp.TipoComponente,
                MaterialId = comp.MaterialId,
                ManoDeObraId = comp.ManoDeObraId,
                MaquinariaId = comp.MaquinariaId,
                AuxiliarId = comp.AuxiliarId,
                HerramientaId = comp.HerramientaId,
                Cantidad = comp.Cantidad,
                Rendimiento = comp.Rendimiento,
                Importe = comp.Importe,
                Orden = comp.Orden,
            };
        }

        /// <summary>
        /// Prepara las bases pre-edición del intento. Devuelve <c>null</c> cuando
        /// no puede construir un comando representativo de lo que la vista tenía
        /// (componente ya tocado en el tracker compartido sin bases pendientes o
        /// proyecto no disponible).
        /// </summary>
        private IntentoCantidadComponente? PrepararIntencionCantidad(ComponenteMatriz comp)
        {
            var estadoComponente = _context.Entry(comp).State;
            if (estadoComponente != EntityState.Unchanged && estadoComponente != EntityState.Detached)
                return null;

            var proyecto = _context.Proyectos.AsNoTracking()
                .FirstOrDefault(p => p.Id == _proyectoId);
            if (proyecto == null)
                return null;

            return new IntentoCantidadComponente
            {
                ComponentId = comp.Id,
                Proyecto = BudgetProjectCalculationBase.FromProyecto(proyecto),
                Matriz = BudgetMatrixCalculationBase.FromMatriz(_matrizActual!),
                Componentes = BudgetMatrixComponentFingerprint.FromComponentes(_matrizActual!.Componentes),
                Componente = BudgetMatrixComponentQuantityBase.FromComponente(comp),
            };
        }

        /// <summary>
        /// Envía la intención al comando sobre un contexto nuevo. Si el contexto
        /// compartido está sucio o la transición de celda es ambigua, conserva el
        /// borrador visible y las bases como intento pendiente sin despachar y
        /// sin guardar. Un reintento del MISMO componente reutiliza las bases
        /// pre-edición; una edición de otro componente arranca bases propias.
        /// </summary>
        private bool DespacharCantidadComponente(
            IntentoCantidadComponente basesPreEdicion,
            ComponenteMatriz comp,
            decimal cantidadPropuesta,
            int rowIndex)
        {
            UltimoEstadoEdicionCantidadComponente = null;

            _context.ChangeTracker.DetectChanges();
            bool contextoSucio = _context.ChangeTracker.HasChanges();
            bool transicionAmbigua = EsTransicionCeldaAmbigua(rowIndex);

            if (contextoSucio || transicionAmbigua)
            {
                UltimaRazonSinDespachar = contextoSucio
                    ? "Contexto compartido sucio"
                    : "Transición de celda ambigua";
                _intentoCantidadPendiente = basesPreEdicion;
                MostrarAvisoCantidadComponente(
                    "Cantidad del componente",
                    "La cantidad no se envió a guardar: hay cambios pendientes o la celda está en " +
                    "transición. El borrador queda visible; reintente para guardar.",
                    MessageBoxIcon.Information);
                return true;
            }

            var bases = _intentoCantidadPendiente != null
                && _intentoCantidadPendiente.ComponentId == comp.Id
                    ? _intentoCantidadPendiente
                    : basesPreEdicion;

            var rowset = CapturarRowsetPresupuesto();
            if (rowset == null)
            {
                _intentoCantidadPendiente = bases;
                MostrarAvisoCantidadComponente(
                    "Cantidad del componente",
                    "La cantidad no se guardó: el rowset del presupuesto no está íntegro en la vista; recargue y reintente.",
                    MessageBoxIcon.Warning);
                return true;
            }

            BudgetMatrixComponentQuantityResult resultado;
            try
            {
                resultado = BudgetPersistenceService.ExecuteMatrixComponentQuantityCommand(
                    _factoryComando,
                    _context.DatabasePath,
                    new BudgetMatrixComponentQuantityCommand(
                        _proyectoId,
                        bases.Proyecto,
                        bases.Matriz,
                        bases.Componentes,
                        bases.Componente,
                        rowset,
                        cantidadPropuesta));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Edición de cantidad de componente: {ex.Message}");
                _intentoCantidadPendiente = bases;
                MostrarAvisoCantidadComponente(
                    "Cantidad del componente",
                    "La cantidad no se guardó por un problema inesperado; revise y reintente.",
                    MessageBoxIcon.Warning);
                return true;
            }

            UltimoEstadoEdicionCantidadComponente = resultado.Status;

            if (!resultado.IsCommitted || resultado.Confirmed is null)
            {
                if (resultado.Status == BudgetOperationStatus.OutcomeUnknown)
                {
                    _intentoCantidadPendiente = null;
                    _edicionCantidadComponenteBloqueada = true;
                    MostrarAvisoCantidadComponente(
                        "Cantidad del componente",
                        "No se sabe si el cambio quedó guardado. Revise el concepto, cierre y " +
                        "vuelva a abrir el presupuesto para reconciliar la vista.",
                        MessageBoxIcon.Warning);
                }
                else
                {
                    _intentoCantidadPendiente = bases;
                    MostrarAvisoCantidadComponente(
                        "Cantidad del componente",
                        resultado.Message ?? "La cantidad no se guardó.",
                        MessageBoxIcon.Information);
                }
                return true;
            }

            _intentoCantidadPendiente = null;

            if (_modoSubedicionComponente && _matrizPadreIdSubedicion.HasValue)
                _huboEdicionEnSubedicion = true;

            AplicarInstantaneaCantidadComponente(resultado.Confirmed);
            return true;
        }

        /// <summary>
        /// Transición de celda ambigua: la celda sigue en modo edición o el grid
        /// del presupuesto está editando; el despacho se difiere con el borrador
        /// visible para no pisar lo que el usuario está capturando.
        /// </summary>
        private bool EsTransicionCeldaAmbigua(int rowIndex)
        {
            if (_dgvComponentes.IsCurrentCellInEditMode)
                return true;

            return _dgvPresupuesto is { IsCurrentCellInEditMode: true };
        }

        /// <summary>
        /// Rowset completo de filas del presupuesto tal como las tiene la vista
        /// (Tags del grid). Devuelve <c>null</c> si el rowset no es íntegro: el
        /// comando exige la serie completa del proyecto.
        /// </summary>
        private BudgetPriceRefreshRowset? CapturarRowsetPresupuesto()
        {
            if (_dgvPresupuesto == null)
                return null;

            var filas = _dgvPresupuesto.Rows
                .Cast<DataGridViewRow>()
                .Select(r => r.Tag as ConceptoPresupuesto)
                .Where(c => c is not null && c.Id > 0)
                .ToList();

            if (!filas.Any() || filas.Any(f => f!.ProyectoId != _proyectoId))
                return null;

            return BudgetPriceRefreshRowset.FromConceptos(filas!);
        }

        /// <summary>
        /// Aplica la instantánea confirmada: replica los valores confirmados en
        /// las entidades compartidas tracked afectadas dejándolas Unchanged (sin
        /// limpiar el ChangeTracker ni tocar entidades Modified ajenas), repinta
        /// el panel desde la instantánea y notifica a FormPresupuesto para las
        /// filas de presupuesto; todo sin segunda escritura.
        /// </summary>
        private void AplicarInstantaneaCantidadComponente(BudgetMatrixComponentQuantitySnapshot snapshot)
        {
            MarcarAfectadasUnchanged(snapshot);

            if (_matrizActual != null)
            {
                var filaMatriz = snapshot.AffectedMatrices
                    .FirstOrDefault(m => m.MatrixId == _matrizActual.Id);
                if (filaMatriz != null)
                {
                    _matrizActual.CostoDirecto = filaMatriz.CostoDirecto;
                    _matrizActual.FechaModificacion = filaMatriz.FechaModificacion;
                }

                var componentesPorId = snapshot.AffectedComponents
                    .GroupBy(c => c.ComponentId)
                    .ToDictionary(g => g.Key, g => g.First());
                foreach (var componente in _matrizActual.Componentes)
                {
                    if (componentesPorId.TryGetValue(componente.Id, out var fila))
                    {
                        componente.Cantidad = fila.Cantidad;
                        componente.Rendimiento = fila.Rendimiento;
                        componente.Importe = fila.Importe;
                    }
                }

                PintarComponentesEnGrid(_matrizActual.Componentes);
            }

            CantidadComponenteConfirmada?.Invoke(this, snapshot);
        }

        /// <summary>
        /// Marca SOLO las entidades compartidas tracked afectadas como Unchanged
        /// con OriginalValues sincronizados, para que ningún SaveChanges heredado
        /// posterior las vuelva a escribir; las entidades Modified ajenas se
        /// conservan y el ChangeTracker jamás se limpia.
        /// </summary>
        private void MarcarAfectadasUnchanged(BudgetMatrixComponentQuantitySnapshot snapshot)
        {
            var matricesPorId = snapshot.AffectedMatrices
                .GroupBy(m => m.MatrixId)
                .ToDictionary(g => g.Key, g => g.First());
            foreach (var entrada in _context.ChangeTracker.Entries<Matriz>())
            {
                if (!matricesPorId.TryGetValue(entrada.Entity.Id, out var fila))
                    continue;
                entrada.Entity.CostoDirecto = fila.CostoDirecto;
                entrada.Entity.FechaModificacion = fila.FechaModificacion;
                entrada.OriginalValues.SetValues(entrada.CurrentValues);
                entrada.State = EntityState.Unchanged;
            }

            var componentesPorId = snapshot.AffectedComponents
                .GroupBy(c => c.ComponentId)
                .ToDictionary(g => g.Key, g => g.First());
            foreach (var entrada in _context.ChangeTracker.Entries<ComponenteMatriz>())
            {
                if (!componentesPorId.TryGetValue(entrada.Entity.Id, out var fila))
                    continue;
                entrada.Entity.Cantidad = fila.Cantidad;
                entrada.Entity.Rendimiento = fila.Rendimiento;
                entrada.Entity.Importe = fila.Importe;
                entrada.OriginalValues.SetValues(entrada.CurrentValues);
                entrada.State = EntityState.Unchanged;
            }
        }

        /// <summary>Recarga segura del panel: libera el bloqueo por resultado incierto y el intento pendiente.</summary>
        private void LiberarBloqueoEdicionCantidadComponente()
        {
            _edicionCantidadComponenteBloqueada = false;
            _intentoCantidadPendiente = null;
        }

        /// <summary>Acceso del ensamblado de pruebas al grid del panel.</summary>
        internal DataGridView DgvComponentesInterno => _dgvComponentes;

        /// <summary>
        /// Semilla de edición para las pruebas: reproduce el bookkeeping de
        /// BeginEdit sobre la celda Cantidad y consume la ruta de comando con
        /// registro de deshacer (misma contabilidad que EndEdit).
        /// </summary>
        internal bool SembrarEdicionCantidadParaPrueba(int rowIndex, string valor)
        {
            if (_dgvComponentes == null)
                return false;

            int columnIndex = _dgvComponentes.Columns["ColCantidad"]?.Index ?? -1;
            if (columnIndex < 0 || rowIndex < 0 || rowIndex >= _dgvComponentes.Rows.Count)
                return false;

            _undoPanelRowIndex = rowIndex;
            _undoPanelColumnIndex = columnIndex;
            _undoPanelColumnName = "colCantidad";
            _undoPanelOldValue = _dgvComponentes.Rows[rowIndex].Cells[columnIndex].Value?.ToString() ?? string.Empty;

            return ConsumirEdicionCantidadComandoConUndo(rowIndex, columnIndex, valor);
        }

        /// <summary>Acceso del ensamblado de pruebas a la etiqueta de costo directo.</summary>
        internal System.Windows.Forms.Label LblDirInterno => _lblDir;

        /// <summary>
        /// Ruta de comando (ADR-0001) para la celda Cantidad de un componente YA
        /// persistido: aplica el borrador/dispatch como comando único y registra
        /// la reversibilidad en el gestor de deshacer. Devuelve <c>false</c>
        /// cuando la edición no es de esta ruta y el flujo heredado continúa.
        /// </summary>
        private bool ConsumirEdicionCantidadComandoConUndo(int rowIndex, int columnIndex, string value)
        {
            if (_dgvComponentes == null)
                return false;
            if (rowIndex < 0 || rowIndex >= _dgvComponentes.Rows.Count)
                return false;
            if (columnIndex < 0 || columnIndex >= _dgvComponentes.Columns.Count)
                return false;

            var nombreColumna = _dgvComponentes.Columns[columnIndex].Name;
            if (nombreColumna != "ColCantidad")
                return false;

            bool consumida = ProcesarEdicionCantidadComponenteCelda(rowIndex, value);
            if (!consumida)
                return false;

            var valorNuevoComando = _dgvComponentes.Rows[rowIndex].Cells[columnIndex].Value?.ToString() ?? string.Empty;
            RegistrarUndoPanel(rowIndex, columnIndex, valorNuevoComando);
            _undoPanelRowIndex = -1;
            _undoPanelColumnIndex = -1;
            _undoPanelColumnName = string.Empty;
            _undoPanelOldValue = string.Empty;
            return true;
        }

        /// <summary>
        /// Registra la inversibilidad de un borrador de celda en el gestor de
        /// deshacer (misma contabilidad que la ruta heredada de EndEdit).
        /// </summary>
        private void RegistrarUndoPanel(int rowIndex, int columnIndex, string valorNuevo)
        {
            string oldValue = _undoPanelOldValue;
            string newValue = valorNuevo;
            string columnName = _undoPanelColumnName;
            string descripcion = $"Editar componente ({columnName})";
            _undoManager.Push(new DelegateUndoableAction(
                descripcion,
                () => AplicarUndoRedoPanelCelda(rowIndex, columnIndex, oldValue),
                () => AplicarUndoRedoPanelCelda(rowIndex, columnIndex, newValue)));
        }
    }
}
