using System;
using System.Collections.Generic;
using System.Linq;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services;

/// <summary>
/// Fila neutral de la Explosión de Insumos para las rutas de exportación PDF/Excel.
/// Desacopla los generadores de la UI (<c>DatosInsumo</c> vive en WinForms) y del
/// renderizador: el resolver sólo materializa texto/números y los generadores
/// aplican el formato con la regla compartida del contrato neutral.
/// </summary>
/// <param name="Clave">Clave del insumo.</param>
/// <param name="Descripcion">Descripción del insumo.</param>
/// <param name="Unidad">Unidad del insumo.</param>
/// <param name="CantidadFisica">Cantidad física (normal) o acumulada (%MO).</param>
/// <param name="PrecioUnitario">Precio unitario del catálogo (normal) o inferido (%MO).</param>
/// <param name="Importe">Importe acumulado del insumo (método OPUS PLANET).</param>
/// <param name="Porcentaje">Participación sobre el costo directo total, como fracción (0..1).</param>
/// <param name="EsPorcentual">True para insumos %MO (cantidad/PU con guion o inferidos).</param>
public sealed record ExplosionReportRow(
    string Clave,
    string Descripcion,
    string Unidad,
    decimal CantidadFisica,
    decimal PrecioUnitario,
    decimal Importe,
    decimal Porcentaje,
    bool EsPorcentual);

/// <summary>
/// Resolución de valores de exportación (PDF/Excel) de una fila de la Explosión de
/// Insumos. Vive en Application para poder probarse sin formularios ni
/// renderizadores: los generadores de la UI sólo materializan lo que este resolver
/// expone.
///
/// Es también el punto único que construye el snapshot neutral de columnas
/// (<see cref="ReportColumnSnapshot"/>) del reporte, de modo que PDF y Excel
/// consuman exactamente la misma lista, orden, anchos, formatos y estilos. Los
/// defaults se construyen EN MEMORIA (nunca con <c>ColumnasExplosionHelper</c>, que
/// persistiría) para no escribir configuración desde la ruta de exportación.
///
/// La decisión de formato numérico del contrato (monetario vs. cantidad vs.
/// porcentaje vs. token legacy) NO vive aquí sino en
/// <c>SOPRO.Reporting.Formatting.ReportColumnGridFormat</c>, que es la única regla
/// compartida por PDF y Excel; este resolver sólo aporta el valor crudo (decimal) o
/// el texto.
/// </summary>
public static class ExplosionExportResolver
{
    /// <summary>Formato numérico por defecto de la cantidad.</summary>
    public const string FormatoCantidadPredeterminado = "N4";

    /// <summary>Formato monetario por defecto del precio unitario.</summary>
    public const string FormatoPrecioUnitarioPredeterminado = "C4";

    /// <summary>Formato monetario por defecto del importe.</summary>
    public const string FormatoMonetarioPredeterminado = "C2";

    /// <summary>Formato por defecto del porcentaje.</summary>
    public const string FormatoPorcentajePredeterminado = "P2";

    /// <summary>Marca de fidelidad legacy para insumos %MO sin cantidad/PU aplicables.</summary>
    public const string GuionInsumo = "\u2014";

