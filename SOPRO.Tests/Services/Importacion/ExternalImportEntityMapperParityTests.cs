using System;
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
/// [N7-11] Diferencial previo a la consolidación: el mismo insumo externo importado
/// por ExternalMatrixImportService (árbol) y por ExternalInsumoImportService
/// (selección) debe producir entidades con todos los campos mapeados idénticos.
/// Si ambas rutas divergen en algún campo, este test lo delata antes de unificarlas
/// en ExternalImportEntityMapper.
/// </summary>
[TestClass]
public class ExternalImportEntityMapperParityTests
{
    [TestMethod]
    public void ImportarMismoInsumoPorAmbasRutas_ProduceMismosCampos()
    {
        using var currentA = TestDbFactory.CreateContext();
        using var currentB = TestDbFactory.CreateContext();
        var proyectoA = CrearProyecto(currentA, "Destino A");
        var proyectoB = CrearProyecto(currentB, "Destino B");

        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_map_{Guid.NewGuid():N}.db");
        int rootId, matId, moId, maqId, herId;
        try
        {
            using (var ctx = new SOPROContext(dbPath))
            {
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();
                SchemaManager.EnsureCurrentSchema(ctx);
                var proy = CrearProyecto(ctx, "Origen");
                (rootId, matId, moId, maqId, herId) = CrearFuente(ctx, proy);
            }

            new ExternalMatrixImportService().ImportMatrixTree(
                currentA, proyectoA.Id, dbPath, rootId,
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey);

            var opciones = new[]
            {
                new ExternalProjectInsumoOption { ItemId = matId, TipoComponente = TipoComponenteMatriz.Material },
                new ExternalProjectInsumoOption { ItemId = moId, TipoComponente = TipoComponenteMatriz.ManoDeObra },
                new ExternalProjectInsumoOption { ItemId = maqId, TipoComponente = TipoComponenteMatriz.Maquinaria },
                new ExternalProjectInsumoOption { ItemId = herId, TipoComponente = TipoComponenteMatriz.Herramienta }
            };
            new ExternalInsumoImportService().ImportSelected(
                currentB, proyectoB.Id, dbPath, opciones,
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey, 1m);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }

        CompararMaterial(
            currentA.Materiales.AsNoTracking().Single(m => m.Clave == "MAT-MAP"),
            currentB.Materiales.AsNoTracking().Single(m => m.Clave == "MAT-MAP"),
            proyectoA.Id, proyectoB.Id);
        CompararManoDeObra(
            currentA.ManoDeObra.AsNoTracking().Single(m => m.Clave == "MO-MAP"),
            currentB.ManoDeObra.AsNoTracking().Single(m => m.Clave == "MO-MAP"),
            proyectoA.Id, proyectoB.Id);
        CompararMaquinaria(
            currentA.Maquinaria.AsNoTracking().Single(m => m.Clave == "MAQ-MAP"),
            currentB.Maquinaria.AsNoTracking().Single(m => m.Clave == "MAQ-MAP"),
            proyectoA.Id, proyectoB.Id);
        CompararHerramienta(
            currentA.Herramientas.AsNoTracking().Single(m => m.Clave == "HER-MAP"),
            currentB.Herramientas.AsNoTracking().Single(m => m.Clave == "HER-MAP"),
            proyectoA.Id, proyectoB.Id);
    }

    private static void CompararMaterial(Material a, Material b, int proyectoA, int proyectoB)
    {
        Assert.AreEqual(a.Clave, b.Clave);
        Assert.AreEqual(a.Descripcion, b.Descripcion);
        Assert.AreEqual(a.Unidad, b.Unidad);
        Assert.AreEqual(a.PrecioUnitario, b.PrecioUnitario);
        Assert.AreEqual(proyectoA, a.ProyectoId);
        Assert.AreEqual(proyectoB, b.ProyectoId);
        Assert.AreEqual(OrigenInsumo.Proyecto, a.Origen);
        Assert.AreEqual(OrigenInsumo.Proyecto, b.Origen);
        Assert.IsNull(a.MaterialMaestroId);
        Assert.IsNull(b.MaterialMaestroId);
        Assert.AreEqual(a.Notas, b.Notas);
        Assert.IsTrue(DateTime.Now - a.FechaModificacion < TimeSpan.FromMinutes(5));
        Assert.IsTrue(DateTime.Now - b.FechaModificacion < TimeSpan.FromMinutes(5));
    }

