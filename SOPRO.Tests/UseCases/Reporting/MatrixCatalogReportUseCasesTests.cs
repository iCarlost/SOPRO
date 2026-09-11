using System.Collections;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Models.Reporting.MatrixCatalog;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.UseCases.Reporting;

/// <summary>
/// Gate N7-18b: el modelo neutral de "Catálogo de Matrices" vive en Application
/// (no en Calculation) y su aritmética es una transformación pura de snapshots.
///
/// Cubre, con el escenario sintético discriminante de N0-TABLA filas 22-23:
///   - totalMO crudo (10.005 + 20.005 = 30.01) y %MO (30.01 × 0.10 = 3.001).
///   - importe crudo por componente (3 × 10.005 = 30.015) sin redondeo.
///   - orden ordinal por clave PESE a la entrada barajada.
///   - inmutabilidad real (ReadOnlyCollection, copia defensiva, sin mutar input).
///   - reloj explícito y cultura invariante para {fecha_impresion}.
///   - puente N7-18a: 30.015 / 30.01 / 3.001 (sintético) y ***REMOVED*** /
///     ***REMOVED*** / 7 matrices (proyecto real). NO compara celdas ni hashes:
///     eso es N7-18c cuando exista SOPRO.Reporting.
/// </summary>
[TestClass]
public class MatrixCatalogReportUseCasesTests
{
    private static readonly DateTime FechaFija = new(2026, 8, 27, 13, 45, 0);

    // ─────────────────────────── Helpers de datos ───────────────────────────

    private static MatrixCatalogProject ProyectoSintetico() => new(
        Nombre: "APU SINTETICO GOLDEN N7-18A",
        Descripcion: "Descripcion de prueba",
        Ubicacion: "Ubicacion de prueba",
        Convocante: "Convocante",
        Contratista: "Contratista",
        ApoderadoLegal: "Apoderado",
        FechaInicio: new DateTime(2026, 3, 1),
        FechaTermino: new DateTime(2026, 3, 31),
        PlazoEjecucion: 31);

    private static MatrixCatalogTemplate PlantillaSintetica() => new(
        EncabezadoIzq: new MatrixCatalogZoneData("Texto", "{nombre_proyecto}", "Segoe UI", 9, false, false, "Izquierda"),
        EncabezadoCen: new MatrixCatalogZoneData("Texto", "{fecha_impresion}", "Segoe UI", 11, true, false, "Centro"),
        EncabezadoDer: new MatrixCatalogZoneData("Texto", "", "Segoe UI", 9, false, false, "Derecha"),
        PieIzq: new MatrixCatalogZoneData("Texto", "", "Segoe UI", 9, false, false, "Izquierda"),
        PieCen: new MatrixCatalogZoneData("Texto", "Pagina {pagina} de {total_paginas}", "Segoe UI", 8, false, false, "Centro"),
        PieDer: new MatrixCatalogZoneData("Texto", "{reviso}", "Segoe UI", 9, false, false, "Derecha"),
        CampoElabaro: "LUIS",
        CampoReviso: "MARIA",
        CampoAutorizo: "",
        CampoDependencia: "DIRECCION DE OBRAS",
        CampoNumeroContrato: "CN-2026",
        CampoLicitacion: "LIC-001",
        CampoUbicacion: "Ubicacion plantilla",
        CampoFechaInicio: "01-mar-2026",
        CampoFechaTermino: "31-mar-2026",
        CampoTextoLibre1: "T1",
        CampoTextoLibre2: "T2");

    private static MatrixCatalogSourceComponent Comp(
        MatrixCatalogComponentKind kind, int orden, string clave, string desc, string unidad,
        decimal cantidad, decimal cu, bool esPctMo = false, bool esCuadrillaAux = false)
        => new(kind, orden, clave, desc, unidad, cantidad, cu, esPctMo, esCuadrillaAux);

    private static MatrixCatalogSourceMatrix Matriz(
        string clave, string desc, string unidad, MatrixCatalogMatrixKind kind,
        decimal directCost, params MatrixCatalogSourceComponent[] componentes)
        => new(clave, desc, unidad, kind, directCost, componentes);

