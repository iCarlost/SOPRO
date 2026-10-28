using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Undo;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Error OBSERVABLE de persistencia de formato: nunca se traga ni se convierte
    /// en un repintado de éxito. <see cref="Status"/> permite distinguir un fallo
    /// revertido (se puede reintentar) de un resultado incierto (hay que recargar).
    /// </summary>
    internal sealed class FormatoCeldasPersistenceException : InvalidOperationException
    {
        public FormatoCeldasPersistenceException(
            BudgetOperationStatus status,
            string? message,
            string? diagnostic)
            : base(string.IsNullOrWhiteSpace(message) ? MensajePorEstado(status) : message)
        {
            Status = status;
            Diagnostic = diagnostic;
        }

        public BudgetOperationStatus Status { get; }

        /// <summary>Detalle técnico; no se muestra al usuario.</summary>
        public string? Diagnostic { get; }

        public bool RequiereReconciliacion => Status == BudgetOperationStatus.OutcomeUnknown;

        private static string MensajePorEstado(BudgetOperationStatus status) => status switch
        {
            BudgetOperationStatus.Conflict =>
                "El formato cambió en la base desde que se cargó. Recargue la vista antes de reintentar.",
            BudgetOperationStatus.OutcomeUnknown =>
                "No se pudo confirmar si el formato quedó guardado. Recargue el formato antes de reintentar.",
            _ => "No se pudo guardar el formato; no se confirmó ningún cambio. Puede reintentar.",
        };
    }

    /// <summary>
    /// Puente único entre los formularios y
    /// <see cref="FormatoCeldasCommandService"/>: propaga la ruta REAL de la base,
    /// traduce el lote antes/después a comandos con base esperada y convierte un
    /// resultado no confirmado en una excepción observable. En
    /// <see cref="BudgetOperationStatus.OutcomeUnknown"/> suspende el historial de
    /// undo de inmediato (sin revertir ni reintentar).
    /// </summary>
    internal static class FormatoCeldasPersistence
    {
        private static readonly IProjectDbContextFactory DefaultFactory = new ProjectDbContextFactory();

        /// <summary>
        /// Persiste el lote (estado "antes" o "después") y SOLO retorna si quedó
        /// confirmado. <paramref name="factory"/> es un seam de pruebas; en
        /// producción se usa la fábrica por defecto (contexto nuevo por operación).
        /// </summary>
        public static void Aplicar(
            string databasePath,
            int proyectoId,
            string tipoReporte,
            IReadOnlyList<CambioFormatoCeldaCommand> comandos,
            UndoManager undoManager,
            IProjectDbContextFactory? factory = null)
        {
            var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                factory ?? DefaultFactory, databasePath, proyectoId, tipoReporte, comandos);

            if (resultado.IsCommitted) return;

            if (resultado.Status == BudgetOperationStatus.OutcomeUnknown)
                undoManager.SuspendForReconciliation();

            throw new FormatoCeldasPersistenceException(
                resultado.Status, resultado.Message, resultado.Diagnostic);
        }

        /// <summary>
        /// Tras una lectura FRESCA de overlays, libera el bloqueo por resultado
        /// incierto y descarta el historial obsoleto. El llamador ya reemplazó la
        /// caché en memoria antes de invocarlo.
        /// </summary>
        public static void ReconciliarSiCorresponde(UndoManager undoManager)
        {
            if (undoManager.RequiresReconciliation)
                undoManager.ReconciliarTrasLecturaFresca();
        }

        /// <summary>
        /// Muestra un error de formato y devuelve <c>true</c> si la excepción venía
        /// de la persistencia de formato. Los handlers de Ctrl+Z/Y lo usan para no
        /// dejar escapar la excepción sin perder el stack (el manager ya lo conservó).
        /// </summary>
        public static bool ManejarErrorDeFormato(Exception ex)
        {
            if (ex is not FormatoCeldasPersistenceException formato) return false;

            MessageBox.Show(
                formato.Message,
                "Formato",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return true;
        }
    }
}
