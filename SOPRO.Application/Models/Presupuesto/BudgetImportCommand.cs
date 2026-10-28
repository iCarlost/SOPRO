using SOPRO.Application.Services;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Intención de importar un lote de renglones nuevos al presupuesto (ADR-0001).
    /// Lleva el proyecto propietario, la base de cálculo esperada y las filas YA
    /// normalizadas por la vista (clave, descripción, unidad, cantidad y tipo). El
    /// comando rechaza un lote vacío o una fila inválida antes de escribir y calcula
    /// en Application la posición inicial (Orden) y la aritmética derivada.
    /// Todos sus valores son de solo lectura para que un reintento repita exactamente
    /// la misma operación.
    /// </summary>
    public sealed class BudgetImportCommand
    {
        public BudgetImportCommand(
            int projectId,
            BudgetProjectCalculationBase expectedProject,
            IEnumerable<BudgetImportRow> rows)
        {
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));

            ArgumentNullException.ThrowIfNull(expectedProject);
            ArgumentNullException.ThrowIfNull(rows);

            var lista = rows.ToArray();
            if (lista.Length == 0)
                throw new ArgumentException("La importación requiere al menos un renglón válido.", nameof(rows));

            if (lista.Any(r => r is null))
                throw new ArgumentException("La importación no admite renglones nulos.", nameof(rows));

            ProjectId = projectId;
            ExpectedProject = expectedProject;
            Rows = lista;
        }

        public int ProjectId { get; }

        /// <summary>Base económica del proyecto con la que la vista construyó el lote.</summary>
        public BudgetProjectCalculationBase ExpectedProject { get; }

        /// <summary>Renglones confirmados por el usuario, en el orden en que se insertarán.</summary>
        public IReadOnlyList<BudgetImportRow> Rows { get; }
    }

    /// <summary>
    /// Renglón normalizado de una importación. La vista ya resolvió el texto de tipo
    /// (<c>Capitulo</c>, <c>Subcapitulo</c>, <c>Nivel n</c> o <c>Concepto</c>) y
    /// descartó las filas <c>No importar</c>; aquí solo se valida lo que se va a
    /// escribir. Los agrupadores se guardan sin unidad ni cantidad.
    /// </summary>
    public sealed class BudgetImportRow
    {
        public BudgetImportRow(
            int? sourceRowNumber,
            string? clave,
            string? descripcion,
            string? unidad,
            decimal cantidad,
            string? tipo)
        {
            if (sourceRowNumber is <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceRowNumber));

            Clave = (clave ?? string.Empty).Trim();
            Descripcion = (descripcion ?? string.Empty).Trim();

            if (Clave.Length == 0 && Descripcion.Length == 0)
                throw new ArgumentException(
                    "El renglón importado no tiene clave ni descripción.", nameof(descripcion));

            if (cantidad < 0m)
                throw new ArgumentOutOfRangeException(
                    nameof(cantidad), "La cantidad importada no puede ser negativa.");

            Tipo = NormalizarTipo(tipo);
            EsAgrupador = !string.Equals(Tipo, "Concepto", StringComparison.Ordinal);
            Nivel = BudgetPersistenceService.ResolveLevelFromType(Tipo);
            Unidad = EsAgrupador ? string.Empty : (unidad ?? string.Empty).Trim();
            Cantidad = EsAgrupador ? 0m : cantidad;
            SourceRowNumber = sourceRowNumber;
        }

        /// <summary>Fila de origen del archivo, si el importador la conoce.</summary>
        public int? SourceRowNumber { get; }

        public string Clave { get; }

        public string Descripcion { get; }

        public string Unidad { get; }

        public decimal Cantidad { get; }

        /// <summary>Tipo normalizado que determina nivel y condición de agrupador.</summary>
        public string Tipo { get; }

        public bool EsAgrupador { get; }

        public int Nivel { get; }

        private static string NormalizarTipo(string? tipo)
        {
            string normalizado = (tipo ?? string.Empty).Trim().ToLowerInvariant();

            return normalizado switch
            {
                "capitulo" or "capítulo" => "Capitulo",
                "subcapitulo" or "subcapítulo" => "Subcapitulo",
                "nivel 1" => "Nivel 1",
                "nivel 2" => "Nivel 2",
                "nivel 3" => "Nivel 3",
                "concepto" => "Concepto",
                _ => throw new ArgumentException(
                    $"Tipo de renglón no válido para importar: '{tipo}'.", nameof(tipo))
            };
        }
    }
}