    /// <summary>Apuntes del escenario sintético de N0-TABLA (CatalogoMatricesFixtures).</summary>
    private static List<MatrixCatalogSourceMatrix> SnapshotsSinteticos() => new()
    {
        Matriz("APU-001", "Zapata de cimentación", "m3", MatrixCatalogMatrixKind.Apu, 67.026m,
            Comp(MatrixCatalogComponentKind.Material, 1, "MAT-RES-5000", "Resistol blanco", "kg", 3m, 10.005m),
            Comp(MatrixCatalogComponentKind.ManoDeObra, 2, "MO-1", "Oficial albañil", "jor", 1m, 10.005m),
            Comp(MatrixCatalogComponentKind.ManoDeObra, 3, "MO-2", "Ayudante", "jor", 1m, 20.005m),
            Comp(MatrixCatalogComponentKind.ManoDeObra, 4, "MO-3", "Cabo de oficio", "%MO", 0.10m, 0m, esPctMo: true),
            Comp(MatrixCatalogComponentKind.Maquinaria, 5, "MAQ-REV-1", "Revolvedora 1 saco", "hora", 1m, 4.00m)),
        Matriz("APU-002", "Recubrimiento de muros", "m2", MatrixCatalogMatrixKind.Apu, 8.00m,
            Comp(MatrixCatalogComponentKind.Herramienta, 1, "HER-1", "Cuchara de albañil", "pieza", 2m, 3.00m),
            Comp(MatrixCatalogComponentKind.Material, 2, "MAT-2", "Pintura vinílica", "l", 1m, 2.00m)),
        Matriz("BAS-001", "Rendición de limpieza final", "m2", MatrixCatalogMatrixKind.Basico, 30.25m,
            Comp(MatrixCatalogComponentKind.Auxiliar, 1, "CU-001", "Cuadrilla de limpieza", "jornada", 0.5m, 55.00m, esCuadrillaAux: true),
            Comp(MatrixCatalogComponentKind.ManoDeObra, 2, "MO-4", "Limpieza final", "%MO", 0.10m, 0m, esPctMo: true)),
        Matriz("CU-001", "Cuadrilla de limpieza", "jornada", MatrixCatalogMatrixKind.Cuadrilla, 55.00m,
            Comp(MatrixCatalogComponentKind.ManoDeObra, 1, "MO-1", "Oficial albañil", "jor", 1m, 10.005m),
            Comp(MatrixCatalogComponentKind.ManoDeObra, 2, "MO-2", "Ayudante", "jor", 1m, 20.005m),
            Comp(MatrixCatalogComponentKind.ManoDeObra, 3, "MO-3", "Cabo de oficio", "%MO", 0.10m, 0m, esPctMo: true))
    };

    private static MatrixCatalogReportDocument Build(
        IReadOnlyCollection<MatrixCatalogSourceMatrix> snapshots,
        MatrixCatalogReportSettings? settings = null,
        string? filtro = null,
        MatrixCatalogTitleOptions? titulo = null)
    {
        settings ??= new MatrixCatalogReportSettings(ProyectoSintetico(), PlantillaSintetica(), titulo, filtro);
        return new MatrixCatalogReportModelBuilder().Build(settings, snapshots, FechaFija);
    }

    // ─────────────────────────── Builder: aritmética ────────────────────────

    [TestMethod]
    public void Builder_Apu001_AritmeticaCrudaLegacy_SinRedondeo()
    {
        var doc = Build(SnapshotsSinteticos());

        var apu001 = doc.Matrices.Single(m => m.Key == "APU-001");

        Assert.AreEqual(30.01m, apu001.TotalMo, "totalMO crudo = 10.005 + 20.005 (sin %MO).");
        Assert.AreEqual(67.026m, apu001.DirectCost, "Costo directo persistido tal cual (N0-TABLA fila 22).");

        var material = apu001.Components.Single(c => c.Key == "MAT-RES-5000");
        Assert.AreEqual(30.015m, material.Amount, "importe crudo 3 × 10.005 = 30.015 (N0-TABLA fila 23).");
        Assert.AreEqual(10.005m, material.UnitCost);
        Assert.AreEqual("M", material.Prefix);
        Assert.AreEqual(MatrixCatalogComponentKind.Material, material.Kind);

        var pctMo = apu001.Components.Single(c => c.Key == "MO-3");
        Assert.AreEqual(3.001m, pctMo.Amount, "%MO: 30.01 × 0.10 = 3.001.");
        Assert.AreEqual(30.01m, pctMo.UnitCost, "base mostrada para %MO = totalMO.");
        Assert.AreEqual("", pctMo.Prefix);

        var maq = apu001.Components.Single(c => c.Key == "MAQ-REV-1");
        Assert.AreEqual(4.00m, maq.Amount);
        Assert.AreEqual("H", maq.Prefix);
    }