    /// <summary>
    /// Columnas por defecto cuando no hay proyecto ni configuración persistida.
    /// Reproduce la configuración del módulo Explosión de Insumos (mismos
    /// identificadores, orden, anchos, alineación y tokens) y se expresa como
    /// <see cref="ColumnaExplosion"/> para viajar por el mismo mapper neutral que
    /// las columnas reales. Es aditiva: no persiste ni crea filas en BD.
    /// </summary>
    private static readonly IReadOnlyList<ColumnaExplosion> ColumnasPredeterminadas = new List<ColumnaExplosion>
    {
        new() { Nombre = "Clave",       NombreInterno = "Clave",          Visible = true, Orden = 1, AnchoColumna = 80,  Alineacion = AlineacionColumna.Izquierda },
        new() { Nombre = "Descripcion", NombreInterno = "Descripcion",    Visible = true, Orden = 2, AnchoColumna = 350, Alineacion = AlineacionColumna.Izquierda, WrapTexto = true },
        new() { Nombre = "Unidad",      NombreInterno = "Unidad",         Visible = true, Orden = 3, AnchoColumna = 70,  Alineacion = AlineacionColumna.Centro },
        new() { Nombre = "Cantidad",    NombreInterno = "Cantidad",       Visible = true, Orden = 4, AnchoColumna = 100, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = FormatoCantidadPredeterminado },
        new() { Nombre = "P.U.",        NombreInterno = "PrecioUnitario", Visible = true, Orden = 5, AnchoColumna = 120, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = FormatoPrecioUnitarioPredeterminado },
        new() { Nombre = "Importe",     NombreInterno = "Importe",        Visible = true, Orden = 6, AnchoColumna = 130, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = FormatoMonetarioPredeterminado },
        new() { Nombre = "%",           NombreInterno = "Porcentaje",     Visible = true, Orden = 7, AnchoColumna = 80,  Alineacion = AlineacionColumna.Derecha, FormatoNumerico = FormatoPorcentajePredeterminado },
    };

    /// <summary>
    /// Construye el snapshot neutral de columnas del reporte de Explosión de Insumos.
    /// Es la ÚNICA fuente que consumen PDF y Excel (misma visibilidad/orden/ancho/
    /// formato y mismos decimales del proyecto, para paridad grid↔export). Si no
    /// llegan columnas se usan las predeterminadas EN MEMORIA.
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas de explosión persistidas.</param>
    /// <param name="decimalesCantidad">Decimales de cantidad del proyecto (por defecto 2).</param>
    /// <param name="decimalesImporte">Decimales de importe del proyecto (por defecto 2).</param>
    /// <param name="decimalesPorcentaje">Decimales de porcentaje del proyecto (por defecto 4).</param>
    public static ReportColumnSnapshot BuildSnapshot(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaExplosion>? columnas,
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        var fuente = columnas?.Where(c => c != null).ToList();
        if (fuente == null || fuente.Count == 0)
            fuente = ColumnasPredeterminadas.ToList();

        return new ExplosionReportSnapshotBuilder().Build(
            proyectoId, titulo, fuente, decimalesCantidad, decimalesImporte, decimalesPorcentaje);
    }

    /// <summary>
    /// Columnas neutrales por defecto (sin proyecto/configuración), ya filtradas
    /// de columnas internas y ordenadas por <c>Orden</c>.
    /// </summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumns()
        => ColumnasPredeterminadas
            .Select(ReportColumnDefinitionMapper.MapearExplosion)
            .ToList();

    /// <summary>
    /// Resuelve el valor de TEXTO de una celda para una columna neutral con paridad
    /// con el grid. Las columnas numéricas (cantidad, precio unitario, importe y
    /// porcentaje) NO se formatean aquí: su valor crudo se obtiene con
    /// <see cref="TryResolveNumber"/> y el formato lo decide la regla compartida del
    /// contrato. Los insumos %MO conservan la marca legacy
    /// <see cref="GuionInsumo"/> cuando la celda numérica no aplica.
    /// </summary>
    /// <param name="row">Fila neutral de la explosión.</param>
    /// <param name="column">Columna neutral.</param>
    public static string ResolveValue(ExplosionReportRow row, ReportColumnDefinition column)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(column);

