using System.Globalization;
using SOPRO.Application.Models.Reporting.MatrixCatalog;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Resolución pura de campos dinámicos de la plantilla. Replica la cadena
/// ReporteService.ResolverCampos (SOPRO.WinForms.Services), usando el reloj
/// explícito (DateTime) para {fecha_impresion}. Los campos {pagina} y
/// {total_paginas} quedan sin resolver (el medio de salida los resuelve).
/// Se usa formato invariante para que el modelo sea independiente de la cultura
/// del equipo; los tests pueden fijar la fecha y verificar su resolución.
/// </summary>
internal static class MatrixCatalogTokenResolver
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Resolver(string? texto, MatrixCatalogProject proyecto, MatrixCatalogTemplate? plantilla, DateTime now)
    {
        if (string.IsNullOrEmpty(texto)) return texto ?? string.Empty;

        return texto
            .Replace("{nombre_proyecto}", proyecto?.Nombre ?? "")
            .Replace("{descripcion}", proyecto?.Descripcion ?? "")
            .Replace("{ubicacion}", proyecto?.Ubicacion ?? plantilla?.CampoUbicacion ?? "")
            .Replace("{convocante}", proyecto?.Convocante ?? "")
            .Replace("{contratista}", proyecto?.Contratista ?? "")
            .Replace("{apoderado_legal}", proyecto?.ApoderadoLegal ?? "")
            .Replace("{fecha_inicio}", proyecto?.FechaInicio.HasValue == true
                ? proyecto.FechaInicio.Value.ToString("dd/MM/yyyy", Invariant)
                : plantilla?.CampoFechaInicio ?? "")
            .Replace("{fecha_termino}", proyecto?.FechaTermino.HasValue == true
                ? proyecto.FechaTermino.Value.ToString("dd/MM/yyyy", Invariant)
                : plantilla?.CampoFechaTermino ?? "")
            .Replace("{plazo_ejecucion}", proyecto?.PlazoEjecucion?.ToString(Invariant) ?? "")
            .Replace("{elaboro}", plantilla?.CampoElabaro ?? "")
            .Replace("{reviso}", plantilla?.CampoReviso ?? "")
            .Replace("{autorizo}", plantilla?.CampoAutorizo ?? "")
            .Replace("{dependencia}", plantilla?.CampoDependencia ?? "")
            .Replace("{numero_contrato}", plantilla?.CampoNumeroContrato ?? "")
            .Replace("{licitacion}", plantilla?.CampoLicitacion ?? "")
            .Replace("{texto1}", plantilla?.CampoTextoLibre1 ?? "")
            .Replace("{texto2}", plantilla?.CampoTextoLibre2 ?? "")
            .Replace("{fecha_impresion}", now.ToString("dd/MM/yyyy HH:mm", Invariant));
    }
}