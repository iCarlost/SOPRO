using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Application.Services
{
    public static partial class BudgetPersistenceService
    {

        /// <summary>
        /// Asigna una matriz APU a un concepto YA existente (ADR-0001) sobre un contexto
        /// NUEVO por operación: recarga y revalida proyecto, concepto y matriz, construye
        /// el borrador con la aritmética canónica de
        /// <see cref="BudgetConceptAssignmentService"/>, recalcula los agrupadores con
        /// <see cref="PricePropagationService.ActualizarAgrupadores"/> (sin guardar) y
        /// confirma una sola vez. El llamador no transporta importes calculados.
        /// </summary>
        public static BudgetMatrixAssignmentResult ExecuteMatrixAssignmentCommand(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetMatrixAssignmentCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                context => StageMatrixAssignment(context, command));

            return new BudgetMatrixAssignmentResult(execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        private static BudgetWriteStep<BudgetMatrixAssignmentSnapshot> StageMatrixAssignment(
            SOPROContext context,
            BudgetMatrixAssignmentCommand command)
        {
            var proyecto = context.Proyectos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == command.ProjectId);

            if (proyecto == null)
                return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El proyecto {command.ProjectId} no existe en la base; la vista debe recargarse."));

            if (!MatchesProjectBase(proyecto, command.ExpectedProject))
                return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Los parámetros de cálculo del proyecto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var concepto = context.ConceptosPresupuesto
                .FirstOrDefault(c => c.Id == command.ConceptId && c.ProyectoId == command.ProjectId);

            if (concepto == null)
                return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El concepto {command.ConceptId} no pertenece al proyecto {command.ProjectId} " +
                        "en la base; la vista debe recargarse."));

            if (concepto.EsAgrupador || command.ExpectedConcept.EsAgrupador)
                return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "Un agrupador no admite asignación directa de matriz; no se guardó nada."));

            if (!MatchesConceptAssignmentBase(concepto, command.ExpectedConcept))
                return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El concepto {command.ConceptId} cambió en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var matriz = context.Matrices
                .FirstOrDefault(m => m.Id == command.MatrixId && m.ProyectoId == command.ProjectId);

            if (matriz == null)
                return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"La matriz {command.MatrixId} no pertenece al proyecto {command.ProjectId} " +
                        "en la base; la vista debe recargarse."));

            if (matriz.Tipo != TipoMatriz.APU)
                return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "Este tipo de matriz no se puede asignar directamente a un concepto del presupuesto. " +
                        "Seleccione una matriz de tipo APU."));

            // Única fuente de aritmética de la asignación: el servicio canónico.
            var draft = BudgetConceptAssignmentService.BuildDraftFromSelectedMatrix(
                proyecto,
                matriz,
                command.ProposedQuantity,
                command.ProposedKey,
                command.DescriptionOverride,
                command.UnitOverride);

            BudgetConceptAssignmentService.ApplyDraftValues(concepto, draft);

            PricePropagationService.ActualizarAgrupadores(context, new List<Proyecto> { proyecto });

            var agrupadores = context.ConceptosPresupuesto
                .Where(c => c.ProyectoId == command.ProjectId && c.EsAgrupador)
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            var snapshot = new BudgetMatrixAssignmentSnapshot(
                command.ProjectId,
                concepto.Id,
                concepto.Clave,
                concepto.Descripcion,
                concepto.Unidad,
                concepto.Cantidad,
                concepto.CostoDirectoUnitario,
                concepto.CostoDirectoTotal,
                concepto.PrecioUnitario,
                concepto.ImporteTotal,
                concepto.MatrizId,
                concepto.FechaModificacion,
                agrupadores.Select(BudgetConfirmedConceptRow.FromConcepto).ToArray());

            return BudgetWriteStep<BudgetMatrixAssignmentSnapshot>.Commit(
                snapshot, new[] { concepto.Id });
        }

        /// <summary>
        /// Aplica un bloque de celdas pegadas sobre filas YA persistidas del presupuesto
        /// (ADR-0001) sobre un contexto NUEVO por operación: revalida proyecto, bases
        /// esperadas de TODAS las filas, matrices APU y conceptos de origen (leídos sin
        /// tracking y contrastados con la instantánea que envió la vista), resuelve la
        /// clave como asignación de matriz, copia desde otra fila o edición simple,
        /// prepara todos los borradores antes de mutar cualquier destino y aplica después
        /// las columnas pegadas en el mismo orden que la pantalla
        /// (Clave → borrador → Descripción/Unidad/Cantidad). Los importes derivados se
        /// calculan aquí con los servicios canónicos, se recalculan los agrupadores una
        /// sola vez sobre la lista final y se confirma en UNA transacción/escritura. Un
        /// conflicto o fallo deja la base sin cambios; el reintento del mismo lote no
        /// duplica filas porque la operación solo actualiza.
        /// </summary>
        public static BudgetPasteResult ExecutePasteCommand(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetPasteCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                context => StagePaste(context, command));

            return new BudgetPasteResult(execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        private static BudgetWriteStep<BudgetPasteSnapshot> StagePaste(
            SOPROContext context,
            BudgetPasteCommand command)
        {
            var proyecto = context.Proyectos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == command.ProjectId);

            if (proyecto == null)
                return BudgetWriteStep<BudgetPasteSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El proyecto {command.ProjectId} no existe en la base; la vista debe recargarse."));

            if (!MatchesProjectBase(proyecto, command.ExpectedProject))
                return BudgetWriteStep<BudgetPasteSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Los parámetros de cálculo del proyecto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var ids = command.Rows.Select(r => r.ConceptId).ToList();

            // Se cargan TRACKEADAS: los valores aplicados y los totales de agrupadores
            // deben viajar en el MISMO SaveChanges que cierra la transacción.
            var conceptos = context.ConceptosPresupuesto
                .Where(c => ids.Contains(c.Id))
                .ToList();

            if (conceptos.Count != ids.Count)
                return BudgetWriteStep<BudgetPasteSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Una o más filas pegadas ya no existen en la base; la vista debe recargarse."));

            if (conceptos.Any(c => c.ProyectoId != command.ProjectId))
                return BudgetWriteStep<BudgetPasteSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Una o más filas pegadas no pertenecen al proyecto; la vista debe recargarse."));

            var porId = conceptos.ToDictionary(c => c.Id);

            foreach (var row in command.Rows)
            {
                var concepto = porId[row.ConceptId];

                if (!MatchesConceptAssignmentBase(concepto, row.ExpectedConcept))
                    return BudgetWriteStep<BudgetPasteSnapshot>.Abort(
                        BudgetOperationResult.Conflict(
                            $"La fila {row.ConceptId} cambió en la base desde que se cargó; " +
                            "la vista debe recargarse."));

                if (row.ExpectedConcept.EsAgrupador
                    && (row.KeyTarget.Kind != BudgetPasteKeyTargetKind.None
                        || row.Unidad is not null
                        || row.Cantidad.HasValue))
                {
                    return BudgetWriteStep<BudgetPasteSnapshot>.Abort(
                        BudgetOperationResult.Failed(
                            "Un agrupador solo admite Clave y Descripción al pegar; no se guardó nada."));
                }
            }

            var matrizIds = command.Rows
                .Where(r => r.KeyTarget.Kind == BudgetPasteKeyTargetKind.Matrix)
                .Select(r => r.KeyTarget.ReferenceId)
                .Distinct()
                .ToList();

            var matrices = matrizIds.Count == 0
                ? new Dictionary<int, Matriz>()
                : context.Matrices
                    .Where(m => matrizIds.Contains(m.Id))
                    .ToDictionary(m => m.Id);

            if (matrices.Count != matrizIds.Count)
                return BudgetWriteStep<BudgetPasteSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Una o más matrices de origen ya no existen en la base; la vista debe recargarse."));

            if (matrices.Values.Any(m => m.ProyectoId != command.ProjectId))
                return BudgetWriteStep<BudgetPasteSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Una o más matrices de origen no pertenecen al proyecto; la vista debe recargarse."));

            if (matrices.Values.Any(m => m.Tipo != TipoMatriz.APU))
                return BudgetWriteStep<BudgetPasteSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "Este tipo de matriz no se puede asignar directamente a un concepto del presupuesto. " +
                        "Seleccione una matriz de tipo APU."));

            var sourceIds = command.Rows
                .Where(r => r.KeyTarget.Kind == BudgetPasteKeyTargetKind.SourceConcept)
                .Select(r => r.KeyTarget.ReferenceId)
                .Distinct()
                .ToList();

            // Los orígenes se leen SIN tracking: el lote solo debe mutar los destinos y
            // la copia debe reflejar el estado que la base tenía al iniciar el pegado,
            // no los valores que otra fila del mismo lote deje en memoria.
            var conceptosOrigen = sourceIds.Count == 0
                ? new Dictionary<int, ConceptoPresupuesto>()
                : context.ConceptosPresupuesto
                    .AsNoTracking()
                    .Where(c => sourceIds.Contains(c.Id))
                    .ToDictionary(c => c.Id);

            if (conceptosOrigen.Count != sourceIds.Count)
                return BudgetWriteStep<BudgetPasteSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Una o más filas de origen ya no existen en la base; la vista debe recargarse."));

            if (conceptosOrigen.Values.Any(c => c.ProyectoId != command.ProjectId))
                return BudgetWriteStep<BudgetPasteSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Una o más filas de origen no pertenecen al proyecto; la vista debe recargarse."));

            foreach (var row in command.Rows)
            {
                if (row.KeyTarget.Kind != BudgetPasteKeyTargetKind.SourceConcept)
                    continue;

                var sourceSnapshot = row.KeyTarget.SourceConcept!;
                if (!MatchesConceptAssignmentBase(
                        conceptosOrigen[row.KeyTarget.ReferenceId],
                        sourceSnapshot.ExpectedConcept))
                {
                    return BudgetWriteStep<BudgetPasteSnapshot>.Abort(
                        BudgetOperationResult.Conflict(
                            $"La fila de origen {row.KeyTarget.ReferenceId} cambió en la base desde que se cargó; " +
                            "la vista debe recargarse."));
                }
            }

            // Todos los borradores se preparan ANTES de mutar cualquier destino. Así una
            // fila que es origen de una copia conserva el estado inicial del pegado
            // aunque ella misma sea destino de otra fila del mismo lote.
            var borradores = new Dictionary<int, BudgetConceptAssignmentDraft>(command.Rows.Count);
            foreach (var row in command.Rows)
            {
                if (row.KeyTarget.Kind == BudgetPasteKeyTargetKind.Matrix)
                {
                    borradores[row.ConceptId] = BudgetConceptAssignmentService.BuildDraftFromSelectedMatrix(
                        proyecto,
                        matrices[row.KeyTarget.ReferenceId],
                        porId[row.ConceptId].Cantidad,
                        row.Clave,
                        descripcionActual: null,
                        unidadActual: null);
                }
                else if (row.KeyTarget.Kind == BudgetPasteKeyTargetKind.SourceConcept)
                {
                    var sourceSnapshot = BudgetPasteSourceConceptSnapshot.FromConcepto(
                        conceptosOrigen[row.KeyTarget.ReferenceId]);

                    borradores[row.ConceptId] = BudgetConceptAssignmentService.BuildDraftFromConcept(
                        proyecto,
                        row.Clave!,
                        new ConceptoPresupuesto
                        {
                            Id = sourceSnapshot.ConceptId,
                            ProyectoId = command.ProjectId,
                            Clave = sourceSnapshot.Clave,
                            Descripcion = sourceSnapshot.Descripcion,
                            Unidad = sourceSnapshot.Unidad,
                            Cantidad = sourceSnapshot.Cantidad,
                            CostoDirectoUnitario = sourceSnapshot.CostoDirectoUnitario,
                            MatrizId = sourceSnapshot.MatrizId
                        });
                }
            }

            foreach (var row in command.Rows)
            {
                var concepto = porId[row.ConceptId];

                if (borradores.TryGetValue(row.ConceptId, out var draft))
                {
                    BudgetConceptAssignmentService.ApplyDraftValues(concepto, draft);
                }
                else if (row.Clave is not null)
                {
                    concepto.Clave = row.Clave;
                }

                // Las columnas pegadas se aplican DESPUÉS del borrador de la clave, igual
                // que la secuencia de pantalla: los textos/cantidad pegados mandan.
                if (row.Descripcion is not null)
                    concepto.Descripcion = row.Descripcion;

                if (row.Unidad is not null)
                    concepto.Unidad = row.Unidad;

                if (row.Cantidad is decimal cantidad)
                {
                    var quantity = BudgetRowEditFlowService.HandleQuantityCellChange(
                        proyecto, concepto, cantidad);

                    if (quantity.HasChanges)
                    {
                        concepto.Cantidad = quantity.Cantidad;
                        concepto.PrecioUnitario = quantity.PrecioUnitario;
                        concepto.ImporteTotal = quantity.Importe;
                        concepto.CostoDirectoTotal = BudgetPricingService.MultiplyUsingDisplayPrecision(
                            proyecto, quantity.Cantidad, concepto.CostoDirectoUnitario);
                    }
                    else
                    {
                        // Sin matriz no hay base para recalcular: se conserva el
                        // comportamiento heredado de la edición simple de la cantidad.
                        concepto.Cantidad = cantidad;
                    }
                }

                concepto.FechaModificacion = DateTime.Now;
            }

            // UN solo recálculo de agrupadores sobre la lista final ya preparada: sus
            // totales viajan en la MISMA escritura que las filas pegadas.
            PricePropagationService.ActualizarAgrupadores(context, new List<Proyecto> { proyecto });

            var agrupadores = context.ConceptosPresupuesto
                .Where(c => c.ProyectoId == command.ProjectId && c.EsAgrupador)
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            var snapshot = new BudgetPasteSnapshot(
                command.ProjectId,
                command.Rows.Select(r => BudgetPasteRowSnapshot.FromConcepto(porId[r.ConceptId])).ToArray(),
                agrupadores.Select(BudgetConfirmedConceptRow.FromConcepto).ToArray());

            return BudgetWriteStep<BudgetPasteSnapshot>.Commit(snapshot, ids);
        }

        /// <summary>
        /// Importa un lote de renglones NUEVOS al presupuesto (ADR-0001) sobre un
        /// contexto NUEVO por operación: revalida el proyecto y su base de cálculo,
        /// calcula la posición inicial a partir del máximo Orden persistido, inserta
        /// todas las filas y recalcula los agrupadores sobre la lista final
        /// (existentes + nuevos) en memoria, ANTES del único SaveChanges, de modo que
        /// esa misma escritura persiste inserciones y totales. Después del SaveChanges
        /// solo se materializa la instantánea con los Ids generados y el mapeo a la
        /// fila de origen, sin mutar entidades. Un conflicto o fallo deja la base sin
        /// cambios; el reintento de la misma intención no duplica filas.
        /// </summary>
        public static BudgetImportResult ExecuteImportCommand(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetImportCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                context => StageImport(context, command));

            return new BudgetImportResult(execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        private static BudgetWriteStep<BudgetImportSnapshot> StageImport(
            SOPROContext context,
            BudgetImportCommand command)
        {
            var proyecto = context.Proyectos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == command.ProjectId);

            if (proyecto == null)
                return BudgetWriteStep<BudgetImportSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El proyecto {command.ProjectId} no existe en la base; la vista debe recargarse."));

            if (!MatchesProjectBase(proyecto, command.ExpectedProject))
                return BudgetWriteStep<BudgetImportSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Los parámetros de cálculo del proyecto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            // Se cargan TRACKEADAS: el recálculo previo al SaveChanges modifica los
            // agrupadores existentes y esa modificación debe viajar en la MISMA
            // escritura que las inserciones.
            var existentes = context.ConceptosPresupuesto
                .Where(c => c.ProyectoId == command.ProjectId)
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            int ordenBase = existentes.Count == 0
                ? -1
                : existentes.Max(c => c.Orden);

            var nuevos = new List<ConceptoPresupuesto>(command.Rows.Count);
            int offset = 1;

            foreach (var fila in command.Rows)
            {
                nuevos.Add(new ConceptoPresupuesto
                {
                    ProyectoId = command.ProjectId,
                    Clave = fila.Clave,
                    Descripcion = fila.Descripcion,
                    Unidad = fila.Unidad,
                    Cantidad = fila.Cantidad,
                    EsAgrupador = fila.EsAgrupador,
                    Nivel = fila.Nivel,
                    Orden = ordenBase + offset++,
                    MatrizId = null,
                    CostoDirectoUnitario = 0m,
                    CostoDirectoTotal = 0m,
                    Indirectos = 0m,
                    Financiamiento = 0m,
                    Utilidad = 0m,
                    CargosAdicionales = 0m,
                    PrecioUnitario = 0m,
                    ImporteTotal = 0m,
                    ColumnasPersonalizadasJSON = string.Empty,
                    Notas = string.Empty,
                    FechaCreacion = DateTime.Now,
                    FechaModificacion = DateTime.Now
                });
            }

            context.ConceptosPresupuesto.AddRange(nuevos);

            // El recálculo ocurre ANTES del único SaveChanges, sobre la lista final
            // existentes + nuevos ya en memoria. Así la misma escritura persiste las
            // inserciones y los totales de los agrupadores; no hay segunda escritura.
            var listaFinal = existentes
                .Concat(nuevos)
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            PricePropagationService.ActualizarAgrupadores(proyecto, listaFinal);

            // Los Ids solo existen después del SaveChanges: la factoría solo
            // materializa la instantánea sobre la lista ya calculada, sin tocar
            // entidades ni volver a recalcular.
            return BudgetWriteStep<BudgetImportSnapshot>.CommitAfterSave(
                () => BuildImportSnapshot(command, nuevos, listaFinal),
                () => nuevos.Select(c => c.Id).ToArray());
        }

        private static BudgetImportSnapshot BuildImportSnapshot(
            BudgetImportCommand command,
            IReadOnlyList<ConceptoPresupuesto> nuevos,
            IReadOnlyList<ConceptoPresupuesto> listaFinal)
        {
            var filas = new List<BudgetConfirmedImportedRow>(nuevos.Count);
            for (int i = 0; i < nuevos.Count; i++)
                filas.Add(BudgetConfirmedImportedRow.FromConcepto(nuevos[i], command.Rows[i].SourceRowNumber));

            var agrupadores = listaFinal
                .Where(c => c.EsAgrupador)
                .Select(BudgetConfirmedConceptRow.FromConcepto)
                .ToArray();

            return new BudgetImportSnapshot(command.ProjectId, filas, agrupadores);
        }
    }
}
