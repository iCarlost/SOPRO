using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Helpers;
using System;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Cambio de tipo de una fila del presupuesto (ADR-0001). La intención viaja como
    /// comando inmutable a <see cref="BudgetPersistenceService.ExecuteTypeChangeCommand"/>,
    /// que la aplica sobre un contexto NUEVO: actualiza el estado jerárquico de una
    /// fila persistida o crea el concepto de una fila nueva. La entidad y el Tag
    /// compartidos no se tocan antes de conocer el resultado; al confirmarse se
    /// sincronizan (o se adjuntan como <c>Unchanged</c>) con la instantánea confirmada
    /// sin una segunda escritura.
    /// </summary>
    public partial class FormPresupuesto
    {
        /// <summary>
        /// Clasificación del último cambio de tipo ejecutado como comando, o
        /// <c>null</c> si no llegó a enviarse (tipo vacío o trabajo pendiente en el
        /// contexto compartido). Observabilidad interna para las pruebas del
        /// ensamblado; no es API pública ni se muestra en la UI.
        /// </summary>
        internal BudgetOperationStatus? UltimoEstadoCambioTipo { get; private set; }

        /// <summary>
        /// Consume el cambio de la celda Tipo con la ruta de comando. Devuelve
        /// <c>false</c> solo cuando no hay un tipo que aplicar; en cualquier otro caso
        /// el evento queda atendido aquí y el llamador NO debe invocar
        /// <see cref="GuardarCambios"/>. Se expone a las pruebas del ensamblado porque
        /// asignar a la celda el mismo borrador no vuelve a disparar
        /// <c>CellValueChanged</c>.
        /// </summary>
        internal bool ProcesarCambioTipoConComando(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count)
                return false;

            string tipo = ObtenerCeldaPorNombreInterno(rowIndex, "Tipo")?.Value?.ToString()?.Trim()
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tipo))
                return false;

            var row = dgvPresupuesto.Rows[rowIndex];
            var existente = row.Tag as ConceptoPresupuesto;

            // Una entidad transitoria (Id 0) no puede representarse como "existente"
            // ni como "nueva" sin arriesgar un duplicado: no se escribe.
            if (existente != null && existente.Id <= 0)
                return true;

            UltimoEstadoCambioTipo = null;

            // Conservador: si el contexto compartido arrastra trabajo pendiente de
            // otras rutas, no se envía el comando ni se guarda nada.
            _context.ChangeTracker.DetectChanges();
            if (_context.ChangeTracker.HasChanges())
                return true;

            BudgetTypeChangeCommand command;
            try
            {
                command = new BudgetTypeChangeCommand(
                    _proyecto.Id,
                    existente?.Id,
                    existente is null ? null : BudgetConceptTypeBase.FromConcepto(existente),
                    tipo,
                    ObtenerCeldaTexto(rowIndex, "Clave"),
                    ObtenerCeldaTexto(rowIndex, "Descripcion"),
                    ObtenerCeldaTexto(rowIndex, "Unidad"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cambio de tipo: {ex.Message}");
                return true;
            }

            BudgetTypeChangeResult resultado;
            try
            {
                resultado = BudgetPersistenceService.ExecuteTypeChangeCommand(
                    _factory,
                    _context.DatabasePath,
                    command);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cambio de tipo: {ex.Message}");
                return true;
            }

            UltimoEstadoCambioTipo = resultado.Status;

            if (!resultado.IsCommitted || resultado.Confirmed is null)
            {
                // Conflicto, fallo o resultado incierto: el borrador (incluido el
                // Tipo) permanece visible y el tracker compartido queda intacto.
                System.Diagnostics.Debug.WriteLine(
                    $"Cambio de tipo no confirmado ({resultado.Status}): {resultado.Diagnostic}");
                return true;
            }

            AplicarCambioTipoConfirmado(rowIndex, existente, resultado.Confirmed);
            return true;
        }

        /// <summary>
        /// Vuelca lo CONFIRMADO en el grid: primero la entidad compartida (sincronizada
        /// o adjuntada como <c>Unchanged</c>) y luego las celdas visibles, todo bajo el
        /// guard de reentrada para no disparar otro comando.
        /// </summary>
        private void AplicarCambioTipoConfirmado(
            int rowIndex,
            ConceptoPresupuesto? existente,
            BudgetTypeChangeSnapshot confirmado)
        {
            var detalle = confirmado.Concept;

            _actualizandoDesdeComando = true;
            try
            {
                ConceptoPresupuesto entidad;
                if (existente is not null)
                {
                    SincronizarConceptoCompartido(existente, detalle);
                    entidad = existente;
                }
                else
                {
                    entidad = AdjuntarConceptoConfirmado(detalle);
                }

                var row = dgvPresupuesto.Rows[rowIndex];
                row.Tag = entidad;

                var unidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Unidad");
                var cantidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Cantidad");
                var puCell = ObtenerCeldaPorNombreInterno(rowIndex, "PrecioUnitario");
                var importeCell = ObtenerCeldaPorNombreInterno(rowIndex, "Importe");

                if (unidadCell != null)
                {
                    unidadCell.ReadOnly = detalle.EsAgrupador;
                    if (detalle.EsAgrupador) unidadCell.Value = string.Empty;
                }

                if (cantidadCell != null)
                {
                    cantidadCell.ReadOnly = detalle.EsAgrupador;
                    if (detalle.EsAgrupador) cantidadCell.Value = string.Empty;
                }

                if (detalle.EsAgrupador)
                {
                    // La UI deja vacíos los valores calculados al convertir en
                    // agrupador; el total confirmado se refleja en los agrupadores
                    // afectados, no como un importe de concepto.
                    if (puCell != null) puCell.Value = string.Empty;
                    if (importeCell != null) importeCell.Value = string.Empty;
                }

                var filasPorConcepto = ConstruirIndiceFilasPorConcepto();
                foreach (var agrupador in confirmado.Aggregators)
                {
                    var entidadAgrupador = ResolverEntidadCompartida(filasPorConcepto, agrupador.ConceptId);
                    if (entidadAgrupador is not null)
                        SincronizarEntidadCompartida(entidadAgrupador, agrupador);

                    if (!filasPorConcepto.TryGetValue(agrupador.ConceptId, out int filaAgrupador))
                        continue;

                    // La propia fila convertida en agrupador conserva la celda vacía
                    // que dejó el cambio de tipo; su total ya quedó en la entidad.
                    if (filaAgrupador == rowIndex && detalle.EsAgrupador)
                        continue;

                    var importeAgrupadorCell = ObtenerCeldaPorNombreInterno(filaAgrupador, "Importe");
                    if (importeAgrupadorCell != null)
                        importeAgrupadorCell.Value = agrupador.ImporteTotal.ToStringImporte();
                }
            }
            finally
            {
                _actualizandoDesdeComando = false;
            }

            dgvPresupuesto.InvalidateRow(rowIndex);
            ReasignarNumerosConceptos();
        }

        /// <summary>
        /// Copia la instantánea confirmada sobre la entidad COMPARTIDA y deja su
        /// entrada como <c>Unchanged</c> con OriginalValues iguales a los actuales:
        /// así ningún <c>SaveChanges</c> posterior vuelve a escribir esta fila.
        /// </summary>
        private void SincronizarConceptoCompartido(
            ConceptoPresupuesto entidad,
            BudgetConfirmedConceptDetail confirmado)
        {
            entidad.Clave = confirmado.Clave;
            entidad.Descripcion = confirmado.Descripcion;
            entidad.Unidad = confirmado.Unidad;
            entidad.EsAgrupador = confirmado.EsAgrupador;
            entidad.Nivel = confirmado.Nivel;
            entidad.Orden = confirmado.Orden;
            entidad.Cantidad = confirmado.Cantidad;
            entidad.MatrizId = confirmado.MatrizId;
            entidad.CostoDirectoUnitario = confirmado.CostoDirectoUnitario;
            entidad.CostoDirectoTotal = confirmado.CostoDirectoTotal;
            entidad.PrecioUnitario = confirmado.PrecioUnitario;
            entidad.ImporteTotal = confirmado.ImporteTotal;
            entidad.ColumnasPersonalizadasJSON = confirmado.ColumnasPersonalizadasJSON;
            entidad.Notas = confirmado.Notas;
            entidad.FechaModificacion = confirmado.FechaModificacion;

            var entry = _context.Entry(entidad);
            if (entry.State == EntityState.Detached) return;

            entry.OriginalValues.SetValues(entry.CurrentValues);
            entry.State = EntityState.Unchanged;
        }

        /// <summary>
        /// Materializa en el contexto COMPARTIDO el concepto creado por el comando,
        /// con el Id que asignó la base, y lo deja <c>Unchanged</c> para que el
        /// autoguardado heredado no lo inserte una segunda vez.
        /// </summary>
        private ConceptoPresupuesto AdjuntarConceptoConfirmado(BudgetConfirmedConceptDetail confirmado)
        {
            if (confirmado.ConceptId <= 0)
                throw new InvalidOperationException(
                    "El comando confirmó un concepto sin Id; no se puede adjuntar a la vista.");

            var entidad = new ConceptoPresupuesto
            {
                Id = confirmado.ConceptId,
                ProyectoId = confirmado.ProjectId,
                Clave = confirmado.Clave,
                Descripcion = confirmado.Descripcion,
                Unidad = confirmado.Unidad,
                EsAgrupador = confirmado.EsAgrupador,
                Nivel = confirmado.Nivel,
                Orden = confirmado.Orden,
                Cantidad = confirmado.Cantidad,
                MatrizId = confirmado.MatrizId,
                CostoDirectoUnitario = confirmado.CostoDirectoUnitario,
                CostoDirectoTotal = confirmado.CostoDirectoTotal,
                PrecioUnitario = confirmado.PrecioUnitario,
                ImporteTotal = confirmado.ImporteTotal,
                ColumnasPersonalizadasJSON = confirmado.ColumnasPersonalizadasJSON,
                Notas = confirmado.Notas,
                FechaCreacion = confirmado.FechaCreacion,
                FechaModificacion = confirmado.FechaModificacion
            };

            _context.Attach(entidad);
            return entidad;
        }
    }
}