        return NormalizarIdentificador(column.Identificador).ToUpperInvariant() switch
        {
            "CLAVE" => row.Clave ?? string.Empty,
            "DESCRIPCION" => row.Descripcion ?? string.Empty,
            "UNIDAD" => row.Unidad ?? string.Empty,
            // Fidelidad legacy: el insumo %MO muestra cantidad/PU con guion en lugar
            // de un número; se resuelve aquí porque no es un valor numérico formateable.
            "CANTIDAD" => row.EsPorcentual ? GuionInsumo : string.Empty,
            "PRECIOUNITARIO" => row.EsPorcentual && row.PrecioUnitario == 0m ? GuionInsumo : string.Empty,
            _ => string.Empty
        };
    }

    /// <summary>
    /// Intenta obtener el valor NUMÉRICO crudo de una fila para una columna neutral.
    /// Devuelve <c>false</c> para columnas de texto, para columnas numéricas sin
    /// campo de respaldo y para los insumos %MO cuyas celdas muestran
    /// <see cref="GuionInsumo"/> (fidelidad legacy: antes se exportaban con guion).
    /// </summary>
    /// <param name="row">Fila neutral de la explosión.</param>
    /// <param name="column">Columna neutral.</param>
    /// <param name="valor">Valor decimal resuelto.</param>
    public static bool TryResolveNumber(ExplosionReportRow row, ReportColumnDefinition column, out decimal valor)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(column);

        switch (NormalizarIdentificador(column.Identificador).ToUpperInvariant())
        {
            case "CANTIDAD":
                if (row.EsPorcentual)
                {
                    valor = 0m;
                    return false;
                }
                valor = row.CantidadFisica;
                return true;
            case "PRECIOUNITARIO":
                if (row.EsPorcentual && row.PrecioUnitario == 0m)
                {
                    valor = 0m;
                    return false;
                }
                valor = row.PrecioUnitario;
                return true;
            case "IMPORTETOTAL":
                valor = row.Importe;
                return true;
            case "PORCENTAJE":
                valor = row.Porcentaje;
                return true;
            default:
                valor = 0m;
                return false;
        }
    }

    /// <summary>
    /// Resuelve el color de fondo (hex) de una celda respetando el color por
    /// columna definido en la entidad y, en su defecto, el bandeado de la tabla.
    /// Es neutral (no depende de UI) para que PDF y Excel compartan la regla.
    /// </summary>
    /// <param name="columna">Columna neutral.</param>
    /// <param name="tabla">Estilo neutral de la tabla.</param>
    /// <param name="esFilaAlterna">Si la fila es la alterna del bandeado.</param>
    public static string ResolveCellBackground(ReportColumnDefinition columna, ReportTableStyle tabla, bool esFilaAlterna)
    {
        ArgumentNullException.ThrowIfNull(columna);
        ArgumentNullException.ThrowIfNull(tabla);

        var porColumna = columna.EstiloContenido.ColorFondo;
        var porDefectoTabla = tabla.EstiloContenido.ColorFondo ?? "#FFFFFF";
        var esOverrideColumna = !string.IsNullOrWhiteSpace(porColumna)
            && !string.Equals(porColumna, porDefectoTabla, StringComparison.OrdinalIgnoreCase);

        if (esOverrideColumna)
            return porColumna!;

        if (esFilaAlterna && !string.IsNullOrWhiteSpace(tabla.FilaAlterna.ColorFondoAlterno))
            return tabla.FilaAlterna.ColorFondoAlterno!;

        return string.IsNullOrWhiteSpace(porColumna) ? porDefectoTabla : porColumna!;
    }

    /// <summary>
    /// Normaliza el identificador para el reconocimiento de campo de respaldo:
    /// recorta espacios, descarta un prefijo de grid <c>col</c> (p. ej.
    /// <c>colImporte</c> → <c>Importe</c>) y resuelve el alias legacy
    /// <c>Importe</c> a <c>ImporteTotal</c>.
    /// </summary>
    private static string NormalizarIdentificador(string? identificador)
    {
        var id = (identificador ?? string.Empty).Trim();
        if (id.Length > 3 && id.StartsWith("col", StringComparison.OrdinalIgnoreCase))
            id = id[3..];
        return ReportColumnDefinitionMapper.NormalizarIdentificador(id);
    }
}
