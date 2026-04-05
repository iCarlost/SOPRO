using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public static class PresupuestoValidationService
    {
        public sealed class ValidationIssue
        {
            public int? ConceptoId { get; set; }
            public string Tipo { get; set; } = string.Empty;
            public string ClaveConcepto { get; set; } = string.Empty;
            public string DescripcionConcepto { get; set; } = string.Empty;
            public string Mensaje { get; set; } = string.Empty;

            public string ToSingleLine()
            {
                var prefijo = string.IsNullOrWhiteSpace(ClaveConcepto)
                    ? "[Sin clave]"
                    : $"[{ClaveConcepto}]";

                var descripcion = string.IsNullOrWhiteSpace(DescripcionConcepto)
                    ? string.Empty
                    : $" {DescripcionConcepto}";

                return $"{prefijo}{descripcion} — {Mensaje}";
            }

            public override string ToString() => $"- {ToSingleLine()}";
        }

        public static List<ValidationIssue> Validate(SOPROContext context, int proyectoId)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var issues = new List<ValidationIssue>();

            var conceptos = context.ConceptosPresupuesto
                .AsNoTracking()
                .Include(c => c.Matriz)
                    .ThenInclude(m => m.Componentes)
                .Where(c => c.ProyectoId == proyectoId)
                .Where(c => !c.EsAgrupador)
                .OrderBy(c => c.Orden)
                .ToList();

            foreach (var concepto in conceptos)
            {
                var mensajes = new List<string>();

                if (concepto.Cantidad == 0m)
                    mensajes.Add("Cantidad = 0");

                if (concepto.PrecioUnitario == 0m)
                    mensajes.Add("Precio unitario = 0");

                if (concepto.Matriz?.Componentes != null && concepto.Matriz.Componentes.Any(x => x.Cantidad == 0m))
                    mensajes.Add("Matriz: componente con cantidad = 0");

                if (mensajes.Count == 0)
                    continue;

                issues.Add(new ValidationIssue
                {
                    ConceptoId = concepto.Id,
                    Tipo = "Concepto",
                    ClaveConcepto = concepto.Clave ?? string.Empty,
                    DescripcionConcepto = concepto.Descripcion ?? string.Empty,
                    Mensaje = string.Join(" | ", mensajes)
                });
            }

            return issues;
        }

        public static string BuildMessage(IEnumerable<ValidationIssue> issues, bool includeHeader = true)
        {
            var lista = issues?.ToList() ?? new List<ValidationIssue>();
            if (lista.Count == 0)
                return includeHeader ? "No se detectaron inconsistencias en el presupuesto." : string.Empty;

            var lineas = new List<string>();
            if (includeHeader)
            {
                lineas.Add("Se detectaron inconsistencias en el presupuesto:");
                lineas.Add(string.Empty);
            }

            lineas.AddRange(lista.Select(x => x.ToString()));
            lineas.Add(string.Empty);
            lineas.Add("Corrija antes de continuar.");
            return string.Join(Environment.NewLine, lineas);
        }
    }
}