    [TestMethod]
    public void Builder_Bas001_TotalMoIncluyeCuadrillaAuxiliar()
    {
        var doc = Build(SnapshotsSinteticos());
        var bas001 = doc.Matrices.Single(m => m.Key == "BAS-001");

        Assert.AreEqual(27.5m, bas001.TotalMo, "0.5 × 55 (cuadrilla auxiliar) se suma al totalMO.");
        var pctMo = bas001.Components.Single(c => c.Key == "MO-4");
        Assert.AreEqual(2.75m, pctMo.Amount, "%MO: 27.5 × 0.10 = 2.75.");
        Assert.AreEqual(27.5m, pctMo.UnitCost);
    }

    [TestMethod]
    public void Builder_OrdenaMatricesPorClave_IgualConEntradaBarajada()
    {
        var barajadas = new List<MatrixCatalogSourceMatrix>();
        for (int i = 0; i < 10; i++)
        {
            barajadas = SnapshotsSinteticos();
            barajadas.Reverse();
            var doc = Build(barajadas);
            CollectionAssert.AreEqual(
                new[] { "APU-001", "APU-002", "BAS-001", "CU-001" },
                doc.Matrices.Select(m => m.Key).ToArray(),
                $"Iteración {i}: el documento ordena por clave ordinal.");
        }
    }

    [TestMethod]
    public void Builder_ComponentesOrdenadosPorOrden()
    {
        var desordenados = new List<MatrixCatalogSourceMatrix>
        {
            Matriz("APU-001", "Zapata", "m3", MatrixCatalogMatrixKind.Apu, 67.026m,
                Comp(MatrixCatalogComponentKind.Maquinaria, 5, "MAQ", "Revolvedora", "hora", 1m, 4m),
                Comp(MatrixCatalogComponentKind.Material, 1, "MAT", "Resistol", "kg", 3m, 10.005m),
                Comp(MatrixCatalogComponentKind.ManoDeObra, 3, "MO-2", "Ayudante", "jor", 1m, 20.005m))
        };

        var doc = Build(desordenados);
        var apu001 = doc.Matrices.Single();
        Assert.AreEqual("MAT", apu001.Components[0].Key);
        Assert.AreEqual("MO-2", apu001.Components[1].Key);
        Assert.AreEqual("MAQ", apu001.Components[2].Key);
    }

    // ──────────────────────── Builder: inmutabilidad ────────────────────────

    [TestMethod]
    public void Builder_MatricesYComponentesExpuestosDeFormaLecturaSola()
    {
        var doc = Build(SnapshotsSinteticos());

        var matrices = (IList)doc.Matrices;
        Assert.IsTrue(matrices.IsReadOnly);
        Assert.ThrowsException<NotSupportedException>(() => matrices.Add(doc.Matrices[0]));

        var componentes = (IList)doc.Matrices[0].Components;
        Assert.IsTrue(componentes.IsReadOnly);
        Assert.ThrowsException<NotSupportedException>(() => componentes.Add(null));
    }

    [TestMethod]
    public void Builder_NoMutaLaEntradaNiRetieneListasMutables()
    {
        var snapshots = SnapshotsSinteticos();
        var antes = snapshots.Select(m => m.Clave).ToArray();

        var doc = Build(snapshots);

        // Mutar la colección de entrada después de Build no afecta al documento.
        snapshots.Add(Matriz("ZZZ", "Afecta solo a la lista", "m2", MatrixCatalogMatrixKind.Apu, 1m));
        Assert.AreEqual(4, doc.Matrices.Count);
        CollectionAssert.AreEqual(antes, snapshots.Take(4).Select(m => m.Clave).ToArray());
    }

    [TestMethod]
    public void Builder_ValidacionDeNulos()
    {
        var builder = new MatrixCatalogReportModelBuilder();

        Assert.ThrowsException<ArgumentNullException>(() =>
            builder.Build(null!, SnapshotsSinteticos(), FechaFija));
        Assert.ThrowsException<ArgumentNullException>(() =>
            builder.Build(new MatrixCatalogReportSettings(ProyectoSintetico(), null, null, null), null!, FechaFija));
    }

    // ──────────────────────── Builder: título y estilos ─────────────────────

