using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto;

/// <summary>
/// Orden relativo único de las columnas personalizadas del presupuesto. Es la
/// misma secuencia que consumen el grid y los reportes (PDF/Excel): ascendente
/// por <see cref="ColumnaPersonalizada.Orden"/> persistido, con desempate
/// determinista por Id. No usa índices absolutos del grid (colId/#, huecos ni
/// valores negativos), por lo que la secuencia sobrevive a recargas.
/// </summary>
public static class BudgetColumnOrder
{
    public static IReadOnlyList<ColumnaPersonalizada> OrdenarPorOrdenPersistido(
        IEnumerable<ColumnaPersonalizada> columnas)
    {
        ArgumentNullException.ThrowIfNull(columnas);

        return columnas
            .Where(c => c is not null)
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Id)
            .ToList();
    }
}
