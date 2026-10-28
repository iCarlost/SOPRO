using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Tests.WinForms.TestInfrastructure;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Performance;

/// <summary>
/// Paso 7.1 — RENDIMIENTO del REPAINT por celda. Construye un grid de catálogo
/// headless con 5.000 filas y ejerce el MISMO camino de pintado que la UI
/// (<see cref="CatalogoFormatoCeldas.AplicarOverride"/>, invocado por
/// <c>DataGridView.CellFormatting</c>): resolución fila×columna a una clave ESTABLE
/// y consulta del diccionario EN MEMORIA. Falla si el repaint degrada de forma
/// grossera (p. ej. si introdujera una consulta a la base de datos por celda).
/// </summary>
[TestClass]
public class FormatoCeldasRepaintPerformanceTests
{
    private const string MaterialesTipo = "Materiales";
    private const string Vista = "";
    private const int FilasGrid = 5000;
    private const int UmbralRepaintMs = 2000;

    public TestContext TestContext { get; set; } = null!;

    private static SOPROContext CrearContexto()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_perf_repaint_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static int AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto repaint",
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
            DecimalesPorcentaje = 4,
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto.Id;
    }

    private static DataGridView CrearGrid(int filas)
    {
        var grid = new DataGridView { AutoGenerateColumns = false, AllowUserToAddRows = false };
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "col_Clave",
            DataPropertyName = "Clave",
            Tag = new ColumnaMaterial { NombreInterno = "Clave" },
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "col_PrecioUnitario",
            DataPropertyName = "PrecioUnitario",
            Tag = new ColumnaMaterial { NombreInterno = "PrecioUnitario" },
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "col_Descripcion",
            DataPropertyName = "Descripcion",
            Tag = new ColumnaMaterial { NombreInterno = "Descripcion" },
        });

        var datos = Enumerable.Range(1, filas)
            .Select(i => new MaterialListItem(i, $"MAT-{i}", $"Material {i}", "kg", 10m + i, string.Empty, OrigenInsumo.Proyecto))
            .ToList();

        // Sin BindingContext el grid headless no materializa las filas (DataBoundItem
        // sería null). Con él, el binding es idéntico al de la UI.
        grid.BindingContext = new BindingContext();
        grid.DataSource = datos;
        return grid;
    }

    [TestMethod]
    public void Repaint_5000Filas_conOverrides_LeeSoloDiccionarioEnMemoria()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        // Se formatea la columna PrecioUnitario de TODAS las filas: peor caso del
        // camino de acierto (hit del diccionario) en el repaint.
        var lote = Enumerable.Range(1, FilasGrid)
            .Select(i => new CambioFormatoCeldaCommand(
                Vista,
                ClaveFilaReporte.Insumo(i),
                "PrecioUnitario",
                ExpectedOverlay: null,
                ProposedOverlay: new ReportStyleOverlay(ColorFondo: "#112233", Negrita: true)))
            .ToList();
        FormatoTestWriter.Aplicar(ctx, proyectoId, MaterialesTipo, lote);

        using var grid = CrearGrid(FilasGrid);
        Assert.AreEqual(FilasGrid, grid.Rows.Count, "El grid debe materializar las 5.000 filas.");

        var formato = new CatalogoFormatoCeldas(grid, ctx, proyectoId, MaterialesTipo, Vista);
        formato.Cargar(); // UNA consulta; llena el diccionario en memoria.

        var columnas = new[]
        {
            grid.Columns["col_Clave"].Index,
            grid.Columns["col_PrecioUnitario"].Index,
            grid.Columns["col_Descripcion"].Index,
        };

        var sw = Stopwatch.StartNew();
        var celdasPintadas = 0;
        for (var fila = 0; fila < FilasGrid; fila++)
        {
            foreach (var columna in columnas)
            {
                var args = new DataGridViewCellFormattingEventArgs(
                    columna, fila, value: null, desiredType: null, new DataGridViewCellStyle());
                formato.AplicarOverride(args);
                celdasPintadas++;
            }
        }
        sw.Stop();

        TestContext.WriteLine($"[perf] Repaint {celdasPintadas} celdas ({FilasGrid} filas × {columnas.Length} columnas): " +
                              $"{sw.ElapsedMilliseconds} ms ({sw.Elapsed.TotalMilliseconds:F1} ms), umbral {UmbralRepaintMs} ms");

        Assert.AreEqual(FilasGrid * columnas.Length, celdasPintadas);
        Assert.IsTrue(sw.ElapsedMilliseconds < UmbralRepaintMs,
            $"El repaint de {celdasPintadas} celdas tardó {sw.ElapsedMilliseconds} ms (umbral {UmbralRepaintMs} ms).");
    }

    [TestMethod]
    public void DiccionarioEnMemoria_EsProporcionalALosOverrides_NoALasFilas()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        // Sólo 25 overrides en un grid de 5.000 filas (3 columnas = 15.000 celdas).
        var lote = Enumerable.Range(1, 25)
            .Select(i => new CambioFormatoCeldaCommand(
                Vista, ClaveFilaReporte.Insumo(i), "PrecioUnitario",
                ExpectedOverlay: null,
                ProposedOverlay: new ReportStyleOverlay(ColorFondo: "#445566")))
            .ToList();
        FormatoTestWriter.Aplicar(ctx, proyectoId, MaterialesTipo, lote);

        using var grid = CrearGrid(FilasGrid);
        var formato = new CatalogoFormatoCeldas(grid, ctx, proyectoId, MaterialesTipo, Vista);
        formato.Cargar();

        // El pintado no toca la BD: se puede recorrer TODO el grid sin conexión.
        ctx.Database.CloseConnection();

        var columnas = new[]
        {
            grid.Columns["col_Clave"].Index,
            grid.Columns["col_PrecioUnitario"].Index,
            grid.Columns["col_Descripcion"].Index,
        };

        for (var fila = 0; fila < FilasGrid; fila++)
        {
            foreach (var columna in columnas)
            {
                var args = new DataGridViewCellFormattingEventArgs(
                    columna, fila, value: null, desiredType: null, new DataGridViewCellStyle());
                formato.AplicarOverride(args); // no debe lanzar: no consulta la BD
            }
        }

        TestContext.WriteLine($"[perf] Repaint sin conexión a BD completado sobre {FilasGrid * columnas.Length} celdas " +
                              "con sólo 25 overrides en memoria (diccionario disperso).");
    }
}
