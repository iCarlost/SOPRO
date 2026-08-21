using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Importacion;

// [N5-13] (Imports externos, parte 2) Paridad de ExternalInsumoImportService:
// las 3 operaciones Multiplicar → Multiply (Importe del componente importado de
// Material/Maquinaria/Auxiliar) con la precisión del proyecto destino. El oráculo
// de la batería es independiente: Math.Round(RoundAmount(precio) × cantidad) sobre
// escalares conocidos, sin engine ni fachada. La rama Auxiliar reutiliza
// ImportMatrixTree (N5-12) para obtener el CostoDirecto redondeado del básico.

[TestClass]
public class ExternalInsumoImportServiceParityTests
{
    private static decimal MultiplicarEsperado(decimal precio, decimal cantidad, int decImp)
        => Math.Round(Math.Round(precio, decImp, MidpointRounding.AwayFromZero) * cantidad, decImp, MidpointRounding.AwayFromZero);

    [TestMethod]
    public void ImportSelected_BateriaMultiplicar_SemillaFija()
    {
        var rnd = new Random(20260822);

        for (int iter = 0; iter < 35; iter++)
        {
            using var current = TestDbFactory.CreateContext();
            var decImp = rnd.Next(0, 5);
            var proyecto = CrearProyecto($"Destino {iter}", decImp);
            current.Proyectos.Add(proyecto);
            current.SaveChanges();

            var puMat = rnd.Next(1, 1_000_000) / 1000m;
            var costoMaq = rnd.Next(1, 1_000_000) / 1000m;
            var cantidad = rnd.Next(1, 100000) / 10000m; // 0.0001–9.9999: estresa que Multiply no redondea cantidad

            var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_ins_{Guid.NewGuid():N}.db");
            try
            {
                int matId, maqId, auxId;
                using (var ctx = new SOPROContext(dbPath))
                {
                    ctx.Database.EnsureDeleted();
                    ctx.Database.EnsureCreated();
                    SchemaManager.EnsureCurrentSchema(ctx);

                    var proy = CrearProyecto($"Origen {iter}", 2);
                    ctx.Proyectos.Add(proy);
                    ctx.SaveChanges();

                    var material = new Material
                    {
                        ProyectoId = proy.Id,
                        Clave = "MAT-I",
                        Descripcion = string.Empty,
                        Unidad = "pza",
                        PrecioUnitario = puMat,
                        Notas = string.Empty,
                        Origen = OrigenInsumo.Proyecto
                    };
                    var maquina = new Maquinaria
                    {
                        ProyectoId = proy.Id,
                        Clave = "MAQ-I",
                        Descripcion = string.Empty,
                        CostoHorario = costoMaq,
                        Notas = string.Empty,
                        Origen = OrigenInsumo.Proyecto
                    };
                    ctx.Materiales.Add(material);
                    ctx.Maquinaria.Add(maquina);
                    ctx.SaveChanges();
                    matId = material.Id;
                    maqId = maquina.Id;

                    var aux = new Matriz
                    {
                        ProyectoId = proy.Id,
                        Clave = "APU-AUX-iter",
                        Descripcion = string.Empty,
                        Unidad = "pza",
                        Tipo = TipoMatriz.APU,
                        Notas = string.Empty
                    };
                    ctx.Matrices.Add(aux);
                    ctx.SaveChanges();
                    ctx.ComponentesMatriz.Add(new ComponenteMatriz
                    {
                        MatrizId = aux.Id,
                        TipoComponente = TipoComponenteMatriz.Material,
                        MaterialId = material.Id,
                        Cantidad = 1m,
                        Importe = 1m,
                        Orden = 1,
                        Notas = string.Empty
                    });
                    ctx.SaveChanges();
                    auxId = aux.Id;
                }

                var opciones = new[]
                {
                    new ExternalProjectInsumoOption
                    {
                        ItemId = matId,
                        ProjectName = "Origen",
                        ProjectPath = dbPath,
                        TipoComponente = TipoComponenteMatriz.Material,
                        Clave = "MAT-I",
                        PrecioUnitario = puMat
                    },
                    new ExternalProjectInsumoOption
                    {
                        ItemId = maqId,
                        ProjectName = "Origen",
                        ProjectPath = dbPath,
                        TipoComponente = TipoComponenteMatriz.Maquinaria,
                        Clave = "MAQ-I",
                        PrecioUnitario = costoMaq
                    },
                    new ExternalProjectInsumoOption
                    {
                        ItemId = auxId,
                        ProjectName = "Origen",
                        ProjectPath = dbPath,
                        TipoComponente = TipoComponenteMatriz.Auxiliar,
                        Clave = "APU-AUX-iter",
                        PrecioUnitario = 0m
                    }
                };

                var service = new ExternalInsumoImportService();
                var resultado = service.ImportSelected(
                    current, proyecto.Id, dbPath, opciones,
                    ExternalMatrixImportConflictPolicy.KeepBothWithTempKey, cantidad);

                var compMat = resultado.ImportedComponents.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
                Assert.AreEqual(MultiplicarEsperado(puMat, cantidad, decImp), compMat.Importe,
                    $"iter={iter} material");

                var compMaq = resultado.ImportedComponents.Single(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria);
                Assert.AreEqual(MultiplicarEsperado(costoMaq, cantidad, decImp), compMaq.Importe,
                    $"iter={iter} maquinaria");

                var compAux = resultado.ImportedComponents.Single(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar);
                var auxImportada = current.Matrices.Single(m => m.Id == compAux.AuxiliarId);
                // El básico importado tiene un componente material con el precio del material origen.
                var auxCdEsperado = MultiplicarEsperado(puMat, 1m, decImp);
                Assert.AreEqual(auxCdEsperado, auxImportada.CostoDirecto,
                    $"iter={iter} CD del básico importado (post-orden N5-12)");
                Assert.AreEqual(MultiplicarEsperado(auxImportada.CostoDirecto, cantidad, decImp), compAux.Importe,
                    $"iter={iter} auxiliar");
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
        }
    }

    [TestMethod]
    public void ImportSelected_Dorado_MaterialYAuxiliar()
    {
        using var current = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto("Destino 2 deci", 2);
        current.Proyectos.Add(proyecto);
        current.SaveChanges();

        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_ins_{Guid.NewGuid():N}.db");
        try
        {
            int matId, auxId;
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(ctx);

                var proy = CrearProyecto("Origen dorado", 2);
                ctx.Proyectos.Add(proy);
                ctx.SaveChanges();

                var material = new Material
                {
                    ProyectoId = proy.Id,
                    Clave = "MAT-D",
                    Descripcion = string.Empty,
                    Unidad = "pza",
                    PrecioUnitario = 10.005m,
                    Notas = string.Empty,
                    Origen = OrigenInsumo.Proyecto
                };
                ctx.Materiales.Add(material);
                ctx.SaveChanges();
                matId = material.Id;

                var aux = new Matriz
                {
                    ProyectoId = proy.Id,
                    Clave = "APU-AUX-D",
                    Descripcion = string.Empty,
                    Unidad = "pza",
                    Tipo = TipoMatriz.APU,
                    Notas = string.Empty
                };
                ctx.Matrices.Add(aux);
                ctx.SaveChanges();
                ctx.ComponentesMatriz.Add(new ComponenteMatriz
                {
                    MatrizId = aux.Id,
                    TipoComponente = TipoComponenteMatriz.Material,
                    MaterialId = material.Id,
                    Cantidad = 1m,
                    Importe = 1m,
                    Orden = 1,
                    Notas = string.Empty
                });
                ctx.SaveChanges();
                auxId = aux.Id;
            }

            var opciones = new[]
            {
                new ExternalProjectInsumoOption
                {
                    ItemId = matId,
                    ProjectName = "Origen",
                    ProjectPath = dbPath,
                    TipoComponente = TipoComponenteMatriz.Material,
                    Clave = "MAT-D",
                    PrecioUnitario = 10.005m
                },
                new ExternalProjectInsumoOption
                {
                    ItemId = auxId,
                    ProjectName = "Origen",
                    ProjectPath = dbPath,
                    TipoComponente = TipoComponenteMatriz.Auxiliar,
                    Clave = "APU-AUX-D",
                    PrecioUnitario = 0m
                }
            };

            var service = new ExternalInsumoImportService();
            var resultado = service.ImportSelected(
                current, proyecto.Id, dbPath, opciones,
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey, 2m);

            var compMat = resultado.ImportedComponents.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
            // PU visible 10.01 → 2 × 10.01 = 20.02
            Assert.AreEqual(20.02m, compMat.Importe);

            var compAux = resultado.ImportedComponents.Single(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar);
            var auxImportada = current.Matrices.Single(m => m.Id == compAux.AuxiliarId);
            // básico: material 10.005 → 10.01; raíz aux cant 2 → 2 × 10.01 = 20.02
            Assert.AreEqual(10.01m, auxImportada.CostoDirecto);
            Assert.AreEqual(20.02m, compAux.Importe);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

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
}