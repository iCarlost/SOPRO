using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Persistencia;

/// <summary>
/// La huella del estado de componentes es la base con la que Application revalida
/// contra la DB: debe exponer sus filas en una colección de solo lectura, nunca en
/// el array mutable subyacente, para que ningún llamador altere lo que se compara.
/// </summary>
[TestClass]
public class BudgetMatrixComponentStateFingerprintImmutabilityTests
{
    [TestMethod]
    public void StateFingerprint_Rows_NoSePuedeModificarNiCastearAlArray()
    {
        var originales = CrearFilas();
        var fingerprint = new BudgetMatrixComponentStateFingerprint(originales);

        var esperado = Instantanea(fingerprint.Rows);

        Assert.IsFalse(
            fingerprint.Rows is BudgetMatrixComponentQuantityBase[],
            "La huella no debe exponer el array mutable subyacente.");
        Assert.IsNull(
            fingerprint.Rows as BudgetMatrixComponentQuantityBase[],
            "El casteo al array subyacente debe ser imposible.");

        var lista = fingerprint.Rows as IList<BudgetMatrixComponentQuantityBase>;
        Assert.IsNotNull(lista, "La huella debe exponer una lista de solo lectura.");
        Assert.IsTrue(lista!.IsReadOnly, "La lista expuesta debe ser de solo lectura.");

        var fila = fingerprint.Rows[0];
        Assert.ThrowsExactly<NotSupportedException>(() => lista.Add(fila));
        Assert.ThrowsExactly<NotSupportedException>(() => lista.Insert(0, fila));
        Assert.ThrowsExactly<NotSupportedException>(() => lista.RemoveAt(0));
        Assert.ThrowsExactly<NotSupportedException>(() => lista.Remove(fila));
        Assert.ThrowsExactly<NotSupportedException>(() => lista.Clear());
        Assert.ThrowsExactly<NotSupportedException>(() => lista[0] = CrearFila(99, 1, cantidad: 999m));

        originales.Clear();

        AssertBasesSinCambios(esperado, fingerprint.Rows);
    }

    [TestMethod]
    public void CostAdjustmentClosureEntry_Components_Rows_QuedaProtegido()
    {
        var matriz = CrearMatriz();
        var entry = BudgetMatrixCostAdjustmentClosureEntry.FromMatriz(matriz);

        var esperado = Instantanea(entry.Components.Rows);

        Assert.IsFalse(
            entry.Components.Rows is BudgetMatrixComponentQuantityBase[],
            "El fingerprint del cierre tampoco debe exponer el array mutable subyacente.");

        var lista = entry.Components.Rows as IList<BudgetMatrixComponentQuantityBase>;
        Assert.IsNotNull(lista, "El fingerprint del cierre debe exponer una lista de solo lectura.");
        Assert.IsTrue(lista!.IsReadOnly);

        var fila = entry.Components.Rows[0];
        Assert.ThrowsExactly<NotSupportedException>(() => lista.Add(fila));
        Assert.ThrowsExactly<NotSupportedException>(() => lista.RemoveAt(0));
        Assert.ThrowsExactly<NotSupportedException>(() => lista[0] = CrearFila(99, 1, cantidad: 999m));

        foreach (var componente in matriz.Componentes)
            componente.Cantidad = 999m;

        AssertBasesSinCambios(esperado, entry.Components.Rows);
        CollectionAssert.AreEqual(
            new[] { 11, 12 },
            entry.Components.Rows.Select(r => r.ComponentId).ToArray(),
            "El cierre debe conservar el orden por Orden e Id de la huella.");
    }

    private static void AssertBasesSinCambios(
        (int ComponentId, int MatrixId, int? MaterialId, decimal Cantidad, decimal Rendimiento, decimal Importe, int Orden)[] esperado,
        IReadOnlyList<BudgetMatrixComponentQuantityBase> filas)
    {
        Assert.AreEqual(esperado.Length, filas.Count, "La huella cambió de tamaño.");
        for (var i = 0; i < esperado.Length; i++)
        {
            Assert.AreEqual(esperado[i].ComponentId, filas[i].ComponentId);
            Assert.AreEqual(esperado[i].MatrixId, filas[i].MatrixId);
            Assert.AreEqual(esperado[i].MaterialId, filas[i].MaterialId);
            Assert.AreEqual(esperado[i].Cantidad, filas[i].Cantidad);
            Assert.AreEqual(esperado[i].Rendimiento, filas[i].Rendimiento);
            Assert.AreEqual(esperado[i].Importe, filas[i].Importe);
            Assert.AreEqual(esperado[i].Orden, filas[i].Orden);
        }
    }

    private static (int ComponentId, int MatrixId, int? MaterialId, decimal Cantidad, decimal Rendimiento, decimal Importe, int Orden)[] Instantanea(
        IReadOnlyList<BudgetMatrixComponentQuantityBase> filas)
        => filas
            .Select(r => (r.ComponentId, r.MatrixId, r.MaterialId, r.Cantidad, r.Rendimiento, r.Importe, r.Orden))
            .ToArray();

    private static List<BudgetMatrixComponentQuantityBase> CrearFilas()
        => new()
        {
            CrearFila(3, 2),
            CrearFila(1, 1, cantidad: 2m),
            CrearFila(2, 1, cantidad: 4m, orden: 1)
        };

    private static BudgetMatrixComponentQuantityBase CrearFila(
        int componentId,
        int matrixId,
        decimal cantidad = 1m,
        int orden = 0)
        => new(
            componentId,
            matrixId,
            TipoComponenteMatriz.Material,
            materialId: 100 + componentId,
            manoDeObraId: null,
            maquinariaId: null,
            auxiliarId: null,
            herramientaId: null,
            cantidad,
            rendimiento: 0m,
            importe: cantidad * 10m,
            orden);

    private static Matriz CrearMatriz()
    {
        var matriz = new Matriz
        {
            Id = 5,
            ProyectoId = 3,
            Clave = "APU-01",
            Descripcion = "Matriz de prueba",
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 60m
        };

        matriz.Componentes.Add(CrearComponente(12, 101, cantidad: 2m, orden: 2));
        matriz.Componentes.Add(CrearComponente(11, 100, cantidad: 4m, orden: 1));

        matriz.CostoDirecto = matriz.Componentes.Sum(c => c.Importe);
        return matriz;
    }

    private static ComponenteMatriz CrearComponente(
        int id,
        int materialId,
        decimal cantidad,
        int orden)
        => new()
        {
            Id = id,
            MatrizId = 5,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = materialId,
            Material = new Material
            {
                Id = materialId,
                ProyectoId = 3,
                Clave = $"MAT-{materialId}",
                Descripcion = $"Material {materialId}",
                Unidad = "kg",
                PrecioUnitario = 10m
            },
            Cantidad = cantidad,
            Importe = cantidad * 10m,
            Orden = orden
        };
}