    private static void CompararManoDeObra(ManoDeObra a, ManoDeObra b, int proyectoA, int proyectoB)
    {
        Assert.AreEqual(a.Clave, b.Clave);
        Assert.AreEqual(a.Descripcion, b.Descripcion);
        Assert.AreEqual(a.Unidad, b.Unidad);
        Assert.AreEqual(a.SalarioBase, b.SalarioBase);
        Assert.AreEqual(a.FactorSalarioReal, b.FactorSalarioReal);
        Assert.AreEqual(a.SalarioReal, b.SalarioReal);
        Assert.AreEqual(proyectoA, a.ProyectoId);
        Assert.AreEqual(proyectoB, b.ProyectoId);
        Assert.AreEqual(OrigenInsumo.Proyecto, a.Origen);
        Assert.AreEqual(OrigenInsumo.Proyecto, b.Origen);
        Assert.IsNull(a.ManoDeObraMaestraId);
        Assert.IsNull(b.ManoDeObraMaestraId);
        Assert.AreEqual(a.Notas, b.Notas);
        Assert.IsTrue(DateTime.Now - a.FechaModificacion < TimeSpan.FromMinutes(5));
        Assert.IsTrue(DateTime.Now - b.FechaModificacion < TimeSpan.FromMinutes(5));
    }

    private static void CompararMaquinaria(Maquinaria a, Maquinaria b, int proyectoA, int proyectoB)
    {
        Assert.AreEqual(a.Clave, b.Clave);
        Assert.AreEqual(a.Descripcion, b.Descripcion);
        Assert.AreEqual(a.PotenciaNominal, b.PotenciaNominal);
        Assert.AreEqual(a.TipoCombustible, b.TipoCombustible);
        Assert.AreEqual(a.ValorAdquisicion, b.ValorAdquisicion);
        Assert.AreEqual(a.ValorLlantas, b.ValorLlantas);
        Assert.AreEqual(a.ValorPiezasEspeciales, b.ValorPiezasEspeciales);
        Assert.AreEqual(a.FactorRescate, b.FactorRescate);
        Assert.AreEqual(a.VidaEconomica, b.VidaEconomica);
        Assert.AreEqual(a.TasaInteres, b.TasaInteres);
        Assert.AreEqual(a.HorasEfectivasAnio, b.HorasEfectivasAnio);
        Assert.AreEqual(a.PrimaSeguro, b.PrimaSeguro);
        Assert.AreEqual(a.FactorMantenimiento, b.FactorMantenimiento);
        Assert.AreEqual(a.CantidadCombustible, b.CantidadCombustible);
        Assert.AreEqual(a.PrecioCombustible, b.PrecioCombustible);
        Assert.AreEqual(a.CantidadAceite, b.CantidadAceite);
        Assert.AreEqual(a.PrecioAceite, b.PrecioAceite);
        Assert.AreEqual(a.NumeroLlantas, b.NumeroLlantas);
        Assert.AreEqual(a.VidaEconomicaLlantas, b.VidaEconomicaLlantas);
        Assert.AreEqual(a.VidaPiezasEspeciales, b.VidaPiezasEspeciales);
        Assert.AreEqual(a.SalarioOperador, b.SalarioOperador);
        Assert.AreEqual(a.FactorSalarioReal, b.FactorSalarioReal);
        Assert.AreEqual(a.HorasEfectivasTurno, b.HorasEfectivasTurno);
        Assert.AreEqual(a.CostoHorario, b.CostoHorario);
        Assert.AreEqual(a.EsCostoCalculado, b.EsCostoCalculado);
        Assert.AreEqual(proyectoA, a.ProyectoId);
        Assert.AreEqual(proyectoB, b.ProyectoId);
        Assert.AreEqual(OrigenInsumo.Proyecto, a.Origen);
        Assert.AreEqual(OrigenInsumo.Proyecto, b.Origen);
        Assert.IsNull(a.MaquinariaMaestraId);
        Assert.IsNull(b.MaquinariaMaestraId);
        Assert.AreEqual(a.Notas, b.Notas);
        Assert.AreEqual(a.FechaCalculoCosto, b.FechaCalculoCosto);
        Assert.IsTrue(DateTime.Now - a.FechaModificacion < TimeSpan.FromMinutes(5));
        Assert.IsTrue(DateTime.Now - b.FechaModificacion < TimeSpan.FromMinutes(5));
    }

