using System;
using System.Collections.Generic;
using System.Linq;
using SOPRO.Application.Models.Reporting.ReportColumns;

namespace SOPRO.Reporting.Columns;

/// <summary>
/// Definiciones de columna neutrales que aporta el renderer cuando el contrato
/// persistido no las incluye. En concreto, la columna sintética de numeración
/// ("#"/<c>Numero</c>) del reporte de Presupuesto: en el flujo legacy vivía en el
/// grid (<c>colNumero</c>) y la exportación la sincronizaba, de modo que el
/// reporte por defecto la mostraba. Al migrar a un snapshot neutral construido
/// desde configuración persistida esa columna desapareció; este helper restaura
/// la paridad por defecto SIN leer del grid ni escribir configuración.
///
/// Es neutral (no depende de MigraDoc/ClosedXML) para que PDF y Excel prependan
/// exactamente la misma definición y no dupliquen sus atributos.
/// </summary>
public static class ReportColumnDefaults
{
    /// <summary>Identificador neutral de la columna de numeración (lo resuelven los renderers).</summary>
    public const string NumeroIdentificador = "Numero";

    /// <summary>Encabezado visible de la columna de numeración (paridad con el grid legacy).</summary>
    public const string NumeroEncabezado = "#";

    /// <summary>Ancho en píxeles de la columna de numeración (paridad con <c>colNumero.Width = 40</c>).</summary>
    public const int NumeroAnchoPx = 40;

    /// <summary>
    /// Definición de la columna de numeración del Presupuesto: angosta, centrada,
    /// no numérica (el valor es el índice de fila como texto) y con los estilos
    /// por defecto (<see cref="ReportTableStyle.LegacyPresupuesto"/>).
    /// </summary>
    public static ReportColumnDefinition PresupuestoNumeroColumn()
    {
        var estilo = ReportTableStyle.LegacyPresupuesto();

        return new ReportColumnDefinition(
            Identificador: NumeroIdentificador,
            Encabezado: NumeroEncabezado,
            Visible: true,
            Orden: int.MinValue,
            Ancho: NumeroAnchoPx,
            EstiloEncabezado: estilo.EstiloEncabezado,
            EstiloContenido: estilo.EstiloContenido,
            Alineacion: ReportTextAlignment.Centro,
            AlineacionVertical: ReportVerticalAlignment.Medio,
            Wrap: false,
            FormatoNumerico: string.Empty,
            EsNumerica: false);
    }

    /// <summary>
    /// Devuelve las columnas con la columna de numeración antepuesta. Si el
    /// snapshot ya define una columna <c>Numero</c> (configuración persistida),
    /// se respeta y no se duplica.
    /// </summary>
    /// <param name="columnas">Columnas visibles ya ordenadas.</param>
    public static List<ReportColumnDefinition> ConPrefijoNumeroPresupuesto(
        IReadOnlyList<ReportColumnDefinition> columnas)
    {
        if (columnas == null) throw new ArgumentNullException(nameof(columnas));

        if (columnas.Any(EsNumero))
            return columnas.ToList();

        var resultado = new List<ReportColumnDefinition>(columnas.Count + 1)
        {
            PresupuestoNumeroColumn()
        };
        resultado.AddRange(columnas);
        return resultado;
    }

    /// <summary>Indica si la columna es la de numeración (comparación sin distinguir mayúsculas).</summary>
    public static bool EsNumero(ReportColumnDefinition? columna)
        => columna != null
           && string.Equals(columna.Identificador, NumeroIdentificador, StringComparison.OrdinalIgnoreCase);
}
