using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Fixture del proyecto sintético de regresión (100% sintético). Se hace una copia temporal
/// escribible de la BD sintética ("PROYECTO SINTETICO VIAL DEMO") para que el generador la lea
/// igual que en producción. La plantilla no contiene {fecha_impresion}, por lo que su salida
/// es determinista; se valida con una comprobación explícita.
/// </summary>
internal sealed class ProyectoSinteticoCatalogoFixture : IDisposable
{
    public ProyectoSinteticoCatalogoFixture()
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "TestData",
            "proyecto-sintetico-vial-demo.db");
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"BD sintética no encontrada: {sourcePath}", sourcePath);

        DbPath = Path.Combine(Path.GetTempPath(), $"sopro_sintetico_catalogo_{Guid.NewGuid():N}.db");
        File.Copy(sourcePath, DbPath, overwrite: true);

        Context = new SOPROContext(DbPath);
        Plantilla = Context.PlantillasReporte.FirstOrDefault()
            ?? throw new InvalidOperationException("El proyecto sintético no tiene plantilla de reporte.");
        Proyecto = Context.Proyectos.First()
            ?? throw new InvalidOperationException("El proyecto sintético no tiene proyecto.");
        Matrices = Context.Matrices.AsNoTracking().ToList();
    }

    public SOPROContext Context { get; }
    public Proyecto Proyecto { get; }
    public List<Matriz> Matrices { get; }
    public PlantillaReporte Plantilla { get; }
    public string DbPath { get; }

    /// <summary>
    /// La plantilla no debe resolver {fecha_impresion}; si un día la trae, el golden
    /// dejaría de ser determinista y el fixture debe fallar en lugar de enmascarar la fecha.
    /// </summary>
    public void AssertPlantillaSinFechaImpresion()
    {
        string joined = string.Join("|",
            Plantilla.EncabezadoIzqContenido ?? "", Plantilla.EncabezadoCenContenido ?? "",
            Plantilla.EncabezadoDerContenido ?? "", Plantilla.PiePaginaIzqContenido ?? "",
            Plantilla.PiePaginaCenContenido ?? "", Plantilla.PiePaginaDerContenido ?? "");
        if (joined.Contains("{fecha_impresion}", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "La plantilla del proyecto sintético usa {fecha_impresion}; el golden deja de ser determinista.");
    }

    public void Dispose()
    {
        Context.Dispose();
        TryDelete(DbPath);
    }

    internal static void TryDelete(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
        catch { /* best effort, misma política que las regresiones reales */ }
    }
}

/// <summary>
/// Escenario sintético discriminante para el catálogo de matrices.
///
/// Precios y salarios se siembran intencionalmente con más de 2 decimales (10.005, 20.005)
/// para exponer la aritmética cruda de filas 22-23 de N0-TABLA: el generador multiplica
/// Cantidad × costoUnitario SIN redondear (GeneradorExcelCatalogoMatrices.cs:258-264) y
/// calcula totalMO sin redondeo (:186-196). El golden debe distinguir el VALOR ALMACENADO
/// (p.ej. 30.015) del FORMATO Excel (#,##0.00) que muestran las celdas.
///
/// 3 matrices + 1 cuadrilla auxiliar:
///   APU-001 (m3,  CD 67.026 crudo):  M 3×10.005=30.015 | MO 1×10.005 | MO 1×20.005
///                                    | %MO .1×30.01=3.001 | MAQ 1×4        (totalMO=30.01)
///   APU-002 (m2,  CD 8):             HER 2×3=6 | M 1×2=2
///   BAS-001 (m2,  CD 30.25 crudo):   AUX cuadrilla .5×55=27.5 | %MO .1×27.5=2.75
///   CU-001 (jornada, CD 55):         cuadrilla auxiliar (MO 10.005 + MO 20.005 + %MO .1×30.01)
///
/// Notas: no se recalcula con el motor canónico a propósito: el fixture congelará la salida
/// del generador legacy tal cual, contrastándola después con el cálculo neutral en N7-18c.
/// </summary>
internal sealed class SinteticoCatalogoFixture : IDisposable
{
    public SinteticoCatalogoFixture()
    {
        DbPath = Path.Combine(Path.GetTempPath(), $"sopro_sintetico_catalogo_{Guid.NewGuid():N}.db");
        Context = new SOPROContext(DbPath);
        Context.Database.EnsureDeleted();
        Context.Database.EnsureCreated();

        Proyecto = new Proyecto
        {
            Nombre = "APU SINTETICO GOLDEN N7-18A",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 3, 1),
            FechaTermino = new DateTime(2026, 3, 31),
            PlazoEjecucion = 31,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
        };
        Context.Proyectos.Add(Proyecto);
        Context.SaveChanges();

