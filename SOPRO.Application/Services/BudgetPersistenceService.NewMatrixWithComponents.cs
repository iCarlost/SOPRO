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
        /// Primera alta de una matriz nueva desde el panel embebido: crea la matriz
        /// APU con su clave real, inserta el lote completo de componentes, la vincula
        /// al concepto persistido de origen y recalcula concepto/agrupadores con los
        /// servicios canónicos. Contexto nuevo, una transacción y un único commit;
        /// cualquier conflicto o fallo revierte matriz, componentes, vínculo y precios.
        /// No es una ruta de autoguardado por tecla: solo se invoca con el primer lote
        /// no vacío y válido.
        /// </summary>
        public static BudgetNewMatrixWithComponentsResult ExecuteNewMatrixWithComponentsCommand(
            IProjectDbContextFactory factory,
            string databasePath,
            BudgetNewMatrixWithComponentsCommand command)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            ArgumentNullException.ThrowIfNull(command);

            var execution = BudgetWriteExecutor.ExecuteWorkTransaction(
                factory,
                databasePath,
                context => StageNewMatrixWithComponents(context, command));

            return new BudgetNewMatrixWithComponentsResult(
                execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        private static BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot> StageNewMatrixWithComponents(
            SOPROContext context,
            BudgetNewMatrixWithComponentsCommand command)
        {
            if (command.Additions.Count == 0)
                return BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "Agregue al menos un componente antes de crear la matriz; no se guardó nada."));

            if (command.Tipo != TipoMatriz.APU)
                return BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "Solo una matriz APU se puede vincular a un concepto del presupuesto; no se guardó nada."));

            if (command.Clave.Length > 50)
                return BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "La clave no puede exceder 50 caracteres; no se guardó nada."));

            if (command.Descripcion.Length > 2000)
                return BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "La descripción no puede exceder 2000 caracteres; no se guardó nada."));

            if (command.Unidad.Length > 20)
                return BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "La unidad no puede exceder 20 caracteres; no se guardó nada."));

            var proyecto = context.Proyectos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == command.ProjectId);

            if (proyecto == null)
                return BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El proyecto {command.ProjectId} no existe en la base; la vista debe recargarse."));

            if (!MatchesProjectBase(proyecto, command.ExpectedProject))
                return BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "Los parámetros de cálculo del proyecto cambiaron en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var concepto = context.ConceptosPresupuesto
                .FirstOrDefault(c => c.Id == command.ConceptId && c.ProyectoId == command.ProjectId);

            if (concepto == null)
                return BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El concepto {command.ConceptId} no pertenece al proyecto {command.ProjectId} " +
                        "en la base; la vista debe recargarse."));

            if (concepto.EsAgrupador || command.ExpectedConcept.EsAgrupador)
                return BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot>.Abort(
                    BudgetOperationResult.Failed(
                        "Un agrupador no admite asignación directa de matriz; no se guardó nada."));

            if (concepto.MatrizId.HasValue || command.ExpectedConcept.MatrizId.HasValue)
            {
                return BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El concepto {command.ConceptId} ya tiene una matriz asignada; " +
                        "no se puede reemplazar el vínculo creando una matriz nueva."));
            }

            if (!MatchesConceptAssignmentBase(concepto, command.ExpectedConcept))
                return BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El concepto {command.ConceptId} cambió en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            var filas = LoadOrderedRows(context, command.ProjectId);
            if (!MatchesAssignmentRowset(filas, command.ExpectedRowset))
                return BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        "El orden de las filas del presupuesto cambió en la base desde que se cargó; " +
                        "la vista debe recargarse."));

            string claveNormalizada = command.Clave.Trim().ToUpperInvariant();
            var clavesProyecto = context.Matrices
                .AsNoTracking()
                .Where(m => m.ProyectoId == command.ProjectId)
                .Select(m => m.Clave)
                .ToList();

            if (clavesProyecto.Any(clave => string.Equals(
                    clave?.Trim(), claveNormalizada, StringComparison.OrdinalIgnoreCase)))
            {
                return BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"Ya existe una matriz con la clave {claveNormalizada} en el proyecto; no se guardó nada."));
            }

            // Primera pasada: validar TODO el lote antes de crear la matriz.
            var validadas = new List<ValidatedAddition>(command.Additions.Count);
            foreach (var item in command.Additions)
            {
                var error = TryLoadCatalogSource(context, command.ProjectId, item, out var validada);
                if (error != null)
                    return BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot>.Abort(error);

                validadas.Add(validada!);
            }

            var matriz = new Matriz
            {
                ProyectoId = command.ProjectId,
                Clave = claveNormalizada,
                Descripcion = command.Descripcion.Trim(),
                Unidad = command.Unidad.Trim(),
                Tipo = TipoMatriz.APU,
                CostoDirecto = 0m,
                Origen = OrigenInsumo.Proyecto,
                Notas = string.Empty,
                FechaCreacion = DateTime.Now,
                FechaModificacion = DateTime.Now
            };

            // La matriz debe existir para que los componentes tengan FK y para que el
            // cierre canónico pueda resolver la identidad. Sigue dentro de la misma
            // transacción: un fallo posterior revierte también esta inserción.
            context.Matrices.Add(matriz);
            context.SaveChanges();

            var nuevos = new List<ComponenteMatriz>(validadas.Count);
            int orden = 0;
            foreach (var validada in validadas)
            {
                var componente = validada.CreateComponent(matriz.Id, ++orden);
                componente.Matriz = matriz;
                context.ComponentesMatriz.Add(componente);
                nuevos.Add(componente);
            }

            var afectadas = PricePropagationService.RecalcularCierreAuxiliarSinGuardar(
                context, new[] { matriz.Id }, command.ProjectId);

            // El concepto se asigna después del recálculo para que el borrador use el
            // CostoDirecto canónico de la matriz recién recalculada.
            var draft = BudgetConceptAssignmentService.BuildDraftFromSelectedMatrix(
                proyecto,
                matriz,
                command.ProposedQuantity,
                concepto.Clave,
                concepto.Descripcion,
                concepto.Unidad);

            BudgetConceptAssignmentService.ApplyDraftValues(concepto, draft);
            PricePropagationService.ActualizarAgrupadores(context, new List<Proyecto> { proyecto });

            context.SaveChanges();

            var filasFinales = LoadOrderedRows(context, command.ProjectId);
            var additionSnapshot = BuildMatrixComponentAdditionSnapshot(
                context, command.ProjectId, proyecto, matriz, afectadas, nuevos);
            var assignmentSnapshot = BuildMatrixAssignmentSnapshot(
                command.ProjectId, concepto, filasFinales, createdNewRow: false);

            return BudgetWriteStep<BudgetNewMatrixWithComponentsSnapshot>.Commit(
                new BudgetNewMatrixWithComponentsSnapshot(
                    command.ProjectId, additionSnapshot, assignmentSnapshot),
                new[] { concepto.Id });
        }
    }
}
