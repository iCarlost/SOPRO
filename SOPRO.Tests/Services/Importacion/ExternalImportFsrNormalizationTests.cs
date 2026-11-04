using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Importacion;

/// <summary>
/// Normalización del FSR de mano de obra importada con los parámetros del proyecto
/// destino: se preserva el salario base y la procedencia, el FSR se redondea a la
/// precisión persistida (6 decimales) y el salario real se recalcula. Sin parámetros
/// destino (o en filas <c>%MO</c>) se conserva el FSR/salario real de origen.
/// </summary>
[TestClass]
public class ExternalImportFsrNormalizationTests
{
    private const string ParametrosDorados = """
    {
      "SalarioMinimo":"248.93",
      "Jornada":"0",
      "Semestre":"0",
      "Anio":"2026",
      "HorasJornada":"8",
      "DiasCalendario":"365",
      "DiasAguinaldo":"15",
      "DiasVacaciones":"12",
      "PrimaVacacional":"25",
      "DiasDescanso":"52",
      "DiasFestivos":"7",
      "PctGuarderias":"1",
      "PctRetiro":"2",
      "PctRiesgos":"4.58875",
      "PctINFONAVIT":"5",
      "PctNomina":"2.4"
    }
    """;

    // 500 × 1.730824 = 865.412. Con precisión de importe 2:
    // componente MO (cantidad 2) = 2 × 865.41 = 1730.82; costo directo raíz = 1730.82.
    private const decimal FsrDestino = 1.730824m;
    private const decimal SalarioRealDestino = 865.412m;
    private const decimal ImporteComponenteDestino = 1730.82m;

