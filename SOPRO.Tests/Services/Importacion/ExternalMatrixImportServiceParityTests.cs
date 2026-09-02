using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Importacion;

// [N5-12] Paridad de ExternalMatrixImportService contra la fachada:
// el único motor del servicio es RoundAmount con la precisión del proyecto
// destino, aplicado por la ruta canónica (N7-1d: MatrixGraphOrderService +
// PricePropagationService.RecalcularConMotor; post-orden: auxiliares antes que
// padres). El oráculo de la batería es independiente: se calcula únicamente con
// Math.Round sobre el Recalculate compartido (N5-2), sin engine ni fachada,
// recorriendo el árbol importado en el mismo orden post-orden para validar que
// el CostoDirecto redondeado del auxiliar es el que consume su matriz padre.

[TestClass]
public class ExternalMatrixImportServiceParityTests
{
    [TestMethod]
    public void ImportMatrixTree_BateriaRedondeoPostOrden_SemillaFija()
    {
        var rnd = new Random(20260821);

        for (int iter = 0; iter < 25; iter++)
        {
            using var current = TestDbFactory.CreateContext();
            var decImp = rnd.Next(0, 5);
            var proyecto = CrearProyecto($"Destino {iter}", decImp);
            current.Proyectos.Add(proyecto);
            current.SaveChanges();

            var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_ext_{Guid.NewGuid():N}.db");
            try
            {
                int rootId;
                using (var ctx = new SOPROContext(dbPath))
                {
                    ctx.Database.EnsureDeleted();
                    ctx.Database.EnsureCreated();
                    SchemaManager.EnsureCurrentSchema(ctx);

                    var proy = CrearProyecto($"Origen {iter}", 2);
                    ctx.Proyectos.Add(proy);
                    ctx.SaveChanges();

                    // Árbol de 3 niveles: raíz → auxiliar → auxiliar.
                    rootId = CrearArbolExterno(ctx, proy, rnd, iter);
                }

                var service = new ExternalMatrixImportService();
                var resultado = service.ImportMatrixTree(
                    current, proyecto.Id, dbPath, rootId,
                    ExternalMatrixImportConflictPolicy.KeepBothWithTempKey);

                var importadas = current.Matrices
                    .Include(m => m.Componentes).ThenInclude(c => c.Material)
                    .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                    .ToList();
                var raiz = importadas.Single(m => m.Id == resultado.RootMatrixId);
                var visitadas = new HashSet<int>();
                EsperadoPostOrden(importadas, raiz, decImp, visitadas, iter);

                Assert.AreEqual(3, importadas.Count, $"iter={iter} deben importarse 3 matrices.");
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
        }
    }

    [TestMethod]
    public void ImportMatrixTree_Dorado_PrecisionDestinoConAuxiliar()
    {
        using var current = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto("Destino 2 deci", 2);
        current.Proyectos.Add(proyecto);
        current.SaveChanges();

        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_ext_{Guid.NewGuid():N}.db");
        try
        {
            int rootId;
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(ctx);

                var proy = CrearProyecto("Origen dorado", 2);
                ctx.Proyectos.Add(proy);
                ctx.SaveChanges();

                var materialRaiz = CrearMaterial(ctx, proy, "MAT-R", 10.005m);
                var materialHijo = CrearMaterial(ctx, proy, "MAT-H", 20.004m);

                var hijo = new Matriz
                {
                    ProyectoId = proy.Id,
                    Clave = "APU-H",
                    Descripcion = string.Empty,
                    Unidad = "m2",
                    Tipo = TipoMatriz.APU,
                    Notas = string.Empty
                };
                ctx.Matrices.Add(hijo);
                ctx.SaveChanges();
                ctx.ComponentesMatriz.Add(new ComponenteMatriz
                {
                    MatrizId = hijo.Id,
                    TipoComponente = TipoComponenteMatriz.Material,
                    MaterialId = materialHijo.Id,
                    Cantidad = 2m,
                    Importe = 40.008m,
                    Orden = 1,
                    Notas = string.Empty
                });
                ctx.SaveChanges();

                var raiz = new Matriz
                {
                    ProyectoId = proy.Id,
                    Clave = "APU-R",
                    Descripcion = string.Empty,
                    Unidad = "m2",
                    Tipo = TipoMatriz.APU,
                    Notas = string.Empty
                };
                ctx.Matrices.Add(raiz);
                ctx.SaveChanges();
                ctx.ComponentesMatriz.AddRange(
                    new ComponenteMatriz
                    {
                        MatrizId = raiz.Id,
                        TipoComponente = TipoComponenteMatriz.Material,
                        MaterialId = materialRaiz.Id,
                        Cantidad = 3m,
                        Importe = 30.015m,
                        Orden = 1,
                        Notas = string.Empty
                    },
                    new ComponenteMatriz
                    {
                        MatrizId = raiz.Id,
                        TipoComponente = TipoComponenteMatriz.Auxiliar,
                        AuxiliarId = hijo.Id,
                        Cantidad = 2m,
                        Importe = 80.016m,
                        Orden = 2,
                        Notas = string.Empty
                    });
                ctx.SaveChanges();
                rootId = raiz.Id;
            }

            var service = new ExternalMatrixImportService();
            var resultado = service.ImportMatrixTree(
                current, proyecto.Id, dbPath, rootId,
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey);

            var importadas = current.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .ToList();
            var raizImportada = importadas.Single(m => m.Id == resultado.RootMatrixId);
            var hijoImportado = importadas.Single(m => m.Clave == "APU-H");

            // Post-orden con precisión 2 del destino:
            //   hijo: 2 × redondear(20.004) = 2 × 20.00 = 40.00
            //   raíz: 3 × redondear(10.005) = 3 × 10.01 = 30.03
            //          auxiliar 2 × 40.00 = 80.00  → CD 110.03
            // (el CostoDirecto redondeado del hijo, 40.00, es el que consume la raíz)
            Assert.AreEqual(40.00m, hijoImportado.CostoDirecto);
            Assert.AreEqual(110.03m, raizImportada.CostoDirecto);

            Assert.AreEqual(30.03m, raizImportada.Componentes
                .Single(c => c.TipoComponente == TipoComponenteMatriz.Material).Importe);
            Assert.AreEqual(80.00m, raizImportada.Componentes
                .Single(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar).Importe);
            Assert.AreEqual(40.00m, hijoImportado.Componentes
                .Single(c => c.TipoComponente == TipoComponenteMatriz.Material).Importe);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [TestMethod]
    public void ImportMatrixTree_Dorado_MatrizSinComponentes_QuedaEnCero()
    {
        using var current = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto("Destino sin componentes", 2);
        current.Proyectos.Add(proyecto);
        current.SaveChanges();

        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_ext_{Guid.NewGuid():N}.db");
        try
        {
            int matrizId;
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(ctx);

                var proy = CrearProyecto("Origen vacia", 2);
                ctx.Proyectos.Add(proy);
                ctx.SaveChanges();

                var matriz = new Matriz
                {
                    ProyectoId = proy.Id,
                    Clave = "APU-V",
                    Descripcion = string.Empty,
                    Unidad = "m2",
                    Tipo = TipoMatriz.APU,
                    CostoDirecto = 123.45m,
                    Notas = string.Empty
                };
                ctx.Matrices.Add(matriz);
                ctx.SaveChanges();
                matrizId = matriz.Id;
            }

            var service = new ExternalMatrixImportService();
            var resultado = service.ImportMatrixTree(
                current, proyecto.Id, dbPath, matrizId,
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey);

            var importada = current.Matrices.Single(m => m.Id == resultado.RootMatrixId);
            Assert.AreEqual(0.00m, importada.CostoDirecto,
                "Una matriz sin componentes se recalcula a cero, no se copia el costo del origen.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    // ── Ayudantes ─────────────────────────────────────────────────────────────

    private static decimal EsperadoPostOrden(
        List<Matriz> importadas, Matriz matriz, int decImp, HashSet<int> visitadas, int iter)
    {
        if (!visitadas.Add(matriz.Id)) return 0m;

        foreach (var comp in matriz.Componentes.Where(c => c.AuxiliarId.HasValue))
        {
            var aux = importadas.Single(m => m.Id == comp.AuxiliarId!.Value);
            EsperadoPostOrden(importadas, aux, decImp, visitadas, iter);
        }

        var totals = MatrixComponentCalculationService.Recalculate(matriz.Componentes.ToList(), decImp);
        var esperado = Math.Round(totals.CostoDirectoTotal, decImp, MidpointRounding.AwayFromZero);
        Assert.AreEqual(esperado, matriz.CostoDirecto,
            $"iter={iter} {matriz.Clave} CostoDirecto debe ser el redondeo del recálculo post-orden (decImp={decImp}).");
        return esperado;
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

    private static Material CrearMaterial(SOPROContext ctx, Proyecto proy, string clave, decimal precio)
    {
        var material = new Material
        {
            ProyectoId = proy.Id,
            Clave = clave,
            Descripcion = string.Empty,
            Unidad = "pza",
            PrecioUnitario = precio,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        };
        ctx.Materiales.Add(material);
        ctx.SaveChanges();
        return material;
    }

    /// <summary>Crea raíz (2 materiales + 1 auxiliar) → hijo (1 material + 1 auxiliar) → nieto (1 material).</summary>
    private static int CrearArbolExterno(SOPROContext ctx, Proyecto proy, Random rnd, int iter)
    {
        var precios = Enumerable.Range(0, 4)
            .Select(_ => rnd.Next(1, 1_000_000) / 1000m)
            .ToList();

        var materiales = new List<Material>();
        for (int i = 0; i < 4; i++)
            materiales.Add(CrearMaterial(ctx, proy, $"MAT-{iter}-{i}", precios[i]));

        var nieto = new Matriz
        {
            ProyectoId = proy.Id,
            Clave = $"APU-{iter}-N",
            Descripcion = string.Empty,
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };
        ctx.Matrices.Add(nieto);
        ctx.SaveChanges();
        ctx.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = nieto.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = materiales[0].Id,
            Cantidad = Cantidad(rnd),
            Importe = 1m,
            Orden = 1,
            Notas = string.Empty
        });
        ctx.SaveChanges();

        var hijo = new Matriz
        {
            ProyectoId = proy.Id,
            Clave = $"APU-{iter}-H",
            Descripcion = string.Empty,
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };
        ctx.Matrices.Add(hijo);
        ctx.SaveChanges();
        ctx.ComponentesMatriz.AddRange(
            new ComponenteMatriz
            {
                MatrizId = hijo.Id,
                TipoComponente = TipoComponenteMatriz.Material,
                MaterialId = materiales[1].Id,
                Cantidad = Cantidad(rnd),
                Importe = 1m,
                Orden = 1,
                Notas = string.Empty
            },
            new ComponenteMatriz
            {
                MatrizId = hijo.Id,
                TipoComponente = TipoComponenteMatriz.Auxiliar,
                AuxiliarId = nieto.Id,
                Cantidad = Cantidad(rnd),
                Importe = 1m,
                Orden = 2,
                Notas = string.Empty
            });
        ctx.SaveChanges();

        var raiz = new Matriz
        {
            ProyectoId = proy.Id,
            Clave = $"APU-{iter}-R",
            Descripcion = string.Empty,
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };
        ctx.Matrices.Add(raiz);
        ctx.SaveChanges();
        ctx.ComponentesMatriz.AddRange(
            new ComponenteMatriz
            {
                MatrizId = raiz.Id,
                TipoComponente = TipoComponenteMatriz.Material,
                MaterialId = materiales[2].Id,
                Cantidad = Cantidad(rnd),
                Importe = 1m,
                Orden = 1,
                Notas = string.Empty
            },
            new ComponenteMatriz
            {
                MatrizId = raiz.Id,
                TipoComponente = TipoComponenteMatriz.Material,
                MaterialId = materiales[3].Id,
                Cantidad = Cantidad(rnd),
                Importe = 1m,
                Orden = 2,
                Notas = string.Empty
            },
            new ComponenteMatriz
            {
                MatrizId = raiz.Id,
                TipoComponente = TipoComponenteMatriz.Auxiliar,
                AuxiliarId = hijo.Id,
                Cantidad = Cantidad(rnd),
                Importe = 1m,
                Orden = 3,
                Notas = string.Empty
            });
        ctx.SaveChanges();
        return raiz.Id;
    }

    private static decimal Cantidad(Random rnd) => rnd.Next(1, 10_000) / 1000m;
}