    [TestMethod]
    public void Builder_TituloSegunFiltro_YEstilosPorDefecto()
    {
        var cfg = new MatrixCatalogReportSettings(ProyectoSintetico(), PlantillaSintetica(), null, null);
        var docDefault = new MatrixCatalogReportModelBuilder().Build(cfg, SnapshotsSinteticos(), FechaFija);

        Assert.AreEqual("CATÁLOGO DE MATRICES", docDefault.Title);
        Assert.AreEqual("Segoe UI", docDefault.TitleStyle.FontName);
        Assert.AreEqual(14d, docDefault.TitleStyle.Size);
        Assert.IsTrue(docDefault.TitleStyle.Bold);
        Assert.AreEqual("#FFFFFF", docDefault.TitleStyle.TextColorHex);
        Assert.AreEqual("APU SINTETICO GOLDEN N7-18A", docDefault.ProjectName);

        Assert.AreEqual("CATÁLOGO DE MATRICES (APU)", Build(SnapshotsSinteticos(), filtro: "APU").Title);
        Assert.AreEqual("CATÁLOGO DE MATRICES (BÁSICOS)", Build(SnapshotsSinteticos(), filtro: "Básicos").Title);
        Assert.AreEqual("CATÁLOGO DE MATRICES (CUADRILLAS)", Build(SnapshotsSinteticos(), filtro: "Cuadrillas").Title);
        Assert.AreEqual("CATÁLOGO DE MATRICES", Build(SnapshotsSinteticos(), filtro: "Todos").Title);
    }

    [TestMethod]
    public void Builder_OpcionesDeTituloPersonalizadas()
    {
        var titulo = new MatrixCatalogTitleOptions(
            "CATÁLOGO PERSONALIZADO", "Arial", 11, false, true, "#112233");

        var doc = Build(SnapshotsSinteticos(), filtro: "APU", titulo: titulo);

        Assert.AreEqual("CATÁLOGO PERSONALIZADO (APU)", doc.Title);
        Assert.AreEqual("Arial", doc.TitleStyle.FontName);
        Assert.AreEqual(11d, doc.TitleStyle.Size);
        Assert.IsFalse(doc.TitleStyle.Bold);
        Assert.IsTrue(doc.TitleStyle.Italic);
        Assert.AreEqual("#112233", doc.TitleStyle.TextColorHex);
    }

    // ──────────────────────── Builder: tokens y reloj ───────────────────────

    [TestMethod]
    public void Builder_ResuelveTokensEnEncabezadoY_DejaPaginaAlMedio()
    {
        var doc = Build(SnapshotsSinteticos());

        Assert.AreEqual("APU SINTETICO GOLDEN N7-18A", doc.Header.Left.Content);
        Assert.AreEqual("27/08/2026 13:45", doc.Header.Center.Content, "reloj explícito en {fecha_impresion}.");
        Assert.AreEqual("Pagina {pagina} de {total_paginas}", doc.Footer.Center.Content, "{pagina} queda para el medio.");
        Assert.AreEqual("MARIA", doc.Footer.Right.Content, "{reviso} se resuelve desde la plantilla.");
        Assert.AreEqual(MatrixCatalogZoneKind.Texto, doc.Header.Left.Kind);
        Assert.AreEqual(MatrixCatalogTextAlignment.Centro, doc.Header.Center.Style.Alignment);
        Assert.AreEqual(11d, doc.Header.Center.Style.Size);
        Assert.IsTrue(doc.Header.Center.Style.Bold);
    }