    [TestMethod]
    public void ImportMatrixTree_ConParametrosDestino_NormalizaFsrSalarioRealEImportes()
    {
        using var current = TestDbFactory.CreateContext();
        var destino = CrearProyecto("Destino FSR", 2, ParametrosDorados);
        current.Proyectos.Add(destino);
        current.SaveChanges();

        var dbPath = NuevaRutaFuente();
        try
        {
            var (_, matrizId) = CrearFuente(dbPath, "Origen FSR");

            var resultado = new ExternalMatrixImportService().ImportMatrixTree(
                current, destino.Id, dbPath, matrizId,
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey);

            var mano = current.ManoDeObra.AsNoTracking().Single(m => m.Clave == "MO-FSR");
            Assert.AreEqual(500m, mano.SalarioBase, "El salario base de origen se conserva.");
            Assert.AreEqual(FsrDestino, mano.FactorSalarioReal, "El FSR debe normalizarse con los parámetros del destino.");
            Assert.AreEqual(SalarioRealDestino, mano.SalarioReal, "El salario real debe recalcularse con el FSR destino.");
            Assert.AreEqual(destino.Id, mano.ProyectoId);
            Assert.AreEqual(OrigenInsumo.Proyecto, mano.Origen);

            var raiz = current.Matrices
                .Include(m => m.Componentes)
                .Single(m => m.Id == resultado.RootMatrixId);
            var componenteMano = raiz.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.ManoDeObra);
            Assert.AreEqual(ImporteComponenteDestino, componenteMano.Importe,
                "El importe del componente de MO debe recalcularse con el salario real destino.");
            Assert.AreEqual(ImporteComponenteDestino, raiz.CostoDirecto,
                "El costo directo de la raíz debe usar la precisión del destino.");

            VerificarFuenteIntacta(dbPath);
        }
        finally
        {
            BorrarFuente(dbPath);
        }
    }

    [TestMethod]
    public void ImportMatrixTree_SinParametrosDestino_PreservaFsrYSalarioRealDeOrigen()
    {
        using var current = TestDbFactory.CreateContext();
        var destino = CrearProyecto("Destino sin FSR", 2, parametrosFSR: null);
        current.Proyectos.Add(destino);
        current.SaveChanges();

        var dbPath = NuevaRutaFuente();
        try
        {
            var (_, matrizId) = CrearFuente(dbPath, "Origen sin FSR");

            var resultado = new ExternalMatrixImportService().ImportMatrixTree(
                current, destino.Id, dbPath, matrizId,
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey);

            var mano = current.ManoDeObra.AsNoTracking().Single(m => m.Clave == "MO-FSR");
            Assert.AreEqual(500m, mano.SalarioBase);
            Assert.AreEqual(1.1m, mano.FactorSalarioReal, "Sin parámetros destino se conserva el FSR de origen.");
            Assert.AreEqual(550m, mano.SalarioReal, "Sin parámetros destino se conserva el salario real de origen.");

            var raiz = current.Matrices
                .Include(m => m.Componentes)
                .Single(m => m.Id == resultado.RootMatrixId);
            Assert.AreEqual(1100m, raiz.Componentes.Single().Importe);
            Assert.AreEqual(1100m, raiz.CostoDirecto);
        }
        finally
        {
            BorrarFuente(dbPath);
        }
    }

    [TestMethod]
    public void ImportSelected_ManoDeObra_NormalizaResultadoYNavegacionPersistida()
    {
        using var current = TestDbFactory.CreateContext();
        var destino = CrearProyecto("Destino directo FSR", 2, ParametrosDorados);
        current.Proyectos.Add(destino);
        current.SaveChanges();

        var dbPath = NuevaRutaFuente();
        try
        {
            var (manoId, _) = CrearFuente(dbPath, "Origen directo FSR");
            var opcion = new ExternalProjectInsumoOption
            {
                ItemId = manoId,
                ProjectName = "Origen directo FSR",
                ProjectPath = dbPath,
                TipoComponente = TipoComponenteMatriz.ManoDeObra,
                Clave = "MO-FSR"
            };

            var resultado = new ExternalInsumoImportService().ImportSelected(
                current, destino.Id, dbPath, new[] { opcion },
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey, cantidad: 2m);

            var componente = resultado.ImportedComponents
                .Single(c => c.TipoComponente == TipoComponenteMatriz.ManoDeObra);
            Assert.IsNotNull(componente.ManoDeObra, "El resultado debe navegar a la MO importada.");
            Assert.AreEqual(FsrDestino, componente.ManoDeObra!.FactorSalarioReal);
            Assert.AreEqual(SalarioRealDestino, componente.ManoDeObra.SalarioReal);

            var persistida = current.ManoDeObra.AsNoTracking().Single(m => m.Id == componente.ManoDeObraId);
            Assert.AreEqual(500m, persistida.SalarioBase);
            Assert.AreEqual(FsrDestino, persistida.FactorSalarioReal);
            Assert.AreEqual(SalarioRealDestino, persistida.SalarioReal);
        }
        finally
        {
            BorrarFuente(dbPath);
        }
    }

    [TestMethod]
    public void ImportSelected_PorcentajeManoDeObra_NoAlteraFsrNiSalarioReal()
    {
        using var current = TestDbFactory.CreateContext();
        var destino = CrearProyecto("Destino %MO", 2, ParametrosDorados);
        current.Proyectos.Add(destino);
        current.SaveChanges();

        var dbPath = NuevaRutaFuente();
        try
        {
            var manoId = CrearFuentePorcentajeManoDeObra(dbPath, "Origen %MO");
            var opcion = new ExternalProjectInsumoOption
            {
                ItemId = manoId,
                ProjectName = "Origen %MO",
                ProjectPath = dbPath,
                TipoComponente = TipoComponenteMatriz.ManoDeObra,
                Clave = "MO-PCT"
            };

            var resultado = new ExternalInsumoImportService().ImportSelected(
                current, destino.Id, dbPath, new[] { opcion },
                ExternalMatrixImportConflictPolicy.KeepBothWithTempKey, cantidad: 1m);

            var mano = resultado.ImportedComponents
                .Single(c => c.TipoComponente == TipoComponenteMatriz.ManoDeObra)
                .ManoDeObra;
            Assert.IsNotNull(mano);
            Assert.AreEqual("%MO", mano!.Unidad);
            Assert.AreEqual(1.1m, mano.FactorSalarioReal, "Una fila %MO no debe normalizarse con el FSR destino.");
            Assert.AreEqual(550m, mano.SalarioReal);
        }
        finally
        {
            BorrarFuente(dbPath);
        }
    }

    [TestMethod]
    public void ImportMatrixTree_ReplaceExisting_PropagaOtraMatrizYConceptoDestino()
    {
        using var current = TestDbFactory.CreateContext();
        var destino = CrearProyecto("Destino replace FSR", 2, ParametrosDorados);
        current.Proyectos.Add(destino);
        current.SaveChanges();

        var manoDestino = new ManoDeObra
        {
            ProyectoId = destino.Id,
            Clave = "MO-FSR",
            Descripcion = "MO destino",
            Unidad = "jor",
            SalarioBase = 100m,
            FactorSalarioReal = 1m,
            SalarioReal = 100m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        };
        var matrizDestino = new Matriz
        {
            ProyectoId = destino.Id,
            Clave = "APU-DESTINO",
            Descripcion = "Consumidor destino",
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 200m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        };
        current.ManoDeObra.Add(manoDestino);
        current.Matrices.Add(matrizDestino);
        current.SaveChanges();

        current.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matrizDestino.Id,
            TipoComponente = TipoComponenteMatriz.ManoDeObra,
            ManoDeObraId = manoDestino.Id,
            Cantidad = 2m,
            Importe = 200m,
            Orden = 1,
            Notas = string.Empty
        });
        var conceptoDestino = new ConceptoPresupuesto
        {
            ProyectoId = destino.Id,
            Clave = "C-1",
            Descripcion = "Concepto destino",
            Unidad = "m2",
            Nivel = 5,
            EsAgrupador = false,
            Orden = 1,
            Cantidad = 1m,
            MatrizId = matrizDestino.Id,
            CostoDirectoUnitario = 200m,
            CostoDirectoTotal = 200m,
            PrecioUnitario = 200m,
            ImporteTotal = 200m
        };
        current.ConceptosPresupuesto.Add(conceptoDestino);
        current.SaveChanges();

        var dbPath = NuevaRutaFuente();
        try
        {
            var (_, matrizOrigenId) = CrearFuente(dbPath, "Origen replace FSR");

            new ExternalMatrixImportService().ImportMatrixTree(
                current, destino.Id, dbPath, matrizOrigenId,
                ExternalMatrixImportConflictPolicy.ReplaceExisting);

            current.ChangeTracker.Clear();

            var manoActualizada = current.ManoDeObra.AsNoTracking().Single(m => m.Id == manoDestino.Id);
            Assert.AreEqual(500m, manoActualizada.SalarioBase);
            Assert.AreEqual(FsrDestino, manoActualizada.FactorSalarioReal);
            Assert.AreEqual(SalarioRealDestino, manoActualizada.SalarioReal);

            var consumidor = current.Matrices
                .Include(m => m.Componentes)
                .AsNoTracking()
                .Single(m => m.Id == matrizDestino.Id);
            Assert.AreEqual(ImporteComponenteDestino, consumidor.Componentes.Single().Importe,
                "La MO reemplazada debe propagarse a la matriz destino que ya la consumía.");
            Assert.AreEqual(ImporteComponenteDestino, consumidor.CostoDirecto);

            var conceptoActualizado = current.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoDestino.Id);
            Assert.AreEqual(ImporteComponenteDestino, conceptoActualizado.CostoDirectoUnitario);
            Assert.AreEqual(ImporteComponenteDestino, conceptoActualizado.CostoDirectoTotal);
            Assert.AreEqual(ImporteComponenteDestino, conceptoActualizado.ImporteTotal);
        }
        finally
        {
            BorrarFuente(dbPath);
        }
    }

    // ── Ayudantes ─────────────────────────────────────────────────────────────

    private static Proyecto CrearProyecto(string nombre, int decimalesImporte, string? parametrosFSR)
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
            DecimalesPorcentaje = 4,
            ParametrosFSR = parametrosFSR
        };
    }

    private static string NuevaRutaFuente()
        => Path.Combine(Path.GetTempPath(), $"sopro_fsr_{Guid.NewGuid():N}.db");

    private static (int ManoId, int MatrizId) CrearFuente(string dbPath, string nombreProyecto)
    {
        using var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        SchemaManager.EnsureCurrentSchema(ctx);

        var proy = CrearProyecto(nombreProyecto, 2, parametrosFSR: null);
        ctx.Proyectos.Add(proy);
        ctx.SaveChanges();

        var mano = new ManoDeObra
        {
            ProyectoId = proy.Id,
            Clave = "MO-FSR",
            Descripcion = "Mano de obra fuente",
            Unidad = "jor",
            SalarioBase = 500m,
            FactorSalarioReal = 1.1m,
            SalarioReal = 550m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        };
        ctx.ManoDeObra.Add(mano);
        ctx.SaveChanges();

        var matriz = new Matriz
        {
            ProyectoId = proy.Id,
            Clave = "APU-FSR",
            Descripcion = "APU fuente FSR",
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 1100m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        };
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();

        ctx.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.ManoDeObra,
            ManoDeObraId = mano.Id,
            Cantidad = 2m,
            Importe = 1100m,
            Orden = 1,
            Notas = string.Empty
        });
        ctx.SaveChanges();

        return (mano.Id, matriz.Id);
    }

    private static int CrearFuentePorcentajeManoDeObra(string dbPath, string nombreProyecto)
    {
        using var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        SchemaManager.EnsureCurrentSchema(ctx);

        var proy = CrearProyecto(nombreProyecto, 2, parametrosFSR: null);
        ctx.Proyectos.Add(proy);
        ctx.SaveChanges();

        var mano = new ManoDeObra
        {
            ProyectoId = proy.Id,
            Clave = "MO-PCT",
            Descripcion = "Cabo de oficio %MO",
            Unidad = "%MO",
            SalarioBase = 500m,
            FactorSalarioReal = 1.1m,
            SalarioReal = 550m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        };
        ctx.ManoDeObra.Add(mano);
        ctx.SaveChanges();

        return mano.Id;
    }

    private static void VerificarFuenteIntacta(string dbPath)
    {
        using var fuente = new ProjectDbContextFactory().CreateReadOnly(dbPath);

        var mano = fuente.ManoDeObra.AsNoTracking().Single(m => m.Clave == "MO-FSR");
        Assert.AreEqual(500m, mano.SalarioBase);
        Assert.AreEqual(1.1m, mano.FactorSalarioReal, "La fuente no debe normalizarse.");
        Assert.AreEqual(550m, mano.SalarioReal, "La fuente no debe recalcularse.");
    }

    private static void BorrarFuente(string dbPath)
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var file = dbPath + suffix;
            if (File.Exists(file))
                File.Delete(file);
        }
    }
}