        var matRes = new Material { ProyectoId = Proyecto.Id, Clave = "MAT-RES-5000", Descripcion = "Resistol blanco", Unidad = "kg", PrecioUnitario = 10.005m, Notas = string.Empty };
        var mat2 = new Material { ProyectoId = Proyecto.Id, Clave = "MAT-2", Descripcion = "Pintura vinílica", Unidad = "l", PrecioUnitario = 2.00m, Notas = string.Empty };
        var mo1 = new ManoDeObra { ProyectoId = Proyecto.Id, Clave = "MO-1", Descripcion = "Oficial albañil", Unidad = "jor", SalarioBase = 10.005m, SalarioReal = 10.005m, Notas = string.Empty };
        var mo2 = new ManoDeObra { ProyectoId = Proyecto.Id, Clave = "MO-2", Descripcion = "Ayudante", Unidad = "jor", SalarioBase = 20.005m, SalarioReal = 20.005m, Notas = string.Empty };
        var moPct1 = new ManoDeObra { ProyectoId = Proyecto.Id, Clave = "MO-3", Descripcion = "Cabo de oficio", Unidad = "%MO", SalarioBase = 0m, SalarioReal = 0m, Notas = string.Empty };
        var moPct2 = new ManoDeObra { ProyectoId = Proyecto.Id, Clave = "MO-4", Descripcion = "Limpieza final", Unidad = "%MO", SalarioBase = 0m, SalarioReal = 0m, Notas = string.Empty };
        var maq = new Maquinaria { ProyectoId = Proyecto.Id, Clave = "MAQ-REV-1", Descripcion = "Revolvedora 1 saco", CostoHorario = 4.00m, Notas = string.Empty };
        var her = new Herramienta { ProyectoId = Proyecto.Id, Clave = "HER-1", Descripcion = "Cuchara de albañil", Unidad = "pieza", PrecioUnitario = 3.00m, Notas = string.Empty };

        Context.Materiales.AddRange(matRes, mat2);
        Context.ManoDeObra.AddRange(mo1, mo2, moPct1, moPct2);
        Context.Maquinaria.Add(maq);
        Context.Herramientas.Add(her);
        Context.SaveChanges();

        var apu001 = New("APU-001", "Zapata de cimentación", "m3", TipoMatriz.APU, 67.026m);
        var apu002 = New("APU-002", "Recubrimiento de muros", "m2", TipoMatriz.APU, 8.00m);
        var bas001 = New("BAS-001", "Rendición de limpieza final", "m2", TipoMatriz.Basico, 30.25m);
        var cu001 = New("CU-001", "Cuadrilla de limpieza", "jornada", TipoMatriz.Cuadrilla, 55.00m);

        Comp(apu001, matRes, 3m, 1);
        Comp(apu001, mo1, 1m, 2);
        Comp(apu001, mo2, 1m, 3);
        Comp(apu001, moPct1, 0.10m, 4);
        Comp(apu001, maq, 1m, 5);

        Comp(apu002, her, 2m, 1);
        Comp(apu002, mat2, 1m, 2);

        Comp(bas001, cu001, 0.5m, 1);
        Comp(bas001, moPct2, 0.10m, 2);

        Comp(cu001, mo1, 1m, 1);
        Comp(cu001, mo2, 1m, 2);
        Comp(cu001, moPct1, 0.10m, 3);

