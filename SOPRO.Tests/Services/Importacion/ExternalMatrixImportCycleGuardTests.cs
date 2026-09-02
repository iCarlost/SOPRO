using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Importacion;

/// <summary>
/// [N7-1e] Un grafo externo con ciclo de referencias entre matrices se diagnostica
/// con ruta ANTES de la copia de componentes (que antes perdía la referencia
/// aun-no-mapeada con un mensaje genérico) y antes de abrir transacción.
/// </summary>
[TestClass]
public class ExternalMatrixImportCycleGuardTests
{
    [TestMethod]
    public void ImportMatrixTree_GrafoFuenteConCiclo_LanzaRutaSinEscribirNada()
    {
        using var current = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto("Destino");
        current.Proyectos.Add(proyecto);
        current.SaveChanges();

        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_ciclo_{Guid.NewGuid():N}.db");
        try
        {
            (int rootId, int aId, int bId) = CrearCicloExterno(dbPath);

            var service = new ExternalMatrixImportService();
            var exception = Assert.ThrowsException<InvalidOperationException>(() => service.ImportMatrixTree(
                current, proyecto.Id, dbPath, rootId, ExternalMatrixImportConflictPolicy.KeepBothWithTempKey));

            StringAssert.Contains(exception.Message, "El grafo externo contiene un ciclo");
            StringAssert.Contains(exception.Message, "Ciclo de matrices detectado");
            Assert.IsTrue(
                exception.Message.Contains($"{aId} -> {bId} -> {aId}", StringComparison.Ordinal) ||
                exception.Message.Contains($"{bId} -> {aId} -> {bId}", StringComparison.Ordinal),
                "La ruta del ciclo debe identificar las dos matrices: " + exception.Message);

            Assert.AreEqual(0, current.Matrices.AsNoTracking().Count(m => m.ProyectoId == proyecto.Id),
                "el guard corre antes de la transacción: no se escribe nada");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [TestMethod]
    public void BuildPreview_GrafoFuenteConCiclo_LanzaAntesDelPreview()
    {
        using var current = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto("Destino");
        current.Proyectos.Add(proyecto);
        current.SaveChanges();

        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_ciclo_{Guid.NewGuid():N}.db");
        try
        {
            var (rootId, _, _) = CrearCicloExterno(dbPath);

            var service = new ExternalMatrixImportService();
            var exception = Assert.ThrowsException<InvalidOperationException>(() =>
                service.BuildPreview(current, proyecto.Id, dbPath, rootId));

            StringAssert.Contains(exception.Message, "El grafo externo contiene un ciclo");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    private static (int RootId, int AId, int BId) CrearCicloExterno(string dbPath)
    {
        using var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        SchemaManager.EnsureCurrentSchema(ctx);

        var proy = CrearProyecto("Origen");
        ctx.Proyectos.Add(proy);
        ctx.SaveChanges();

        var a = new Matriz
        {
            ProyectoId = proy.Id,
            Clave = "A-CIC",
            Descripcion = string.Empty,
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };
        var b = new Matriz
        {
            ProyectoId = proy.Id,
            Clave = "B-CIC",
            Descripcion = string.Empty,
            Unidad = "m2",
            Tipo = TipoMatriz.Basico,
            Notas = string.Empty
        };
        ctx.Matrices.AddRange(a, b);
        ctx.SaveChanges();

        ctx.ComponentesMatriz.AddRange(
            new ComponenteMatriz
            {
                MatrizId = a.Id,
                TipoComponente = TipoComponenteMatriz.Auxiliar,
                AuxiliarId = b.Id,
                Cantidad = 1m,
                Orden = 1,
                Notas = string.Empty
            },
            new ComponenteMatriz
            {
                MatrizId = b.Id,
                TipoComponente = TipoComponenteMatriz.Auxiliar,
                AuxiliarId = a.Id,
                Cantidad = 1m,
                Orden = 1,
                Notas = string.Empty
            });
        ctx.SaveChanges();

        return (a.Id, a.Id, b.Id);
    }

    private static Proyecto CrearProyecto(string nombre)
    {
        return new Proyecto
        {
            Nombre = nombre,
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            PorcentajeIndirectosCentral = 0m,
            PorcentajeIndirectosCampo = 0m,
            PorcentajeFinanciamiento = 0m,
            PorcentajeUtilidad = 0m,
            PorcentajeCargosAdicionales = 0m,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4
        };
    }
}
