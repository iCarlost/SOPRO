using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Application.Services
{
    public static partial class BudgetPersistenceService
    {

        /// <summary>
        /// RUTA HEREDADA (temporal): escribe en el contexto COMPARTIDO y no controla
        /// conflictos ni confirma resultados. Se conserva solo porque WinForms aún la
        /// consume; la ruta vigente es <see cref="ExecuteQuantityEditCommand"/>. Se
        /// eliminará al completar la migración (ADR-0001).
        /// </summary>
        public static void ApplyAutoSaveChanges(SOPROContext context, IEnumerable<BudgetConceptRowDto> rows)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            foreach (var row in rows.Where(r => r.ExistingConceptId.HasValue))
            {
                var enBd = context.ConceptosPresupuesto.Find(row.ExistingConceptId!.Value);
                if (enBd == null) continue;

                enBd.Descripcion = row.Descripcion;
                enBd.Clave = row.Clave;
                enBd.Unidad = row.Unidad;
                enBd.Cantidad = row.Cantidad;
                enBd.EsAgrupador = row.EsAgrupador;
                enBd.CostoDirectoUnitario = row.CostoDirectoUnitario;
                enBd.CostoDirectoTotal = row.CostoDirectoTotal;
                enBd.PrecioUnitario = row.PrecioUnitario;
                enBd.ImporteTotal = row.ImporteTotal;
                enBd.MatrizId = row.MatrizId;
                enBd.FechaModificacion = DateTime.Now;
            }

            context.SaveChanges();
        }

        private static bool MatchesProjectBase(Proyecto proyecto, BudgetProjectCalculationBase expected)
        {
            return proyecto.PorcentajeIndirectosCentral == expected.PorcentajeIndirectosCentral
                && proyecto.PorcentajeIndirectosCampo == expected.PorcentajeIndirectosCampo
                && proyecto.PorcentajeFinanciamiento == expected.PorcentajeFinanciamiento
                && proyecto.PorcentajeUtilidad == expected.PorcentajeUtilidad
                && proyecto.PorcentajeCargosAdicionales == expected.PorcentajeCargosAdicionales
                && proyecto.PorcentajeIVA == expected.PorcentajeIVA
                && string.Equals(proyecto.ModoCalculoPorcentajes, expected.ModoCalculoPorcentajes,
                    StringComparison.OrdinalIgnoreCase)
                && proyecto.DecimalesCantidad == expected.DecimalesCantidad
                && proyecto.DecimalesImporte == expected.DecimalesImporte
                && proyecto.DecimalesPorcentaje == expected.DecimalesPorcentaje;
        }

        private static bool MatchesConceptBase(ConceptoPresupuesto concepto, BudgetConceptQuantityBase expected)
        {
            return concepto.Cantidad == expected.Cantidad
                && concepto.CostoDirectoUnitario == expected.CostoDirectoUnitario
                && concepto.MatrizId == expected.MatrizId
                && concepto.EsAgrupador == expected.EsAgrupador;
        }

        private static bool MatchesConceptAssignmentBase(
            ConceptoPresupuesto concepto,
            BudgetConceptAssignmentBase expected)
        {
            return concepto.EsAgrupador == expected.EsAgrupador
                && string.Equals(concepto.Clave ?? string.Empty, expected.Clave, StringComparison.Ordinal)
                && string.Equals(concepto.Descripcion ?? string.Empty, expected.Descripcion, StringComparison.Ordinal)
                && string.Equals(concepto.Unidad ?? string.Empty, expected.Unidad, StringComparison.Ordinal)
                && concepto.Cantidad == expected.Cantidad
                && concepto.MatrizId == expected.MatrizId
                && concepto.CostoDirectoUnitario == expected.CostoDirectoUnitario
                && concepto.CostoDirectoTotal == expected.CostoDirectoTotal
                && concepto.PrecioUnitario == expected.PrecioUnitario
                && concepto.ImporteTotal == expected.ImporteTotal;
        }

        public static BudgetSaveResult ReplaceProjectConcepts(SOPROContext context, int projectId, IEnumerable<BudgetConceptRowDto> rows)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var concepts = BuildConcepts(projectId, rows).ToList();
            var conceptosExistentes = context.ConceptosPresupuesto.Where(c => c.ProyectoId == projectId);
            context.ConceptosPresupuesto.RemoveRange(conceptosExistentes);
            context.ConceptosPresupuesto.AddRange(concepts);
            context.SaveChanges();

            return new BudgetSaveResult { SavedConceptCount = concepts.Count };
        }

        public static IEnumerable<ConceptoPresupuesto> BuildConcepts(int projectId, IEnumerable<BudgetConceptRowDto> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            foreach (var row in rows.Where(r => r.HasContent).OrderBy(r => r.Orden))
            {
                yield return new ConceptoPresupuesto
                {
                    ProyectoId = projectId,
                    Clave = row.Clave,
                    Descripcion = row.Descripcion,
                    EsAgrupador = row.EsAgrupador,
                    Nivel = ResolveLevelFromType(row.Tipo),
                    Orden = row.Orden,
                    Unidad = row.Unidad,
                    ColumnasPersonalizadasJSON = string.Empty,
                    Notas = string.Empty,
                    Cantidad = row.Cantidad,
                    MatrizId = row.EsAgrupador ? null : row.MatrizId,
                    CostoDirectoUnitario = row.EsAgrupador ? 0 : row.CostoDirectoUnitario,
                    CostoDirectoTotal = row.CostoDirectoTotal,  // agrupadores guardan su total calculado
                    PrecioUnitario = row.EsAgrupador ? 0 : row.PrecioUnitario,
                    ImporteTotal = row.ImporteTotal,            // agrupadores guardan su total calculado
                };
            }
        }

        public static int ResolveLevelFromType(string? tipo)
        {
            return tipo switch
            {
                "Capitulo" => 0,
                "Subcapitulo" => 1,
                "Nivel 1" => 2,
                "Nivel 2" => 3,
                "Nivel 3" => 4,
                _ => 5
            };
        }
    }

    /// <summary>Resultado de un ejecutor de escritura: clasificación, instantánea y diagnóstico técnico.</summary>
    internal sealed class BudgetWriteExecution<TConfirmed> where TConfirmed : class
    {
        private BudgetWriteExecution(BudgetOperationResult result, TConfirmed? confirmed, string? diagnostic)
        {
            Result = result;
            Confirmed = confirmed;
            Diagnostic = diagnostic;
        }

        public BudgetOperationResult Result { get; }

        public TConfirmed? Confirmed { get; }

        public string? Diagnostic { get; }

        internal static BudgetWriteExecution<TConfirmed> Committed(
            TConfirmed confirmed,
            IReadOnlyList<int> confirmedConceptIds,
            string? diagnostic)
            => new(BudgetOperationResult.Committed(confirmedConceptIds), confirmed, diagnostic);

        internal static BudgetWriteExecution<TConfirmed> Aborted(BudgetOperationResult result, string? diagnostic)
            => new(result, null, diagnostic);

        internal static BudgetWriteExecution<TConfirmed> Failed(string message, string? diagnostic)
            => new(BudgetOperationResult.Failed(message), null, diagnostic);

        internal static BudgetWriteExecution<TConfirmed> OutcomeUnknown(string message, string? diagnostic)
            => new(BudgetOperationResult.OutcomeUnknown(message), null, diagnostic);
    }

    /// <summary>
    /// Decisión de la fase de aplicación de un comando: confirmar con una instantánea o
    /// detenerse con un resultado ya clasificado (conflicto o fallo sin escritura).
    /// </summary>
    internal sealed class BudgetWriteStep<TConfirmed> where TConfirmed : class
    {
        private BudgetWriteStep(
            bool shouldCommit,
            BudgetOperationResult? stopResult,
            TConfirmed? confirmed,
            IReadOnlyList<int> confirmedConceptIds,
            string? diagnostic,
            Func<TConfirmed>? confirmedFactory = null,
            Func<IReadOnlyList<int>>? confirmedConceptIdsFactory = null)
        {
            ShouldCommit = shouldCommit;
            StopResult = stopResult;
            Confirmed = confirmed;
            ConfirmedConceptIds = confirmedConceptIds;
            Diagnostic = diagnostic;
            ConfirmedFactory = confirmedFactory;
            ConfirmedConceptIdsFactory = confirmedConceptIdsFactory;
        }

        public bool ShouldCommit { get; }

        public BudgetOperationResult? StopResult { get; }

        public TConfirmed? Confirmed { get; }

        public IReadOnlyList<int> ConfirmedConceptIds { get; }

        public string? Diagnostic { get; }

        /// <summary>
        /// Materializa la instantánea después del SaveChanges; necesaria cuando el
        /// Id de una entidad nueva solo existe una vez que la base lo asignó.
        /// </summary>
        public Func<TConfirmed>? ConfirmedFactory { get; }

        /// <summary>Ids confirmados que dependen de Ids generados por la base.</summary>
        public Func<IReadOnlyList<int>>? ConfirmedConceptIdsFactory { get; }

        internal static BudgetWriteStep<TConfirmed> Commit(
            TConfirmed confirmed,
            IReadOnlyList<int> confirmedConceptIds)
            => new(true, null, confirmed, confirmedConceptIds ?? Array.Empty<int>(), null);

        internal static BudgetWriteStep<TConfirmed> CommitAfterSave(
            Func<TConfirmed> confirmedFactory,
            Func<IReadOnlyList<int>> confirmedConceptIdsFactory)
            => new(true, null, null, Array.Empty<int>(), null,
                confirmedFactory ?? throw new ArgumentNullException(nameof(confirmedFactory)),
                confirmedConceptIdsFactory ?? throw new ArgumentNullException(nameof(confirmedConceptIdsFactory)));

        internal static BudgetWriteStep<TConfirmed> Abort(BudgetOperationResult result, string? diagnostic = null)
            => new(false, result ?? throw new ArgumentNullException(nameof(result)), null,
                Array.Empty<int>(), diagnostic);
    }

    /// <summary>
    /// Ejecutor ÚNICO de comandos de escritura del presupuesto (ADR-0001): contexto nuevo
    /// por operación creado por <see cref="IProjectDbContextFactory"/>, transacción corta
    /// opcional, aplicación delegada en el handler, exactamente un SaveChanges y
    /// clasificación de conflicto, fallo revertido y resultado incierto. El diagnóstico
    /// técnico viaja separado del mensaje seguro para el usuario.
    /// </summary>
    internal static class BudgetWriteExecutor
    {
        internal static BudgetWriteExecution<TConfirmed> Execute<TConfirmed>(
            IProjectDbContextFactory factory,
            string databasePath,
            bool useTransaction,
            Func<SOPROContext, BudgetWriteStep<TConfirmed>> stage)
            where TConfirmed : class
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            if (stage == null) throw new ArgumentNullException(nameof(stage));

            SOPROContext? context = null;
            IDbContextTransaction? transaction = null;

            try
            {
                context = factory.Create(databasePath);
                if (useTransaction)
                    transaction = context.Database.BeginTransaction();
            }
            catch (Exception ex)
            {
                DisposeQuietly(transaction);
                DisposeQuietly(context);
                return BudgetWriteExecution<TConfirmed>.Failed(MensajeFalloRevertido, Describe(ex));
            }

            BudgetWriteStep<TConfirmed> step;
            try
            {
                step = stage(context);
            }
            catch (Exception ex)
            {
                // Fallo al leer o aplicar valores: nada de este comando llegó a SaveChanges.
                RollbackQuietly(transaction);
                DisposeQuietly(transaction);
                DisposeQuietly(context);
                return BudgetWriteExecution<TConfirmed>.Failed(MensajeFalloRevertido, Describe(ex));
            }

            if (!step.ShouldCommit)
            {
                DisposeQuietly(transaction);
                DisposeQuietly(context);
                return BudgetWriteExecution<TConfirmed>.Aborted(
                    step.StopResult!, step.Diagnostic);
            }

            try
            {
                context.SaveChanges();
            }
            catch (Exception ex)
            {
                string? diagnostic = Describe(ex);
                try
                {
                    // Sin transacción explícita, SaveChanges ya revirtió la suya propia.
                    transaction?.Rollback();
                }
                catch (Exception rollbackFailure)
                {
                    DisposeQuietly(transaction);
                    DisposeQuietly(context);
                    return BudgetWriteExecution<TConfirmed>.OutcomeUnknown(
                        MensajeReversionIncierta,
                        diagnostic + " | rollback: " + Describe(rollbackFailure));
                }

                DisposeQuietly(transaction);
                DisposeQuietly(context);
                return BudgetWriteExecution<TConfirmed>.Failed(MensajeFalloRevertido, diagnostic);
            }

            TConfirmed confirmed;
            IReadOnlyList<int> confirmedConceptIds;
            try
            {
                confirmed = step.ConfirmedFactory is not null
                    ? step.ConfirmedFactory()
                    : step.Confirmed!;
                confirmedConceptIds = step.ConfirmedConceptIdsFactory is not null
                    ? step.ConfirmedConceptIdsFactory()
                    : step.ConfirmedConceptIds;
            }
            catch (Exception ex)
            {
                // La escritura ya ocurrió dentro de la transacción: si la
                // instantánea no se puede materializar, se revierte para no
                // confirmar una operación que el llamador no puede reconciliar.
                string? diagnostic = Describe(ex);
                if (transaction == null)
                {
                    DisposeQuietly(context);
                    return BudgetWriteExecution<TConfirmed>.OutcomeUnknown(
                        MensajeCommitIncierto, diagnostic);
                }

                try
                {
                    transaction.Rollback();
                }
                catch (Exception rollbackFailure)
                {
                    DisposeQuietly(transaction);
                    DisposeQuietly(context);
                    return BudgetWriteExecution<TConfirmed>.OutcomeUnknown(
                        MensajeReversionIncierta,
                        diagnostic + " | rollback: " + Describe(rollbackFailure));
                }

                DisposeQuietly(transaction);
                DisposeQuietly(context);
                return BudgetWriteExecution<TConfirmed>.Failed(MensajeFalloRevertido, diagnostic);
            }

            if (transaction != null)
            {
                try
                {
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    DisposeQuietly(transaction);
                    DisposeQuietly(context);
                    return BudgetWriteExecution<TConfirmed>.OutcomeUnknown(
                        MensajeCommitIncierto, Describe(ex));
                }
            }

            try
            {
                transaction?.Dispose();
            }
            catch (Exception ex)
            {
                DisposeQuietly(context);
                return BudgetWriteExecution<TConfirmed>.OutcomeUnknown(
                    MensajeCommitIncierto, Describe(ex));
            }

            try
            {
                context.Dispose();
            }
            catch (Exception ex)
            {
                // El commit ya ocurrió pero la operación no se cerró de forma segura:
                // no se afirma ni éxito ni fallo (ADR-0001: reconciliar antes de reintentar).
                return BudgetWriteExecution<TConfirmed>.OutcomeUnknown(
                    MensajeCommitIncierto, Describe(ex));
            }

            return BudgetWriteExecution<TConfirmed>.Committed(
                confirmed, confirmedConceptIds, diagnostic: null);
        }

        private static void RollbackQuietly(IDbContextTransaction? transaction)
        {
            if (transaction == null) return;

            try { transaction.Rollback(); } catch { }
        }

        private static void DisposeQuietly(IDbContextTransaction? transaction)
        {
            if (transaction == null) return;

            try { transaction.Dispose(); } catch { }
        }

        private static void DisposeQuietly(SOPROContext? context)
        {
            if (context == null) return;

            try { context.Dispose(); } catch { }
        }

        private static string Describe(Exception exception) => exception.ToString();

        private const string MensajeFalloRevertido =
            "No se pudo guardar el presupuesto; no se confirmó ningún cambio. Puede reintentar.";

        private const string MensajeReversionIncierta =
            "No se pudo guardar el presupuesto y no se pudo confirmar que la base haya quedado sin cambios. " +
            "Reconcilie con la base antes de reintentar.";

        private const string MensajeCommitIncierto =
            "No se pudo confirmar si el guardado del presupuesto quedó en la base. " +
            "Reconcilie con la base antes de reintentar.";
    }
}
