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
/// Tests pin para defectos de precisión en importación externa.
/// O1 — ExternalInsumoImportService: Importe = PU * cantidad sin usar el motor.
/// O2 — ExternalMatrixImportService: copia Importe/CostoDirecto del origen sin recalcular.
/// Deben FALLAR (rojo) con el código actual y QUEDAR VERDES con el fix.
/// </summary>
[TestClass]
public class ExternalImportPrecisionTests
{
    private static Proyecto CrearProyecto(string nombre, int decimalesImporte)
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
            DecimalesImporte = decimalesImporte,
            DecimalesPorcentaje = 4
        };
    }

    [TestMethod]
    public void ImportarMaterial_DebeUsarMotorDelProyectoDestino()
    {
        using var current = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto("Destino 2 deci", 2);
        current.Proyectos.Add(proyecto);
        current.SaveChanges();

        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_ext_{Guid.NewGuid():N}.db");
        try
        {
            int materialId;
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(ctx);

                var proy = CrearProyecto("Origen (2 deci)", 2);
                ctx.Proyectos.Add(proy);
                ctx.SaveChanges();

                var material = new Material
                {
                    ProyectoId = proy.Id,
                    Clave = "MAT-X",
                    Descripcion = "Material origen",
                    Unidad = "pza",
                    PrecioUnitario = 10.005m,
                    Notas = string.Empty,
                    Origen = OrigenInsumo.Proyecto
                };
                ctx.Materiales.Add(material);
                ctx.SaveChanges();
                materialId = material.Id;
            }

            var opcion = new ExternalProjectInsumoOption
            {
                ItemId = materialId,
                ProjectName = "Origen",
                ProjectPath = dbPath,
                TipoComponente = TipoComponenteMatriz.Material,
                Clave = "MAT-X",
                PrecioUnitario = 10.005m
            };

            var service = new ExternalInsumoImportService();
            var resultado = service.ImportSelected(
                current, proyecto.Id, dbPath,
                new[] { opcion },
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey,
                cantidad: 2m);

            // PU 10.005 → redondeado a 10.01 (2 deci) → 2 × 10.01 = 20.02
            // Defecto O1: 2 × 10.005 = 20.01 (sin redondear)
            var componente = resultado.ImportedComponents
                .Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
            Assert.AreEqual(20.02m, componente.Importe,
                "El importe del material importado debe calcularse con precisión del proyecto destino.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [TestMethod]
    public void ImportarMaquinaria_SeUsaMotorDelProyectoDestino()
    {
        using var current = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto("Destino 2 deci", 2);
        current.Proyectos.Add(proyecto);
        current.SaveChanges();

        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_ext_{Guid.NewGuid():N}.db");
        try
        {
            int maquinariaId;
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(ctx);

                var proy = CrearProyecto("Origen maq", 2);
                ctx.Proyectos.Add(proy);
                ctx.SaveChanges();

                var maquina = new Maquinaria
                {
                    ProyectoId = proy.Id,
                    Clave = "MAQ-X",
                    Descripcion = "Maquina origen",
                    CostoHorario = 10.005m,
                    Notas = string.Empty,
                    Origen = OrigenInsumo.Proyecto
                };
                ctx.Maquinaria.Add(maquina);
                ctx.SaveChanges();
                maquinariaId = maquina.Id;
            }

            var opcion = new ExternalProjectInsumoOption
            {
                ItemId = maquinariaId,
                ProjectName = "Origen",
                ProjectPath = dbPath,
                TipoComponente = TipoComponenteMatriz.Maquinaria,
                Clave = "MAQ-X"
            };

            var service = new ExternalInsumoImportService();
            var resultado = service.ImportSelected(
                current, proyecto.Id, dbPath,
                new[] { opcion },
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey,
                cantidad: 2m);

            // PU visible 10.01 → 2 × 10.01 = 20.02 (no 20.01 crudo)
            var componente = resultado.ImportedComponents
                .Single(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria);
            Assert.AreEqual(20.02m, componente.Importe,
                "El importe de maquinaria importada debe usar el motor (2 × 10.01 = 20.02).");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [TestMethod]
    public void ImportarMatriz_CostoDirectoSeRecalculaConMotorDelDestino()
    {
        using var current = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto("Destino 2 deci", 2);
        current.Proyectos.Add(proyecto);
        current.SaveChanges();

        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_ext_{Guid.NewGuid():N}.db");
        try
        {
            int materialId;
            int matrizId;
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(ctx);

                var proy = CrearProyecto("Origen mat", 2);
                ctx.Proyectos.Add(proy);
                ctx.SaveChanges();

                var material = new Material
                {
                    ProyectoId = proy.Id,
                    Clave = "MAT-Y",
                    Descripcion = "Mat origen",
                    Unidad = "pza",
                    PrecioUnitario = 10.005m,
                    Notas = string.Empty,
                    Origen = OrigenInsumo.Proyecto
                };
                ctx.Materiales.Add(material);
                ctx.SaveChanges();

                var matriz = new Matriz
                {
                    ProyectoId = proy.Id,
                    Clave = "APU-Y",
                    Descripcion = "APU origen",
                    Unidad = "m2",
                    Tipo = TipoMatriz.APU,
                    Notas = string.Empty,
                    CostoDirecto = 30.015m // origen persiste costo NO redondeado
                };
                ctx.Matrices.Add(matriz);
                ctx.SaveChanges();

                ctx.ComponentesMatriz.Add(new ComponenteMatriz
                {
                    MatrizId = matriz.Id,
                    TipoComponente = TipoComponenteMatriz.Material,
                    MaterialId = material.Id,
                    Cantidad = 3m,
                    Importe = 30.015m,
                    Orden = 1,
                    Notas = string.Empty
                });
                ctx.SaveChanges();
                materialId = material.Id;
                matrizId = matriz.Id;
            }

            var service = new ExternalMatrixImportService();
            var resultado = service.ImportMatrixTree(
                current, proyecto.Id, dbPath, matrizId,
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey);

            var importada = current.Matrices
                .Include(m => m.Componentes)
                .First(m => m.Id == resultado.RootMatrixId);

            // Con motor del destino (2 deci): 3 × redondear(10.005) = 3 × 10.01 = 30.03
            // Defecto O2: copia 30.015 tal cual del origen.
            Assert.AreEqual(30.03m, importada.CostoDirecto,
                "El CostoDirecto de la matriz importada debe recalcularse con el motor del proyecto destino.");
            Assert.AreEqual(30.03m, importada.Componentes.Single().Importe,
                "El Importe de cada componente importado debe recalcularse con el motor del destino.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}