    [TestMethod]
    public void Builder_ResolucionInvarianteBajoCualquierCultura()
    {
        var culturaOriginal = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var plantilla = PlantillaSintetica() with
            {
                EncabezadoIzq = new MatrixCatalogZoneData("Texto", "{fecha_inicio} {fecha_termino} {plazo_ejecucion}", "Segoe UI", 9, false, false, "Izquierda"),
            };
            var settings = new MatrixCatalogReportSettings(ProyectoSintetico(), plantilla, null, null);

            var doc = new MatrixCatalogReportModelBuilder().Build(settings, SnapshotsSinteticos(), FechaFija);

            // Con una cultura que usa "dd.MM.yyyy", el modelo sigue resolviendo en
            // formato invariante "dd/MM/yyyy" (independiente de la máquina).
            Assert.AreEqual("01/03/2026 31/03/2026 31", doc.Header.Left.Content);
            Assert.AreEqual("27/08/2026 13:45", doc.Header.Center.Content);
        }
        finally
        {
            CultureInfo.CurrentCulture = culturaOriginal;
        }
    }

    // ─────────────────────────── Proyección pura ────────────────────────────

    [TestMethod]
    public void Proyeccion_Material_CuYUnidadDelInsumo()
    {
        var material = new Material { Clave = "MAT-RES-5000", Descripcion = "Resistol blanco", Unidad = "kg", PrecioUnitario = 10.005m, Notas = string.Empty };
        var comp = new ComponenteMatriz { MatrizId = 1, TipoComponente = TipoComponenteMatriz.Material, Material = material, Cantidad = 3m, Orden = 1 };

        var snap = MatrixCatalogSourceMapper.MapearComponente(comp);

        Assert.AreEqual(MatrixCatalogComponentKind.Material, snap.Kind);
        Assert.AreEqual("MAT-RES-5000", snap.Clave);
        Assert.AreEqual("kg", snap.Unidad);
        Assert.AreEqual(10.005m, snap.CostoUnitario);
        Assert.IsFalse(snap.EsPorcentajeMo);
        Assert.IsFalse(snap.EsCuadrillaAuxiliar);
    }

    [TestMethod]
    public void Proyeccion_ManoDeObraPorcentajeMO_UnidadJorYSalarioReal()
    {
        var mo = new ManoDeObra { Clave = "MO-3", Descripcion = "Cabo de oficio", Unidad = "%MO", SalarioBase = 0m, SalarioReal = 10.005m, Notas = string.Empty };
        var comp = new ComponenteMatriz { MatrizId = 1, TipoComponente = TipoComponenteMatriz.ManoDeObra, ManoDeObra = mo, Cantidad = 0.10m, Orden = 4 };

        var snap = MatrixCatalogSourceMapper.MapearComponente(comp);

        Assert.AreEqual(MatrixCatalogComponentKind.ManoDeObra, snap.Kind);
        Assert.IsTrue(snap.EsPorcentajeMo);
        Assert.AreEqual("jor", snap.Unidad);
        Assert.AreEqual(10.005m, snap.CostoUnitario, "SalarioReal es el costo unitario de la proyección.");
    }

    [TestMethod]
    public void Proyeccion_AuxiliarCuadrilla_MarcaBaseDeTotalMo()
    {
        var cu = new Matriz { Clave = "CU-001", Descripcion = "Cuadrilla", Unidad = "jornada", Tipo = TipoMatriz.Cuadrilla, CostoDirecto = 55m, Notas = string.Empty };
        var comp = new ComponenteMatriz { MatrizId = 1, TipoComponente = TipoComponenteMatriz.Auxiliar, Auxiliar = cu, Cantidad = 0.5m, Orden = 1 };

        var snap = MatrixCatalogSourceMapper.MapearComponente(comp);

        Assert.AreEqual(MatrixCatalogComponentKind.Auxiliar, snap.Kind);
        Assert.IsTrue(snap.EsCuadrillaAuxiliar);
        Assert.AreEqual(55m, snap.CostoUnitario);

        var bas = new Matriz { Clave = "BAS-X", Descripcion = "Basico", Unidad = "m2", Tipo = TipoMatriz.Basico, CostoDirecto = 3m, Notas = string.Empty };
        var noCuadrilla = MatrixCatalogSourceMapper.MapearComponente(
            new ComponenteMatriz { MatrizId = 1, TipoComponente = TipoComponenteMatriz.Auxiliar, Auxiliar = bas, Cantidad = 1m });
        Assert.IsFalse(noCuadrilla.EsCuadrillaAuxiliar);
    }

    [TestMethod]
    public void Proyeccion_NavegacionesNulas_ValoresVaciosYNeutrales()
    {
        var snap = MatrixCatalogSourceMapper.MapearComponente(
            new ComponenteMatriz { MatrizId = 1, TipoComponente = TipoComponenteMatriz.Material, Material = null, Orden = 2 });

        Assert.AreEqual("", snap.Clave);
        Assert.AreEqual("", snap.Descripcion);
        Assert.AreEqual("", snap.Unidad);
        Assert.AreEqual(0m, snap.CostoUnitario);
        Assert.IsFalse(snap.EsPorcentajeMo);
    }

    [TestMethod]
    public void Proyeccion_Matriz_OrdenaComponentesPorOrden()
    {
        var matriz = new Matriz { Clave = "APU-001", Descripcion = "Zapata", Unidad = "m3", Tipo = TipoMatriz.APU, CostoDirecto = 67.026m, Notas = string.Empty };
        matriz.Componentes.Add(new ComponenteMatriz { MatrizId = 1, TipoComponente = TipoComponenteMatriz.Maquinaria, Orden = 3, Cantidad = 1m, Maquinaria = new Maquinaria { Clave = "MAQ", CostoHorario = 4m } });
        matriz.Componentes.Add(new ComponenteMatriz { MatrizId = 1, TipoComponente = TipoComponenteMatriz.Material, Orden = 1, Cantidad = 3m, Material = new Material { Clave = "MAT", PrecioUnitario = 10.005m } });
        matriz.Componentes.Add(new ComponenteMatriz { MatrizId = 1, TipoComponente = TipoComponenteMatriz.ManoDeObra, Orden = 2, Cantidad = 1m, ManoDeObra = new ManoDeObra { Clave = "MO", SalarioReal = 20.005m } });

        var snap = MatrixCatalogSourceMapper.MapearMatriz(matriz);

        Assert.AreEqual("APU-001", snap.Clave);
        Assert.AreEqual(MatrixCatalogMatrixKind.Apu, snap.Kind);
        Assert.AreEqual(67.026m, snap.CostoDirecto);
        CollectionAssert.AreEqual(new[] { "MAT", "MO", "MAQ" }, snap.Componentes.Select(c => c.Clave).ToArray());
    }

    [TestMethod]
    public void Proyeccion_ProyectoYPlantillaNulos_ModelosVacios()
    {
        var proyecto = MatrixCatalogSourceMapper.MapearProyecto(null);
        Assert.AreEqual("", proyecto.Nombre);

        var plantilla = MatrixCatalogSourceMapper.MapearPlantilla(null);
        Assert.AreEqual("", plantilla.EncabezadoCen.Contenido);
        Assert.AreEqual("", plantilla.CampoElabaro);
    }

    // ─────────────────── Integración: proyecto real (SQLite) ────────────────

    [TestMethod]
    public void Execute_***REMOVED***_Las7MatricesConAritmeticaCruda()
    {
        using var copia = new Copia***REMOVED***();
        var session = LeerSessionReal(copia.DbPath);
        var factory = new ProjectDbContextFactory();
        var useCase = new BuildMatrixCatalogReport(factory);

        var ids = LeerIdsMatrices(copia.DbPath);

        var result = useCase.Execute(session, new BuildMatrixCatalogReportRequest(ids), CancellationToken.None).GetAwaiter().GetResult();

        Assert.IsTrue(result.IsSuccess, result.Error?.Message ?? "sin mensaje");
        var doc = result.Value!;

        if (doc.Matrices.Count != 7)
            Assert.Fail("matrices: " + string.Join("|", doc.Matrices.Select(m => m.Key)));

        Assert.AreEqual("CATÁLOGO DE MATRICES", doc.Title);
        CollectionAssert.AreEqual(
            new[] { "CU001.", "M-1", "M-2", "M-3", "M-4", "M-5", "M-6" },
            doc.Matrices.Select(m => m.Key).ToArray());

        var cu001 = doc.Matrices.Single(m => m.Key == "CU001.");
        Assert.AreEqual(***REMOVED***m, cu001.TotalMo, "totalMO crudo de la cuadrilla (SalarioReal de MO002).");

        var m1 = doc.Matrices.Single(m => m.Key == "M-1");
        var retro = m1.Components.Single(c => c.Key == "RETRO235");
        Assert.AreEqual(MatrixCatalogComponentKind.Maquinaria, retro.Kind);
        Assert.AreEqual("H", retro.Prefix);
        Assert.AreEqual(2202.65m, retro.UnitCost, "CostoHorario de RETRO235.");
        Assert.AreEqual(***REMOVED***m, retro.Amount, "importe crudo 5.47884 × 2202.65.");
    }

    [TestMethod]
    public void Execute_***REMOVED***_IdsDuplicadosEInexistentes_ComportamientoLegacy()
    {
        using var copia = new Copia***REMOVED***();
        var session = LeerSessionReal(copia.DbPath);
        var useCase = new BuildMatrixCatalogReport(new ProjectDbContextFactory());

        var ids = LeerIdsMatrices(copia.DbPath);
        var cuId = ids[0];

        // Duplicado del mismo id + uno inexistente → WHERE IN deduplica e ignora.
        var result = useCase.Execute(session, new BuildMatrixCatalogReportRequest(new[] { cuId, cuId, 99999999 }), CancellationToken.None).GetAwaiter().GetResult();

        Assert.IsTrue(result.IsSuccess, result.Error?.Message ?? "sin mensaje");
        Assert.AreEqual(1, result.Value!.Matrices.Count);
        Assert.AreEqual("CU001.", result.Value!.Matrices[0].Key);
    }

    [TestMethod]
    public void Execute_IdsVacios_DocumentoSinMatricesPeroConTitulo()
    {
        using var copia = new Copia***REMOVED***();
        var session = LeerSessionReal(copia.DbPath);
        var useCase = new BuildMatrixCatalogReport(new ProjectDbContextFactory());

        var result = useCase.Execute(session, new BuildMatrixCatalogReportRequest(Array.Empty<int>()), CancellationToken.None).GetAwaiter().GetResult();

        Assert.IsTrue(result.IsSuccess, result.Error?.Message ?? "sin mensaje");
        Assert.AreEqual(0, result.Value!.Matrices.Count);
        Assert.AreEqual("CATÁLOGO DE MATRICES", result.Value!.Title);
    }

    [TestMethod]
    public void Execute_DosProyectos_MatrizDelOtroProyectoNoAparece()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"sopro_mc_2p_{Guid.NewGuid():N}.db");
        try
        {
            int idA;
            int idB;
            using (var ctx = TestDbFactory.CreateContextAt(dbPath))
            {
                var pA = CrearProyectoBasico("Proyecto A");
                var pB = CrearProyectoBasico("Proyecto B");
                ctx.Proyectos.AddRange(pA, pB);
                ctx.SaveChanges();
                ctx.Matrices.AddRange(
                    new Matriz { ProyectoId = pA.Id, Clave = "A-1", Descripcion = "Matriz de A", Unidad = "pza", CostoDirecto = 10m },
                    new Matriz { ProyectoId = pB.Id, Clave = "B-1", Descripcion = "Matriz de B", Unidad = "pza", CostoDirecto = 20m });
                ctx.SaveChanges();
                idA = pA.Id;
                idB = pB.Id;
            }

            var sessionA = CrearSession(dbPath, idA);
            var useCase = new BuildMatrixCatalogReport(new ProjectDbContextFactory());

            // La matriz de B, pedida con la sesión activa de A, NO aparece: el WHERE
            // exige m.ProyectoId == sesión (dictamen NO-GO: aislamiento por proyecto).
            var ajeno = useCase.Execute(sessionA, new BuildMatrixCatalogReportRequest(new[] { idB }), CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(ajeno.IsSuccess, ajeno.Error?.Message ?? "sin mensaje");
            Assert.AreEqual(0, ajeno.Value!.Matrices.Count, "una matriz de otro ProyectoId nunca se reporta.");

            // La matriz del propio proyecto sí aparece.
            var propio = useCase.Execute(sessionA, new BuildMatrixCatalogReportRequest(new[] { idA }), CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(propio.IsSuccess, propio.Error?.Message ?? "sin mensaje");
            Assert.AreEqual(1, propio.Value!.Matrices.Count);
            Assert.AreEqual("A-1", propio.Value!.Matrices[0].Key);
            Assert.AreEqual("Proyecto A", propio.Value!.ProjectName);
        }
        finally
        {
            try { File.Delete(dbPath); } catch { /* best effort */ }
        }
    }

    [TestMethod]
    public void Execute_RelojFijo_ProduceDocumentoDeterministaDesdeElCasoDeUso()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"sopro_mc_clock_{Guid.NewGuid():N}.db");
        try
        {
            int idProyecto;
            int idMatriz;
            using (var ctx = TestDbFactory.CreateContextAt(dbPath))
            {
                var p = CrearProyectoBasico("Proyecto Reloj");
                ctx.Proyectos.Add(p);
                ctx.SaveChanges();
                ctx.Matrices.Add(new Matriz { ProyectoId = p.Id, Clave = "M-1", Descripcion = "Matriz", Unidad = "pza", CostoDirecto = 5m });
                ctx.PlantillasReporte.Add(new PlantillaReporte
                {
                    ProyectoId = p.Id,
                    EncabezadoCenContenido = "{fecha_impresion}",
                    EncabezadoIzqContenido = "{nombre_proyecto}",
                });
                ctx.SaveChanges();
                idProyecto = p.Id;
                idMatriz = ctx.Matrices.Single().Id;
            }

            var session = CrearSession(dbPath, idProyecto);
            var useCase = new BuildMatrixCatalogReport(new ProjectDbContextFactory(), new FixedTimeProvider(FechaFija));
            var request = new BuildMatrixCatalogReportRequest(new[] { idMatriz });

            // El reloj NO es DateTime.Now oculto: se inyecta en Execute y dos llamadas
            // con el mismo reloj fijo producen exactamente el mismo documento.
            var r1 = useCase.Execute(session, request, CancellationToken.None).GetAwaiter().GetResult();
            var r2 = useCase.Execute(session, request, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(r1.IsSuccess, r1.Error?.Message ?? "sin mensaje");
            Assert.AreEqual("27/08/2026 13:45", r1.Value!.Header.Center.Content, "{fecha_impresion} se resuelve con el TimeProvider inyectado.");
            Assert.AreEqual("Proyecto Reloj", r1.Value!.ProjectName);
            Assert.AreEqual(r1.Value!.Matrices.Count, r2.Value!.Matrices.Count);
            Assert.AreEqual(r1.Value!.Header.Center.Content, r2.Value!.Header.Center.Content);
        }
        finally
        {
            try { File.Delete(dbPath); } catch { /* best effort */ }
        }
    }

    // ─────────────────────── Request: inputs inmutables ──────────────────────

    [TestMethod]
    public void Request_MatrixIdsCopiaDefensiva_NoRetieneLaListaMutable()
    {
        var lista = new List<int> { 1, 2, 3 };
        var request = new BuildMatrixCatalogReportRequest(lista);

        // Mutar la lista original después de construir la solicitud no la altera.
        lista.Add(4);
        lista[0] = 99;

        Assert.AreEqual(3, request.MatrixIds.Count);
        Assert.AreEqual(1, request.MatrixIds[0]);
        Assert.IsTrue(((ICollection<int>)request.MatrixIds).IsReadOnly, "la copia se materializa como array de solo lectura.");
        Assert.ThrowsException<NotSupportedException>(() => ((ICollection<int>)request.MatrixIds).Add(5));
        Assert.ThrowsException<InvalidCastException>(() => (List<int>)request.MatrixIds);
        Assert.ThrowsException<ArgumentNullException>(() => new BuildMatrixCatalogReportRequest(null!));
    }

    // ─────────────────────────── Infraestructura ────────────────────────────

    private sealed class Copia***REMOVED*** : IDisposable
    {
        private const string DbFileName = "***REMOVED***.db";

        public Copia***REMOVED***()
        {
            var sourcePath = Path.Combine(AppContext.BaseDirectory, "TestData", DbFileName);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"BD real no encontrada: {sourcePath}", sourcePath);

            DbPath = Path.Combine(Path.GetTempPath(), $"sopro_matrixcatalog_real_{Guid.NewGuid():N}.db");
            File.Copy(sourcePath, DbPath, overwrite: true);
            // Copia inmutable, igual que ***REMOVED***.
            File.SetAttributes(DbPath, FileAttributes.ReadOnly);
        }

        public string DbPath { get; }

        public void Dispose()
        {
            try { File.SetAttributes(DbPath, FileAttributes.Normal); } catch { /* best effort */ }
            try { File.Delete(DbPath); } catch { /* best effort */ }
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _fixedUtc;

        public FixedTimeProvider(DateTime utc)
            => _fixedUtc = new DateTimeOffset(utc, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => _fixedUtc;
    }

    /// <summary>Crea un Proyecto mínimo con todos los campos NOT NULL de la tabla.</summary>
    private static Proyecto CrearProyectoBasico(string nombre) => new()
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

    private static ProjectSessionInfo CrearSession(string dbPath, int proyectoId)
    {
        using var lectura = new SOPROContext(dbPath);
        var proyecto = lectura.Proyectos.AsNoTracking().First(p => p.Id == proyectoId);
        return ProjectSessionInfo.Create(ProjectRef.FromEntity(proyecto), dbPath, null, proyecto.DecimalesImporte);
    }

    private static ProjectSessionInfo LeerSessionReal(string dbPath)
    {
        using var lectura = new SOPROContext(dbPath);
        var proyecto = lectura.Proyectos.AsNoTracking().First();
        return ProjectSessionInfo.Create(ProjectRef.FromEntity(proyecto), dbPath, null, proyecto.DecimalesImporte);
    }

    private static List<int> LeerIdsMatrices(string dbPath)
    {
        using var lectura = new SOPROContext(dbPath);
        return lectura.Matrices.AsNoTracking().OrderBy(m => m.Clave).Select(m => m.Id).ToList();
    }
}