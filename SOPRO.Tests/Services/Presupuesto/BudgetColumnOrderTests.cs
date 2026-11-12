using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Presupuesto;

/// <summary>
/// Orden relativo único de columnas del presupuesto: ascendente por el
/// <c>Orden</c> persistido, sin depender de índices absolutos del grid, y con
/// desempate determinista por Id para que grid y reportes coincidan.
/// </summary>
[TestClass]
public class BudgetColumnOrderTests
{
    [TestMethod]
    public void OrdenarPorOrdenPersistido_MezclaNegativosYPositivos_DevuelveSecuenciaAscendente()
    {
        var columnas = new[]
        {
            Columna(id: 3, orden: 2),
            Columna(id: 1, orden: -7),
            Columna(id: 4, orden: 9),
            Columna(id: 2, orden: -1)
        };

        var ordenadas = BudgetColumnOrder.OrdenarPorOrdenPersistido(columnas);

        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, ordenadas.Select(c => c.Id).ToArray());
        CollectionAssert.AreEqual(new[] { -7, -1, 2, 9 }, ordenadas.Select(c => c.Orden).ToArray());
    }

    [TestMethod]
    public void OrdenarPorOrdenPersistido_OrdenRepetido_DesempataPorIdDeFormaDeterminista()
    {
        var columnas = new[]
        {
            Columna(id: 9, orden: 4),
            Columna(id: 2, orden: 4),
            Columna(id: 5, orden: 4)
        };

        var ordenadas = BudgetColumnOrder.OrdenarPorOrdenPersistido(columnas);

        CollectionAssert.AreEqual(new[] { 2, 5, 9 }, ordenadas.Select(c => c.Id).ToArray());
    }

    [TestMethod]
    public void OrdenarPorOrdenPersistido_NoModificaLaEntrada()
    {
        var original = new[]
        {
            Columna(id: 1, orden: 10),
            Columna(id: 2, orden: -3)
        };

        _ = BudgetColumnOrder.OrdenarPorOrdenPersistido(original);

        CollectionAssert.AreEqual(new[] { 1, 2 }, original.Select(c => c.Id).ToArray());
        CollectionAssert.AreEqual(new[] { 10, -3 }, original.Select(c => c.Orden).ToArray());
    }

    private static ColumnaPersonalizada Columna(int id, int orden) => new()
    {
        Id = id,
        NombreInterno = "Col" + id,
        Nombre = "Columna " + id,
        Orden = orden
    };
}
