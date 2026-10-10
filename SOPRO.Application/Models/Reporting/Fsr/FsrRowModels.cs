using System.Collections.Generic;

namespace SOPRO.Application.Models.Reporting.Fsr;

/// <summary>
/// Tipo de fila del reporte AE-2(A) "Tabla de cálculo del FSR": secciones y
/// subsecciones se renderizan como bandas, los datos como filas con valor.
/// Se extrae del generador PDF para que el modelo de fila viva en Application
/// (neutral a MigraDoc/ClosedXML) y sea compartido por PDF y Excel.
/// </summary>
public enum FsrRowType
{
    /// <summary>Banda de sección (fondo destacado).</summary>
    Seccion,

    /// <summary>Banda de subsección (fondo tenue).</summary>
    Subseccion,

    /// <summary>Fila de dato con descripción/operación/unidad/valor.</summary>
    Dato,

    /// <summary>Fila final destacada (factor de salario real).</summary>
    Final
}

/// <summary>
/// Modelo neutral de una fila del reporte AE-2(A) del FSR. El valor viaja como
/// <see cref="decimal"/> cuando es numérico (el renderizador lo formatea con la
/// regla compartida del contrato) o como texto (<see cref="ValorTexto"/>) para
/// los casos de fidelidad legacy (p. ej. el límite INFONAVIT sin decimales).
/// No depende de ninguna tecnología de UI o renderizado.
/// </summary>
public sealed class FsrRow
{
    /// <summary>Tipo de fila.</summary>
    public FsrRowType Tipo { get; set; }

    /// <summary>Descripción de la fila (columna 1).</summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Operación/fórmula (columna 2).</summary>
    public string Operacion { get; set; } = string.Empty;

    /// <summary>Unidad (columna 3).</summary>
    public string Unidad { get; set; } = string.Empty;

    /// <summary>Valor numérico (columna 4); <c>null</c> si el valor es texto.</summary>
    public decimal? Valor { get; set; }

    /// <summary>Valor textual (columna 4) cuando no es numérico.</summary>
    public string? ValorTexto { get; set; }

    /// <summary>Crea una fila de sección.</summary>
    public static FsrRow Seccion(string descripcion)
        => new() { Tipo = FsrRowType.Seccion, Descripcion = descripcion };

    /// <summary>Crea una fila de subsección.</summary>
    public static FsrRow Subseccion(string descripcion)
        => new() { Tipo = FsrRowType.Subseccion, Descripcion = descripcion };

    /// <summary>Crea una fila de dato con valor numérico.</summary>
    public static FsrRow Numero(string descripcion, string operacion, string unidad, decimal valor)
        => new()
        {
            Tipo = FsrRowType.Dato,
            Descripcion = descripcion,
            Operacion = operacion ?? string.Empty,
            Unidad = unidad ?? string.Empty,
            Valor = valor
        };

    /// <summary>Crea una fila de dato con valor textual (fidelidad legacy).</summary>
    public static FsrRow Texto(string descripcion, string operacion, string unidad, string valor)
        => new()
        {
            Tipo = FsrRowType.Dato,
            Descripcion = descripcion,
            Operacion = operacion ?? string.Empty,
            Unidad = unidad ?? string.Empty,
            ValorTexto = valor ?? string.Empty
        };
}

/// <summary>
/// Modelo neutral de una fila del Tabulador FSR (AE-2(C)). El encabezado de cada
/// fila se compone de <see cref="Clave"/> y <see cref="Descripcion"/>; los 19
/// valores numéricos restantes viajan crudos en <see cref="Valores"/> (alineados
/// con las columnas numéricas del snapshot AE-2(C)) y cada renderizador los
/// formatea con la regla compartida del contrato. Sustituye al DTO
/// <c>GeneradorExcelFSR.AE2CRowData</c> que preformateaba las cadenas.
/// </summary>
public sealed class FsrTabuladorRow
{
    /// <summary>Clave del insumo de mano de obra.</summary>
    public string Clave { get; set; } = string.Empty;

    /// <summary>Descripción del insumo de mano de obra.</summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Indica que el insumo no tuvo cálculo FSR (SalarioBase &lt;= 0).</summary>
    public bool SinCalculo { get; set; }

    /// <summary>
    /// Valores numéricos alineados con las columnas numéricas del snapshot
    /// AE-2(C) en su orden canónico (SalarioBase, SalarioNominal, ..., SalarioReal).
    /// Vacío cuando <see cref="SinCalculo"/> es <c>true</c>.
    /// </summary>
    public IReadOnlyList<decimal> Valores { get; set; } = System.Array.Empty<decimal>();
}