    private static void CompararHerramienta(Herramienta a, Herramienta b, int proyectoA, int proyectoB)
    {
        Assert.AreEqual(a.Clave, b.Clave);
        Assert.AreEqual(a.Descripcion, b.Descripcion);
        Assert.AreEqual(a.Unidad, b.Unidad);
        Assert.AreEqual(a.PrecioUnitario, b.PrecioUnitario);
        Assert.AreEqual(proyectoA, a.ProyectoId);
        Assert.AreEqual(proyectoB, b.ProyectoId);
        Assert.AreEqual(OrigenInsumo.Proyecto, a.Origen);
        Assert.AreEqual(OrigenInsumo.Proyecto, b.Origen);
        Assert.AreEqual(a.Notas, b.Notas);
        Assert.IsTrue(DateTime.Now - a.FechaModificacion < TimeSpan.FromMinutes(5));
        Assert.IsTrue(DateTime.Now - b.FechaModificacion < TimeSpan.FromMinutes(5));
    }

    private static (int RootId, int MatId, int MoId, int MaqId, int HerId) CrearFuente(SOPROContext ctx, Proyecto proy)
    {
        var mat = new Material
        {
            ProyectoId = proy.Id, Clave = "mat-map", Descripcion = "Material mapa",
            Unidad = "kg", PrecioUnitario = 12.345m, Notas = "nota mat"
        };
        var mo = new ManoDeObra
        {
            ProyectoId = proy.Id, Clave = "mo-map", Descripcion = "MO mapa",
            Unidad = "jor", SalarioBase = 20m, FactorSalarioReal = 1.5m, SalarioReal = 30m,
            Notas = "nota mo"
        };
        var maq = new Maquinaria
        {
            ProyectoId = proy.Id, Clave = "maq-map", Descripcion = "Maq mapa",
            PotenciaNominal = 100m, TipoCombustible = TipoCombustible.Diesel,
            ValorAdquisicion = 50000m, ValorLlantas = 2000m, ValorPiezasEspeciales = 1000m,
            FactorRescate = 0.1m, VidaEconomica = 12000m, TasaInteres = 21.24m,
            HorasEfectivasAnio = 1600m, PrimaSeguro = 3m, FactorMantenimiento = 0.2m,
            CantidadCombustible = 5m, PrecioCombustible = 22m, CantidadAceite = 1m,
            PrecioAceite = 90m, NumeroLlantas = 4, VidaEconomicaLlantas = 3000m,
            VidaPiezasEspeciales = 4000m, SalarioOperador = 250m, FactorSalarioReal = 1.6543m,
            HorasEfectivasTurno = 8m, CostoHorario = 345.67m, EsCostoCalculado = true,
            FechaCalculoCosto = new DateTime(2026, 1, 2), Notas = "nota maq"
        };
        var her = new Herramienta
        {
            ProyectoId = proy.Id, Clave = "her-map", Descripcion = "Her mapa",
            Unidad = "pza", PrecioUnitario = 7.77m, Notas = "nota her"
        };
        ctx.Materiales.Add(mat);
        ctx.ManoDeObra.Add(mo);
        ctx.Maquinaria.Add(maq);
        ctx.Herramientas.Add(her);
        ctx.SaveChanges();

        var raiz = new Matriz
        {
            ProyectoId = proy.Id, Clave = "APU-MAP", Descripcion = string.Empty,
            Unidad = "m2", Tipo = TipoMatriz.APU, Notas = string.Empty
        };
        ctx.Matrices.Add(raiz);
        ctx.SaveChanges();
        ctx.ComponentesMatriz.AddRange(
            new ComponenteMatriz
            {
                MatrizId = raiz.Id, TipoComponente = TipoComponenteMatriz.Material,
                MaterialId = mat.Id, Cantidad = 1m, Importe = 1m, Orden = 1, Notas = string.Empty
            },
            new ComponenteMatriz
            {
                MatrizId = raiz.Id, TipoComponente = TipoComponenteMatriz.ManoDeObra,
                ManoDeObraId = mo.Id, Cantidad = 1m, Importe = 1m, Orden = 2, Notas = string.Empty
            },
            new ComponenteMatriz
            {
                MatrizId = raiz.Id, TipoComponente = TipoComponenteMatriz.Maquinaria,
                MaquinariaId = maq.Id, Cantidad = 1m, Importe = 1m, Orden = 3, Notas = string.Empty
            },
            new ComponenteMatriz
            {
                MatrizId = raiz.Id, TipoComponente = TipoComponenteMatriz.Herramienta,
                HerramientaId = her.Id, Cantidad = 1m, Importe = 1m, Orden = 4, Notas = string.Empty
            });
        ctx.SaveChanges();

        return (raiz.Id, mat.Id, mo.Id, maq.Id, her.Id);
    }

    private static Proyecto CrearProyecto(SOPROContext ctx, string nombre)
    {
        // TestDbFactory.CreateContext() ya siembra proyecto(s); se crea uno dedicado.
        var p = new Proyecto
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
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4
        };
        ctx.Proyectos.Add(p);
        ctx.SaveChanges();
        return p;
    }
}
