using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Indirectos;

/// <summary>
/// Issue #205 (piloto de tema de Indirectos): el tema viste SOLO el chrome del
/// módulo (header, toolbar, tabs, encabezados, status y acción primaria). La
/// configuración de columnas persistida por el usuario (fondo y color de
/// fuente en <c>ColumnaIndirectos</c>) es dato de usuario y debe sobrevivir a
/// la construcción real de <see cref="FormIndirectos"/> y a una re-aplicación
/// del tema, en los TRES presets, sin reescribirse en la base.
///
/// El formulario se construye con una BD SQLite temporal y columnas ya
/// personalizadas (el helper de defaults no vuelve a sembrar). La baseline de
/// persistencia se toma DESPUÉS de ese setup, se relee con un contexto nuevo y
/// se compara: no se asume nada del change tracker.
///
/// Limitaciones declaradas: no se hace <c>Show()</c> (no corre <c>Load</c>),
/// por lo que no se ejercitan overlays de celda ni estilos por rol; esos
/// caminos ya tienen cobertura en <c>FormatoCeldasIndirectosTests</c> y
/// <c>EstilosFilaIndirectosGridTests</c>. Los colores de CELDA no se asertan
/// aquí a propósito: son datos del usuario y no un rol del tema.
/// </summary>
[STATestClass]
[DoNotParallelize] // Cambia la paleta activa del proceso y construye formularios reales.
public class IndirectosTemaPreservaFormatoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    /// <summary>Fondo y fuente que el usuario persiste por columna (hex de BD).</summary>
    private static readonly Dictionary<string, (string Fondo, string Fuente)> ColoresPersonalizados =
        new(StringComparer.Ordinal)
        {
            ["Grupo"] = ("#123456", "#FEDCBA"),
            ["ImporteMensual"] = ("#1E5F74", "#FFF4E6"),
            ["Duracion"] = ("#F0F1F2", "#101112"),
            // Distinto del gris hardcodeado (240,240,240): prueba que la
            // configuración persistida gana al default de la columna calculada.
            ["ImporteTotal"] = ("#E1E2E3", "#010203"),
        };

    private static readonly Dictionary<string, string> ColumnaGridPorNombreInterno =
        new(StringComparer.Ordinal)
        {
            ["Grupo"] = "colGrupo",
            ["ImporteMensual"] = "colImporteMensual",
            ["Duracion"] = "colDuracion",
            ["ImporteTotal"] = "colImporteTotal",
        };

    private static readonly string[] Grids = { "dgvOficinaCentral", "dgvCampo" };

    [TestMethod]
    public void ColumnasPersistidas_ConservanFondoYFuente_TrasConstruirYReaplicarTema_ClaroAzul()
        => VerificarPreset(SoproTemaPreset.ClaroAzul);

    [TestMethod]
    public void ColumnasPersistidas_ConservanFondoYFuente_TrasConstruirYReaplicarTema_Grafito()
        => VerificarPreset(SoproTemaPreset.Grafito);

    [TestMethod]
    public void ColumnasPersistidas_ConservanFondoYFuente_TrasConstruirYReaplicarTema_ClaroVerde()
        => VerificarPreset(SoproTemaPreset.ClaroVerde);

    private static void VerificarPreset(SoproTemaPreset preset)
    {
        SoproTemaPreset presetPrevio = SoproTheme.CurrentPalette.Preset;
        Proyecto? proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        string root = Path.Combine(Path.GetTempPath(), $"sopro_ind_tema_fmt_{Guid.NewGuid():N}");
        string dbPath = Path.Combine(root, "proyecto-temporal.soproproj");

        SOPROContext? ctx = null;
        FormIndirectos? form = null;
        try
        {
            SoproTheme.InitializePalette(preset);

            Directory.CreateDirectory(root);
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            Proyecto proyecto = CrearProyecto(ctx);
            IReadOnlyList<BaselineColumna> baseline = PersistirFormatoDeColumnas(ctx, proyecto.Id);

            // Construcción real: InitializeComponent + AplicarTema + ConfigurarGrids
            // (que aplica la configuración persistida a ambos grids).
            form = new FormIndirectos(ctx, proyecto);

            AssertarFormatoDeGrids(form, $"{preset} tras construir");

            // Re-aplicar el tema sobre un formulario ya construido tampoco debe
            // repintar las columnas configuradas por el usuario.
            form.AplicarTema();
            AssertarFormatoDeGrids(form, $"{preset} tras re-aplicar el tema");

            AssertarBaselineSinCambios(dbPath, proyecto.Id, baseline, preset.ToString());
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            SoproTheme.InitializePalette(presetPrevio);
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    // ═════════════════════════════════ Infraestructura ═════════════════════════════

    private static Proyecto CrearProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto tema indirectos",
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
            PorcentajeIVA = 0m,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto;
    }

    /// <summary>
    /// Siembra los defaults persistentes reales, personaliza fondo/fuente de
    /// las columnas conocidas y devuelve la baseline leída de la BD DESPUÉS
    /// del setup (sin entidades trackeadas).
    /// </summary>
    private static IReadOnlyList<BaselineColumna> PersistirFormatoDeColumnas(SOPROContext ctx, int proyectoId)
    {
        ColumnasIndirectosHelper.CrearColumnasPredeterminadas(ctx, proyectoId);

        foreach (ColumnaIndirectos columna in ctx.ColumnasIndirectos
                     .Where(c => c.ProyectoId == proyectoId)
                     .ToList())
        {
            if (!ColoresPersonalizados.TryGetValue(columna.NombreInterno, out var colores))
                continue;

            columna.ColorFondo = colores.Fondo;
            columna.ColorFuente = colores.Fuente;
            columna.FechaModificacion = new DateTime(2026, 1, 2);
        }

        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();

        return ctx.ColumnasIndirectos
            .AsNoTracking()
            .Where(c => c.ProyectoId == proyectoId)
            .OrderBy(c => c.Orden)
            .Select(c => new BaselineColumna(c.NombreInterno, c.ColorFondo, c.ColorFuente))
            .ToList();
    }

    private static void AssertarFormatoDeGrids(FormIndirectos form, string contexto)
    {
        foreach (string nombreGrid in Grids)
        {
            DataGridView grid = ObtenerGrid(form, nombreGrid);

            foreach (var esperada in ColoresPersonalizados)
            {
                string nombreColumna = ColumnaGridPorNombreInterno[esperada.Key];
                Assert.IsTrue(grid.Columns.Contains(nombreColumna),
                    $"{contexto}: {nombreGrid} debe tener la columna {nombreColumna}.");

                DataGridViewCellStyle estilo = grid.Columns[nombreColumna].DefaultCellStyle;

                Assert.AreEqual(
                    ColorTranslator.FromHtml(esperada.Value.Fondo).ToArgb(),
                    estilo.BackColor.ToArgb(),
                    $"{contexto}: {nombreGrid}.{nombreColumna} debe conservar el fondo persistido.");
                Assert.AreEqual(
                    ColorTranslator.FromHtml(esperada.Value.Fuente).ToArgb(),
                    estilo.ForeColor.ToArgb(),
                    $"{contexto}: {nombreGrid}.{nombreColumna} debe conservar el color de fuente persistido.");
            }
        }
    }

    private static void AssertarBaselineSinCambios(
        string dbPath,
        int proyectoId,
        IReadOnlyList<BaselineColumna> baseline,
        string contexto)
    {
        using var verificacion = new SOPROContext(dbPath);
        List<BaselineColumna> actuales = verificacion.ColumnasIndirectos
            .AsNoTracking()
            .Where(c => c.ProyectoId == proyectoId)
            .OrderBy(c => c.Orden)
            .Select(c => new BaselineColumna(c.NombreInterno, c.ColorFondo, c.ColorFuente))
            .ToList();

        Assert.AreEqual(baseline.Count, actuales.Count,
            $"{contexto}: la construcción no debe sembrar ni duplicar columnas.");

        for (int i = 0; i < baseline.Count; i++)
        {
            Assert.AreEqual(baseline[i].NombreInterno, actuales[i].NombreInterno,
                $"{contexto}: orden/nombre persistido intacto.");
            Assert.AreEqual(baseline[i].ColorFondo, actuales[i].ColorFondo,
                $"{contexto}: el fondo de {baseline[i].NombreInterno} no debe reescribirse en BD.");
            Assert.AreEqual(baseline[i].ColorFuente, actuales[i].ColorFuente,
                $"{contexto}: la fuente de {baseline[i].NombreInterno} no debe reescribirse en BD.");
        }
    }

    private static DataGridView ObtenerGrid(Form form, string nombre)
    {
        Control[] encontrados = form.Controls.Find(nombre, searchAllChildren: true);
        Assert.AreEqual(1, encontrados.Length, $"Debe existir un único {nombre} en el formulario.");

        return encontrados[0] as DataGridView
            ?? throw new AssertFailedException($"{nombre} debe ser un DataGridView.");
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)CampoProyectoGlobal().GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => CampoProyectoGlobal().SetValue(null, previo);

    private static FieldInfo CampoProyectoGlobal()
        => typeof(FormatoHelper).GetField(
               CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)
           ?? throw new InvalidOperationException(
               $"No se encontró el campo FormatoHelper.{CampoProyectoGlobalFormato}.");

    private static void EliminarDirectorio(string root)
    {
        for (int intento = 0; intento < 5; intento++)
        {
            try
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException) when (intento < 4)
            {
                System.Threading.Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException) when (intento < 4)
            {
                System.Threading.Thread.Sleep(200);
            }
        }
    }

    private sealed record BaselineColumna(string NombreInterno, string ColorFondo, string ColorFuente);
}
