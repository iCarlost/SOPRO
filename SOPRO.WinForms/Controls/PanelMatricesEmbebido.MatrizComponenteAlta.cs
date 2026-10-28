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
    /// El modo de creación (componentes temporales) sigue la ruta heredada a
    /// propósito.
    /// </summary>
    public partial class PanelMatricesEmbebido
    {
        private BudgetMatrixComponentAdditionCommand? _comandoAltasComponentesPendiente;

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
        /// Ruta de comando para el lote del selector: construye las bases y el
        /// lote SOLO desde la vista (sin mutar nada), rechaza lotes auxiliares o
        /// mixtos COMPLETOS antes de escribir y despacha una vez. Un fallo o
        /// conflicto preserva la vista y avisa que no se guardó nada; un lote
        /// vacío termina sin comando ni aviso.
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
        /// rechazo cuando el lote auxiliar/mixto o la vista no lo admiten.
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

            // Rechazo COMPLETO del lote antes de escribir: nunca se recorta un
            // auxiliar ni se cae a otro tipo. El selector de mano de obra también
            // puede devolver cuadrillas (matrices).
            if (seleccion.Any(c => c is null || c.TipoComponente == TipoComponenteMatriz.Auxiliar))
            {
                motivoRechazo =
                    "El lote contiene un componente auxiliar o cuadrilla. La adición de auxiliares " +
                    "no está soportada; no se guardó nada. Seleccione solo insumos individuales.";
                return null;
            }

            if (seleccion.Any(c => c.TipoComponente != tipoComponente))
            {
                motivoRechazo =
                    "El lote mezcla tipos de insumo distintos y no se guardó nada; " +
                    "agregue cada tipo por separado.";
                return null;
            }

            var bases = new List<(int SourceId, BudgetMatrixComponentSourceBase Base, decimal Cantidad, string Notas)>();
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

                bases.Add((baseOrigen.SourceId, baseOrigen, componente.Cantidad, componente.Notas ?? string.Empty));
            }

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

            var adiciones = bases.Select(b =>
                new BudgetMatrixComponentAdditionItem(
                    tipoComponente, b.SourceId, b.Base, b.Cantidad, b.Notas));

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
        /// Base de catálogo esperada, construida desde el objeto que mostró la
        /// vista SIN tocar el tracker: es solo lectura y su fuente de verdad es la
        /// que revalidará el comando contra la base fresca.
        /// </summary>
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
                _ => "El lote contiene un componente auxiliar o cuadrilla. " +
                     "La adición de auxiliares no está soportada; no se guardó nada.",
            };

            if (error != null)
                return null;

            return componente.TipoComponente switch
            {
                TipoComponenteMatriz.Material => BudgetMatrixComponentSourceBase.FromMaterial(componente.Material!),
                TipoComponenteMatriz.ManoDeObra => BudgetMatrixComponentSourceBase.FromManoDeObra(componente.ManoDeObra!),
                TipoComponenteMatriz.Maquinaria => BudgetMatrixComponentSourceBase.FromMaquinaria(componente.Maquinaria!),
                _ => BudgetMatrixComponentSourceBase.FromHerramienta(componente.Herramienta!),
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
