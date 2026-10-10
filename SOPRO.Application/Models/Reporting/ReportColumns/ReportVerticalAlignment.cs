namespace SOPRO.Application.Models.Reporting.ReportColumns;

/// <summary>
/// Alineación vertical neutral del contenido de una celda. Reemplaza el entero
/// crudo legacy (<c>0</c> = Superior, <c>1</c> = Medio, <c>2</c> = Inferior) que
/// persisten <c>ColumnaPersonalizada</c> y <c>ColumnaMaterial</c>.
/// </summary>
public enum ReportVerticalAlignment
{
    Superior = 0,
    Medio = 1,
    Inferior = 2
}