        // Plantilla determinista: centro = {nombre_proyecto}, derecha e izquierda vacías,
        // sin {fecha_impresion} (rompería el golden). Pie central con campo de página.
        Plantilla = new PlantillaReporte
        {
            ProyectoId = Proyecto.Id,
            EncabezadoIzqContenido = "",
            EncabezadoCenContenido = "{nombre_proyecto}",
            EncabezadoDerContenido = "",
            EncabezadoIzqTipo = "Texto",
            EncabezadoCenTipo = "Texto",
            EncabezadoDerTipo = "Texto",
            PiePaginaIzqContenido = "",
            PiePaginaCenContenido = "Página {pagina} de {total_paginas}",
            PiePaginaDerContenido = "",
            PiePaginaIzqTipo = "Texto",
            PiePaginaCenTipo = "Texto",
            PiePaginaDerTipo = "Texto",
            FechaModificacion = DateTime.Now,
        };
        Context.PlantillasReporte.Add(Plantilla);
        Context.SaveChanges();

        // Se entregan las matrices "barajadas" para que el golden demuestre que la hoja
        // queda ordenada por Clave (GeneradorExcelCatalogoMatrices.cs:67).
        Matrices = Context.Matrices.ToList();
        Shuffle(Matrices);
    }

    public SOPROContext Context { get; }
    public Proyecto Proyecto { get; }
    public List<Matriz> Matrices { get; }
    public PlantillaReporte Plantilla { get; }
    public string DbPath { get; }

    private Matriz New(string clave, string desc, string unidad, TipoMatriz tipo, decimal cd)
    {
        var m = new Matriz
        {
            ProyectoId = Proyecto.Id,
            Clave = clave,
            Descripcion = desc,
            Unidad = unidad,
            Tipo = tipo,
            CostoDirecto = cd,
            Notas = string.Empty,
        };
        Context.Matrices.Add(m);
        Context.SaveChanges();
        return m;
    }

    private void Comp(Matriz matriz, Material insumo, decimal cantidad, int orden)
        => Comp(matriz, TipoComponenteMatriz.Material, insumo.Id, insumo, cantidad, orden);

    private void Comp(Matriz matriz, ManoDeObra insumo, decimal cantidad, int orden)
        => Comp(matriz, TipoComponenteMatriz.ManoDeObra, insumo.Id, insumo, cantidad, orden);

    private void Comp(Matriz matriz, Maquinaria insumo, decimal cantidad, int orden)
        => Comp(matriz, TipoComponenteMatriz.Maquinaria, insumo.Id, insumo, cantidad, orden);

    private void Comp(Matriz matriz, Herramienta insumo, decimal cantidad, int orden)
        => Comp(matriz, TipoComponenteMatriz.Herramienta, insumo.Id, insumo, cantidad, orden);

    private void Comp(Matriz matriz, Matriz auxiliar, decimal cantidad, int orden)
        => Comp(matriz, TipoComponenteMatriz.Auxiliar, auxiliar.Id, auxiliar, cantidad, orden);

    private void Comp(Matriz matriz, TipoComponenteMatriz tipo, int? insumoId, object nav, decimal cantidad, int orden)
    {
        var comp = new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            Matriz = matriz,
            TipoComponente = tipo,
            Cantidad = cantidad,
            Orden = orden,
            Notas = string.Empty,
        };
        switch (tipo)
        {
            case TipoComponenteMatriz.Material: comp.MaterialId = insumoId; comp.Material = (Material)nav; break;
            case TipoComponenteMatriz.ManoDeObra: comp.ManoDeObraId = insumoId; comp.ManoDeObra = (ManoDeObra)nav; break;
            case TipoComponenteMatriz.Maquinaria: comp.MaquinariaId = insumoId; comp.Maquinaria = (Maquinaria)nav; break;
            case TipoComponenteMatriz.Herramienta: comp.HerramientaId = insumoId; comp.Herramienta = (Herramienta)nav; break;
            case TipoComponenteMatriz.Auxiliar: comp.AuxiliarId = insumoId; comp.Auxiliar = (Matriz)nav; break;
        }
        Context.ComponentesMatriz.Add(comp);
        Context.SaveChanges();
    }

    private static void Shuffle<T>(IList<T> list)
    {
        var rnd = new Random(12345);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public void Dispose()
    {
        Context.Dispose();
        ProyectoSinteticoCatalogoFixture.TryDelete(DbPath);
    }
}