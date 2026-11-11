using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Controls
{
    /// <summary>
    /// Ruta de comando (ADR-0001) para dar de alta en lote componentes NUEVOS en
    /// una matriz YA persistida del panel embebido: tras cerrar el selector se
    /// despacha UNA vez <see cref="BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand"/>
    /// para TODO el lote, sobre un contexto NUEVO; el panel jamás usa la ruta
    /// heredada (Add/SaveChanges/GuardarYPropagar) ni recalcula importes por su
    /// cuenta. La atomicidad que se comunica al usuario es SOLO del alta de
    /// componentes: si el selector importó catálogo de otro proyecto con
    /// <c>ExternalInsumoImportService</c>, ese commit ya ocurrió y NO se resta
    /// ni se revierte aquí.
    /// El alta sobre una matriz YA persistida usa el comando existente; la primera
    /// alta de una matriz nueva usa <see cref="BudgetPersistenceService.ExecuteNewMatrixWithComponentsCommand"/>
    /// y crea la matriz, el lote y el vínculo al concepto en una sola transacción.
    /// El panel jamás usa la ruta heredada (Add/SaveChanges/GuardarYPropagar) para
    /// esas altas ni recalcula importes por su cuenta.
    /// </summary>
    public partial class PanelMatricesEmbebido
    {
        private BudgetMatrixComponentAdditionCommand? _comandoAltasComponentesPendiente;
        private bool _despachandoNuevaMatriz;
        private bool _nuevaMatrizPendienteReconciliacion;
        private string? _firmaUltimaSeleccionRechazada;
        private decimal? _cantidadNuevaMatriz;
        private int? _conceptoOrigenIdNuevaMatriz;

        /// <summary>
        /// Estado de la última primera alta de matriz vinculada a un concepto.
        /// </summary>
        internal BudgetOperationStatus? UltimoEstadoNuevaMatriz { get; private set; }

        internal string? UltimaRazonNuevaMatrizSinDespachar { get; private set; }

        /// <summary>Entrada de primera alta para las pruebas; misma ruta que el selector.</summary>
        internal void AgregarPrimerLoteNuevaMatrizParaPrueba(TipoComponenteMatriz tipoComponente)
            => AgregarComponentesTemporales(tipoComponente);

        /// <summary>
        /// Clasificación de la última alta en lote ejecutada como comando, o
        /// <c>null</c> si no llegó a enviarse (tracker sucio, transición ambigua,
        /// lote vacío o lotes rechazados). Observabilidad interna para las
        /// pruebas; no se muestra en la UI.
        /// </summary>
        internal BudgetOperationStatus? UltimoEstadoAltasComponente { get; private set; }

        /// <summary>Detalle técnico de la última alta no despachada; nunca se muestra.</summary>
        internal string? UltimaRazonAltasSinDespachar { get; private set; }

        /// <summary>
        /// Comando inmutable del lote retenido para reintento explícito tras un
        /// bloqueo o rollback; nunca se reenvía de forma silenciosa.
        /// </summary>
        internal BudgetMatrixComponentAdditionCommand? AltaPendiente => _comandoAltasComponentesPendiente;

        /// <summary>
        /// Instantánea confirmada por la ruta de comando de ALTA en lote (matrices
        /// afectadas, componentes nuevos y restantes con sus notas, hojas y
        /// agrupadores del presupuesto). La consume FormPresupuesto aplicando
        /// filas por Id; NUNCA dispara refrescos de precios por segunda vez.
        /// </summary>
        public event EventHandler<BudgetMatrixComponentAdditionSnapshot>? AltasComponentesConfirmadas;

        /// <summary>
        /// Primera alta confirmada de una matriz nueva vinculada a un concepto:
        /// FormPresupuesto aplica la asignación y los totales por Id, sin callback
        /// heredado ni segunda escritura.
        /// </summary>
        public event EventHandler<BudgetNewMatrixWithComponentsSnapshot>? NuevaMatrizConComponentesConfirmada;

        /// <summary>Aislamiento de avisos de la ruta de altas para las pruebas.</summary>
        internal static Action<string, string, MessageBoxIcon> MostrarAvisoAltasComponente { get; set; }
            = (titulo, mensaje, icono) => MessageBox.Show(mensaje, titulo, MessageBoxButtons.OK, icono);

        /// <summary>
        /// Confirmación de reenvío del lote retenido para las pruebas; el panel
        /// jamás reenvía el mismo lote de forma silenciosa.
        /// </summary>
        internal static Func<bool> ConfirmarReintentoAltasComponente { get; set; }
            = () => MessageBox.Show(
                "Hay un lote retenido que no se guardó. ¿Desea reintentarlo ahora?",
                "Reintentar guardado",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes;

        /// <summary>
        /// Sustituto del selector de insumos para las pruebas; en producción es
        /// <c>null</c> y se abre el diálogo real. Devuelve los componentes
        /// seleccionados, una lista vacía (lote vacío) o null (cancelado).
        /// </summary>
        internal static Func<TipoComponenteMatriz, List<ComponenteMatriz>?>? SeleccionInsumoParaPrueba { get; set; }

        private static List<ComponenteMatriz>? AbrirSelectorInsumos(
            SOPROContext context, int proyectoId, TipoComponenteMatriz tipoComponente)
        {
            using var dlg = new Forms.FormSeleccionarInsumo(context, proyectoId, tipoComponente);
            return dlg.ShowDialog() == DialogResult.OK ? dlg.ComponentesSeleccionados : null;
        }

        /// <summary>Entrada de ruta de alta para las pruebas; misma ruta que los botones.</summary>
        internal void AgregarInsumosPersistidosParaPrueba(TipoComponenteMatriz tipoComponente)
            => AgregarComponentesPersistidos(tipoComponente);

        /// <summary>
        /// Ruta de comando para el lote del selector sobre una matriz persistida:
        /// construye las bases y el lote SOLO desde la vista (sin mutar nada), valida
        /// tipos y fuentes y despacha una vez. Un fallo o conflicto preserva la vista
        /// y avisa que no se guardó nada; un lote vacío termina sin comando ni aviso.
        /// La primera alta de matriz nueva tiene su propia ruta atómica.
        /// </summary>
        private void AgregarComponentesPersistidos(TipoComponenteMatriz tipoComponente)
        {
            UltimoEstadoAltasComponente = null;
            UltimaRazonAltasSinDespachar = null;

            if (_matrizActual == null)
                return;

            if (_edicionCantidadComponenteBloqueada)
            {
                UltimaRazonAltasSinDespachar = "Panel bloqueado por resultado incierto previo";
                MostrarAvisoAltasComponente(
                    "Agregar insumos",
                    "Las altas no se procesaron: hay una operación previa cuyo resultado no se sabe si " +
                    "quedó guardado. Cierre y vuelva a abrir el presupuesto para reconciliar la vista.",
                    MessageBoxIcon.Warning);
                return;
            }

            if (_comandoAltasComponentesPendiente is { } pendiente)
            {
                TratarAltasPendientes(pendiente);
                return;
            }

            var seleccion = SeleccionInsumoParaPrueba?.Invoke(tipoComponente)
                ?? AbrirSelectorInsumos(_context, _proyectoId, tipoComponente);

            // Cancelado o lote vacío: no-op real, sin comando y sin aviso.
            if (seleccion is null || seleccion.Count == 0)
                return;

            var comando = PrepararComandoAltas(seleccion, tipoComponente, out var motivoRechazo);
            if (comando == null)
            {
                UltimaRazonAltasSinDespachar = motivoRechazo;
                MostrarAvisoAltasComponente(
                    "Agregar insumos",
                    motivoRechazo ?? "Las altas no se guardaron.",
                    MessageBoxIcon.Information);
                return;
            }

            DespacharAltasComponentes(comando);
        }

        /// <summary>
        /// Lote retenido presente: nunca se reenvía de forma silenciosa. Con el
        /// panel bloqueado no llega aquí; si la matriz visible cambió, el lote es
        /// obsoleto y se exige re-selección; si no, se ofrece un reintento
        /// explícito del MISMO comando o la re-selección de insumos.
        /// </summary>
        private void TratarAltasPendientes(BudgetMatrixComponentAdditionCommand pendiente)
        {
            if (pendiente.MatrixId != _matrizActual!.Id)
            {
                _comandoAltasComponentesPendiente = null;
                UltimaRazonAltasSinDespachar = "Lote pendiente obsoleto: corresponde a otra matriz";
                MostrarAvisoAltasComponente(
                    "Agregar insumos",
                    "El lote pendiente corresponde a otra matriz ya no visible; no se guardó. " +
                    "Vuelva a seleccionar los insumos en la matriz actual para reintentar.",
                    MessageBoxIcon.Warning);
                return;
            }

            if (ConfirmarReintentoAltasComponente())
                DespacharAltasComponentes(pendiente);
            else
                MostrarAvisoAltasComponente(
                    "Agregar insumos",
                    "El lote queda retenido y no se guardó nada: reintente la misma adición " +
                    "o vuelva a seleccionar los insumos.",
                    MessageBoxIcon.Information);
        }

        /// <summary>
        /// Construye el comando inmutable desde la vista ANTES de cualquier
        /// mutación: bases del proyecto (AsNoTracking), de la matriz y del estado
        /// completo de sus componentes, rowset del presupuesto y bases de catálogo
        /// de cada insumo seleccionado. Devuelve <c>null</c> con el motivo del
        /// rechazo cuando el lote tiene tipos incompatibles o una fila auxiliar sin
        /// fuente/navegación.
        /// </summary>
        private BudgetMatrixComponentAdditionCommand? PrepararComandoAltas(
            List<ComponenteMatriz> seleccion,
            TipoComponenteMatriz tipoComponente,
            out string? motivoRechazo)
        {
            motivoRechazo = null;

            if (_matrizActual == null)
            {
                motivoRechazo = "La matriz visible cambió en el panel; ninguna alta se guardó.";
                return null;
            }

            var adiciones = ConstruirAdiciones(seleccion, tipoComponente, out motivoRechazo);
            if (adiciones == null)
                return null;

            var proyecto = _context.Proyectos.AsNoTracking()
                .FirstOrDefault(p => p.Id == _proyectoId);
            if (proyecto == null)
            {
                motivoRechazo =
                    "Las altas no se guardaron: el proyecto no está disponible en la vista; recargue y reintente.";
                return null;
            }

            var rowset = CapturarRowsetPresupuesto();
            if (rowset == null)
            {
                motivoRechazo =
                    "Las altas no se guardaron: el rowset del presupuesto no está íntegro en la vista; recargue y reintente.";
                return null;
            }

            var fingerprint =
                BudgetMatrixComponentStateFingerprint.FromComponentes(_matrizActual.Componentes);

            try
            {
                return new BudgetMatrixComponentAdditionCommand(
                    _proyectoId,
                    BudgetProjectCalculationBase.FromProyecto(proyecto),
                    BudgetMatrixCalculationBase.FromMatriz(_matrizActual),
                    fingerprint,
                    rowset,
                    adiciones);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Preparación de altas de componentes: {ex.Message}");
                motivoRechazo = "Las altas no se guardaron: el lote no es válido; revise la selección.";
                return null;
            }
        }

        /// <summary>
        /// Valida el lote y construye las altas tipadas con el tipo REAL de cada fila.
        /// Compartido por el alta sobre matriz persistida y por la primera alta
        /// vinculada. Devuelve <c>null</c> con el motivo del rechazo.
        /// </summary>
        private static List<BudgetMatrixComponentAdditionItem>? ConstruirAdiciones(
            List<ComponenteMatriz> seleccion,
            TipoComponenteMatriz? tipoComponente,
            out string? motivoRechazo)
        {
            motivoRechazo = null;

            if (seleccion.Any(c => c is null))
            {
                motivoRechazo = "El lote contiene una fila nula; no se guardó nada.";
                return null;
            }

            var auxiliarSinFuente = seleccion.FirstOrDefault(c =>
                c.TipoComponente == TipoComponenteMatriz.Auxiliar
                && (c.AuxiliarId is not > 0 || c.Auxiliar == null));
            if (auxiliarSinFuente != null)
            {
                motivoRechazo =
                    $"El auxiliar {auxiliarSinFuente.AuxiliarId} cambió en la vista; no se guardó nada.";
                return null;
            }

            if (tipoComponente.HasValue
                && seleccion.Any(c => !EsTipoSeleccionable(tipoComponente.Value, c)))
            {
                motivoRechazo =
                    "El lote mezcla tipos de insumo incompatibles y no se guardó nada; " +
                    "agregue cada tipo por separado.";
                return null;
            }

            var adiciones = new List<BudgetMatrixComponentAdditionItem>(seleccion.Count);
            foreach (var componente in seleccion)
            {
                var baseOrigen = ConstruirBaseComponente(componente, out var error);
                if (baseOrigen == null)
                {
                    motivoRechazo = error ?? "Las altas no se guardaron.";
                    return null;
                }

                if (componente.Cantidad <= 0m)
                {
                    motivoRechazo =
                        $"El insumo {baseOrigen.Clave} {componente.TipoComponente} " +
                        "tiene una cantidad inválida; no se guardó nada.";
                    return null;
                }

                adiciones.Add(new BudgetMatrixComponentAdditionItem(
                    baseOrigen.TipoComponente,
                    baseOrigen.SourceId,
                    baseOrigen,
                    componente.Cantidad,
                    componente.Notas ?? string.Empty));
            }

            return adiciones;
        }

        /// <summary>
        /// Construye el comando de primera alta vinculada desde la vista: cabecera,
        /// concepto persistido por <c>ConceptoId</c>, rowset completo y lote. Solo se
        /// invoca en estado borrador y con al menos un componente.
        /// </summary>
        private BudgetNewMatrixWithComponentsCommand? PrepararComandoNuevaMatriz(
            List<ComponenteMatriz> seleccion,
            List<ComponenteMatriz> nuevas,
            TipoComponenteMatriz tipoComponente,
            out string? motivoRechazo)
        {
            motivoRechazo = null;

            if (!_modoCreacionMatriz || _matrizActual != null)
            {
                motivoRechazo = "La matriz ya está persistida; use la ruta de altas existente.";
                return null;
            }

            string clave = (_txtClaveMatriz.Text ?? string.Empty).Trim();
            string descripcion = (_txtDescripcionMatriz.Text ?? string.Empty).Trim();
            string unidad = (_txtUnidadMatriz.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clave)
                || string.IsNullOrWhiteSpace(descripcion)
                || string.IsNullOrWhiteSpace(unidad))
            {
                motivoRechazo = "Clave, Descripción y Unidad son obligatorios.";
                return null;
            }

            if (GetTipoCabeceraSeleccionado() != TipoMatriz.APU)
            {
                motivoRechazo =
                    "Solo una matriz APU se puede vincular a un concepto del presupuesto.";
                return null;
            }

            if (_dgvPresupuesto == null)
            {
                motivoRechazo = "No hay un concepto de presupuesto seleccionado para vincular la matriz.";
                return null;
            }

            ConceptoPresupuesto? concepto = null;
            DataGridViewRow? filaConcepto = null;
            if (_conceptoOrigenIdNuevaMatriz is int conceptoOrigenId)
            {
                filaConcepto = _dgvPresupuesto.Rows
                    .Cast<DataGridViewRow>()
                    .FirstOrDefault(r => (r.Tag as ConceptoPresupuesto)?.Id == conceptoOrigenId);
                concepto = filaConcepto?.Tag as ConceptoPresupuesto;
            }
            else if (_filaActual >= 0 && _filaActual < _dgvPresupuesto.Rows.Count)
            {
                filaConcepto = _dgvPresupuesto.Rows[_filaActual];
                concepto = filaConcepto.Tag as ConceptoPresupuesto;
            }

            if (concepto is not { Id: > 0 })
            {
                motivoRechazo =
                    "El concepto de origen debe estar guardado para crear y vincular la matriz. " +
                    "Guarde primero el presupuesto.";
                return null;
            }

            if (concepto.EsAgrupador || concepto.MatrizId.HasValue)
            {
                motivoRechazo =
                    "El concepto seleccionado ya tiene matriz o es un agrupador; no se puede vincular otra matriz.";
                return null;
            }

            decimal cantidad = _cantidadNuevaMatriz ?? concepto.Cantidad;
            if (cantidad <= 0m)
            {
                motivoRechazo = "La cantidad del concepto debe ser mayor que cero para crear la matriz.";
                return null;
            }

            var filas = _dgvPresupuesto.Rows
                .Cast<DataGridViewRow>()
                .Select(r => r.Tag as ConceptoPresupuesto)
                .Where(c => c is { Id: > 0 })
                .ToList();

            if (!filas.Any(c => c!.Id == concepto.Id))
            {
                motivoRechazo =
                    "El rowset del presupuesto no está íntegro en la vista; recargue y reintente.";
                return null;
            }

            var rowset = BudgetAssignmentRowset.FromConceptos(filas!);

            var adicionesNuevas = ConstruirAdiciones(nuevas, tipoComponente, out motivoRechazo);
            if (adicionesNuevas == null)
                return null;

            // El lote acumulado puede contener tipos válidos de selecciones previas:
            // no se le impone el tipo del último selector. Solo se valida que cada
            // fila acumulada siga siendo una fuente válida y convertible.
            var adiciones = ConstruirAdiciones(seleccion, tipoComponente: null, out motivoRechazo);
            if (adiciones == null)
                return null;

            var proyecto = _context.Proyectos.AsNoTracking()
                .FirstOrDefault(p => p.Id == _proyectoId);
            if (proyecto == null)
            {
                motivoRechazo = "El proyecto no está disponible en la vista; recargue y reintente.";
                return null;
            }

            try
            {
                return new BudgetNewMatrixWithComponentsCommand(
                    _proyectoId,
                    BudgetProjectCalculationBase.FromProyecto(proyecto),
                    concepto.Id,
                    BudgetConceptAssignmentBase.FromConcepto(concepto),
                    rowset,
                    clave,
                    descripcion,
                    unidad,
                    TipoMatriz.APU,
                    cantidad,
                    adiciones);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Preparación de matriz nueva: {ex.Message}");
                motivoRechazo = "La matriz nueva no se guardó: los datos no son válidos; revise la captura.";
                return null;
            }
        }

        /// <summary>
        /// Despacha la primera alta con protección de reentrada. Un fallo conserva el
        /// borrador y no reenvía nada; un resultado incierto bloquea toda mutación
        /// posterior hasta reconciliar la vista.
        /// </summary>
        private void DespacharNuevaMatriz(
            BudgetNewMatrixWithComponentsCommand comando,
            string firmaSeleccion)
        {
            if (_despachandoNuevaMatriz)
                return;

            if (_nuevaMatrizPendienteReconciliacion)
            {
                MostrarAvisoAltasComponente(
                    "Nueva matriz",
                    "No se puede reintentar: la creación anterior quedó con resultado incierto. " +
                    "Cierre y vuelva a abrir el presupuesto para reconciliar la vista.",
                    MessageBoxIcon.Warning);
                return;
            }

            UltimoEstadoNuevaMatriz = null;
            UltimaRazonNuevaMatrizSinDespachar = null;
            _despachandoNuevaMatriz = true;
            try
            {
                _context.ChangeTracker.DetectChanges();
                if (_context.ChangeTracker.HasChanges())
                {
                    _firmaUltimaSeleccionRechazada = firmaSeleccion;
                    UltimaRazonNuevaMatrizSinDespachar = "Contexto compartido sucio";
                    MostrarAvisoAltasComponente(
                        "Nueva matriz",
                        "La matriz nueva no se envió a guardar: hay cambios pendientes ajenos en el " +
                        "contexto. Guarde o descarte esos cambios y reintente.",
                        MessageBoxIcon.Information);
                    return;
                }

                BudgetNewMatrixWithComponentsResult resultado;
                try
                {
                    resultado = BudgetPersistenceService.ExecuteNewMatrixWithComponentsCommand(
                        _factoryComando,
                        _context.DatabasePath,
                        comando);
                }
                catch (Exception ex)
                {
                    _firmaUltimaSeleccionRechazada = firmaSeleccion;
                    UltimaRazonNuevaMatrizSinDespachar = "Excepción al despachar el comando";
                    Debug.WriteLine($"Matriz nueva con componentes: {ex}");
                    MostrarAvisoAltasComponente(
                        "Nueva matriz",
                        "La matriz nueva no se guardó por un problema inesperado; el borrador queda visible.",
                        MessageBoxIcon.Warning);
                    return;
                }

                UltimoEstadoNuevaMatriz = resultado.Status;

                if (!resultado.IsCommitted || resultado.Confirmed is null)
                {
                    _firmaUltimaSeleccionRechazada = firmaSeleccion;

                    if (resultado.Status == BudgetOperationStatus.OutcomeUnknown)
                    {
                        _edicionCantidadComponenteBloqueada = true;
                        _nuevaMatrizPendienteReconciliacion = true;
                        MostrarAvisoAltasComponente(
                            "Nueva matriz",
                            "No se sabe si la matriz nueva quedó guardada. Cierre y vuelva a abrir el " +
                            "presupuesto para reconciliar la vista antes de reintentar.",
                            MessageBoxIcon.Warning);
                        return;
                    }

                    MostrarAvisoAltasComponente(
                        "Nueva matriz",
                        (resultado.Message ?? "La matriz nueva no se guardó.") +
                        (string.IsNullOrEmpty(resultado.Message) ? string.Empty : " No se guardó nada."),
                        MessageBoxIcon.Information);
                    return;
                }

                _nuevaMatrizPendienteReconciliacion = false;
                _firmaUltimaSeleccionRechazada = null;
                AplicarNuevaMatrizConfirmada(resultado.Confirmed);
            }
            finally
            {
                _despachandoNuevaMatriz = false;
            }
        }

        /// <summary>
        /// Firma determinista de una selección del selector para reconocer un reintento
        /// del MISMO lote y no concatenarlo dos veces al borrador pendiente.
        /// </summary>
        private static string CrearFirmaSeleccion(IEnumerable<ComponenteMatriz> componentes)
            => string.Join("|", componentes.Select(c => string.Join(";",
                ((int)c.TipoComponente).ToString(System.Globalization.CultureInfo.InvariantCulture),
                c.MaterialId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                c.ManoDeObraId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                c.MaquinariaId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                c.AuxiliarId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                c.HerramientaId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                c.Cantidad.ToString(System.Globalization.CultureInfo.InvariantCulture),
                c.Notas ?? string.Empty)));

        private void AplicarNuevaMatrizConfirmada(BudgetNewMatrixWithComponentsSnapshot snapshot)
        {
            _matrizEditandoId = snapshot.MatrixId;
            _modoCreacionMatriz = false;
            _modoEdicionCabecera = true;
            _firmaUltimaSeleccionRechazada = null;
            _cantidadNuevaMatriz = null;
            _conceptoOrigenIdNuevaMatriz = null;
            _componentesTemp.Clear();
            _panelBotonesAgregar.Enabled = true;
            _onMatrixHeaderSaved = null;
            _onMatrixHeaderCancelled = null;

            var recargada = CargarMatrizConComponentes(snapshot.MatrixId, asNoTracking: true);
            if (recargada != null)
            {
                _matrizActual = recargada;
                CapturarEstadoBaseCabeceraDesdeMatriz(recargada);
                MostrarMatriz(recargada, null);
            }

            AplicarPresentacionCabecera(PresentacionCabecera.Matriz);
            NuevaMatrizConComponentesConfirmada?.Invoke(this, snapshot);
        }


        private static BudgetMatrixComponentSourceBase? ConstruirBaseComponente(
            ComponenteMatriz componente, out string? error)
        {
            error = componente.TipoComponente switch
            {
                TipoComponenteMatriz.Material => componente.Material == null
                    ? $"El material {componente.MaterialId} cambió en la vista; no se guardó nada."
                    : null,
                TipoComponenteMatriz.ManoDeObra => componente.ManoDeObra == null
                    ? $"La mano de obra {componente.ManoDeObraId} cambió en la vista; no se guardó nada."
                    : null,
                TipoComponenteMatriz.Maquinaria => componente.Maquinaria == null
                    ? $"La maquinaria {componente.MaquinariaId} cambió en la vista; no se guardó nada."
                    : null,
                TipoComponenteMatriz.Herramienta => componente.Herramienta == null
                    ? $"La herramienta {componente.HerramientaId} cambió en la vista; no se guardó nada."
                    : null,
                TipoComponenteMatriz.Auxiliar => componente.AuxiliarId is > 0 && componente.Auxiliar != null
                    ? null
                    : $"El auxiliar {componente.AuxiliarId} cambió en la vista; no se guardó nada.",
                _ => "El lote contiene un tipo de componente no soportado; no se guardó nada.",
            };

            if (error != null)
                return null;

            return componente.TipoComponente switch
            {
                TipoComponenteMatriz.Material => BudgetMatrixComponentSourceBase.FromMaterial(componente.Material!),
                TipoComponenteMatriz.ManoDeObra => BudgetMatrixComponentSourceBase.FromManoDeObra(componente.ManoDeObra!),
                TipoComponenteMatriz.Maquinaria => BudgetMatrixComponentSourceBase.FromMaquinaria(componente.Maquinaria!),
                TipoComponenteMatriz.Auxiliar => BudgetMatrixComponentSourceBase.FromAuxiliar(componente.Auxiliar!),
                _ => BudgetMatrixComponentSourceBase.FromHerramienta(componente.Herramienta!),
            };
        }

        /// <summary>
        /// Tipos que el selector solicitado puede devolver en un mismo lote:
        /// Mano de Obra entrega mano de obra individual y cuadrillas (Auxiliar cuya
        /// matriz es <see cref="TipoMatriz.Cuadrilla"/>); Auxiliares entrega
        /// únicamente básicos (<see cref="TipoMatriz.Basico"/>). El resto exige
        /// exactamente su tipo. Se valida la fila completa, no solo el enum, para no
        /// aceptar un APU u otra matriz inyectada bajo un selector incompatible.
        /// </summary>
        private static bool EsTipoSeleccionable(
            TipoComponenteMatriz tipoSolicitado,
            ComponenteMatriz componente)
        {
            if (componente is null)
                return false;

            return tipoSolicitado switch
            {
                TipoComponenteMatriz.ManoDeObra =>
                    componente.TipoComponente == TipoComponenteMatriz.ManoDeObra
                    || (componente.TipoComponente == TipoComponenteMatriz.Auxiliar
                        && componente.Auxiliar != null
                        && componente.Auxiliar.Tipo == TipoMatriz.Cuadrilla),

                TipoComponenteMatriz.Auxiliar =>
                    componente.TipoComponente == TipoComponenteMatriz.Auxiliar
                    && componente.Auxiliar != null
                    && componente.Auxiliar.Tipo == TipoMatriz.Basico,

                _ => componente.TipoComponente == tipoSolicitado
            };
        }

        /// <summary>
        /// Envía el lote COMPLETO con UNA llamada: antes del despacho exige el
        /// contexto compartido limpio y sin transición de celda ambigua; si algo
        /// lo impide, retiene el MISMO comando inmutable para un reintento
        /// explícito. Un fallo/conflicto conserva la vista y avisa que no se
        /// guardó; el resultado incierto activa el bloqueo del panel. Una
        /// confirmación aplica la instantánea sin segunda escritura.
        /// </summary>
        private void DespacharAltasComponentes(BudgetMatrixComponentAdditionCommand comando)
        {
            _context.ChangeTracker.DetectChanges();
            bool contextoSucio = _context.ChangeTracker.HasChanges();
            bool transicionAmbigua = EsTransicionCeldaAmbigua(-1);

            if (contextoSucio || transicionAmbigua)
            {
                _comandoAltasComponentesPendiente = comando;
                UltimaRazonAltasSinDespachar = contextoSucio
                    ? "Contexto compartido sucio"
                    : "Transición de celda ambigua";
                MostrarAvisoAltasComponente(
                    "Agregar insumos",
                    "Los insumos no se enviaron a guardar: hay cambios pendientes o una celda en " +
                    "transición en la vista. El lote queda retenido; reintente la misma " +
                    "adición para volver a intentarlo o recargue la vista.",
                    MessageBoxIcon.Information);
                return;
            }

            BudgetMatrixComponentAdditionResult resultado;
            try
            {
                resultado = BudgetPersistenceService.ExecuteMatrixComponentAdditionCommand(
                    _factoryComando,
                    _context.DatabasePath,
                    comando);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Altas de componentes de matriz: {ex.Message}");
                _comandoAltasComponentesPendiente = comando;
                UltimaRazonAltasSinDespachar = "Excepción al despachar el comando";
                MostrarAvisoAltasComponente(
                    "Agregar insumos",
                    "Los insumos no se guardaron por un problema inesperado; reintente el lote " +
                    "retenido o vuelva a seleccionar los insumos.",
                    MessageBoxIcon.Warning);
                return;
            }

            UltimoEstadoAltasComponente = resultado.Status;

            if (!resultado.IsCommitted || resultado.Confirmed is null)
            {
                if (resultado.Status == BudgetOperationStatus.OutcomeUnknown)
                {
                    // No se sabe si el commit ocurrió: NADA del lote se reenvía
                    // hasta una recarga segura. El bloqueo cubre TODAS las
                    // mutaciones de componentes del panel.
                    _edicionCantidadComponenteBloqueada = true;
                    MostrarAvisoAltasComponente(
                        "Agregar insumos",
                        "No se sabe si las altas quedaron guardadas. Revise el concepto, cierre y " +
                        "vuelva a abrir el presupuesto para reconciliar la vista.",
                        MessageBoxIcon.Warning);
                    return;
                }

                _comandoAltasComponentesPendiente = comando;
                MostrarAvisoAltasComponente(
                    "Agregar insumos",
                    (resultado.Message ?? "Los insumos no se guardaron.") +
                    (string.IsNullOrEmpty(resultado.Message) ? string.Empty : " No se guardó nada."),
                    MessageBoxIcon.Information);
                return;
            }

            _comandoAltasComponentesPendiente = null;
            UltimaRazonAltasSinDespachar = null;
            AplicarAltasComponentesConfirmadas(resultado.Confirmed);
        }

        /// <summary>
        /// Aplica la instantánea confirmada: sincroniza SOLO las entidades
        /// compartidas tracked afectadas como Unchanged (los componentes nuevos
        /// jamás se adjuntan al tracker), recarga la matriz del panel AsNoTracking
        /// y emite el evento para FormPresupuesto; todo sin segunda escritura ni
        /// refrescos de precios.
        /// </summary>
        private void AplicarAltasComponentesConfirmadas(BudgetMatrixComponentAdditionSnapshot snapshot)
        {
            MarcarAfectadasUnchangedAltas(snapshot);

            if (_matrizActual != null && snapshot.Matrix.MatrixId == _matrizActual.Id)
            {
                if (_modoSubedicionComponente && _matrizPadreIdSubedicion.HasValue)
                    _huboEdicionEnSubedicion = true;

                var recargada = CargarMatrizConComponentes(_matrizActual.Id, asNoTracking: true);
                if (recargada != null)
                {
                    _matrizActual = recargada;
                    MostrarMatriz(recargada, null);
                }
            }

            AltasComponentesConfirmadas?.Invoke(this, snapshot);
        }

        /// <summary>
        /// Marca SOLO las entidades compartidas tracked afectadas por el alta
        /// como Unchanged con OriginalValues sincronizados (incluidas las notas),
        /// para que ningún SaveChanges heredado las vuelva a escribir; las entidades
        /// Modified ajenas se conservan, el ChangeTracker jamás se limpia y los
        /// componentes nuevos, que no existen en el tracker, no se adjuntan.
        /// </summary>
        private void MarcarAfectadasUnchangedAltas(BudgetMatrixComponentAdditionSnapshot snapshot)
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
                entrada.Entity.Orden = fila.Orden;
                entrada.Entity.Notas = fila.Notas;
                entrada.OriginalValues.SetValues(entrada.CurrentValues);
                entrada.State = EntityState.Unchanged;
            }
        }
    }
}
