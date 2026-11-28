using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// Issue #207 — el tema de los catálogos viste SOLO el chrome. El formato de
/// columna que el usuario persiste en BD (fondo, fuente, alineación, wrap y
/// formato numérico) debe sobrevivir a la construcción real del formulario, a
/// la reconstrucción de columnas (<c>ConfigurarGrid()</c>) y a una nueva
/// construcción (reaplicación del tema) en los tres presets, sin reescribirse
/// en la base.
///
/// La baseline se toma DESPUÉS del setup y se relee con un contexto NUEVO:
/// no se asume nada del change tracker. Los colores de celda y su persistencia
/// amplia ya están cubiertos en <c>FormatoCeldasCatalogosTests</c>; aquí sólo
/// se añade la capa de columnas por tipo de catálogo.
/// </summary>
[STATestClass]
[DoNotParallelize] // Cambia la paleta global del proceso y construye formularios reales.
public class CatalogosTemaPreservaFormatoTests
{
    private static readonly EstiloEsperado EstiloClave =
        new(Fondo: "#112233", Fuente: "#FEDCBA", Formato: string.Empty, FormatoSeAplicaAlGrid: false);

    private static readonly EstiloEsperado EstiloNumerico =
        new(Fondo: "#123456", Fuente: "#F0E1D2", Formato: "N3", FormatoSeAplicaAlGrid: true);

    /// <summary>
    /// Columnas persistidas que se personalizan por catálogo. El nombre del
    /// control es el que produce <c>ConfigurarGrid()</c> a partir de
    /// <c>NombreInterno</c>.
    /// </summary>
    private static readonly Dictionary<string, ColumnaPersistida[]> ColumnasPorCatalogo =
        new(StringComparer.Ordinal)
        {
            ["Materiales"] = new[]
            {
                new ColumnaPersistida("Clave", "col_Clave", EstiloClave),
                new ColumnaPersistida("PrecioUnitario", "col_PrecioUnitario", EstiloNumerico),
            },
            ["Mano de Obra"] = new[]
            {
                new ColumnaPersistida("Clave", "col_Clave", EstiloClave),
                new ColumnaPersistida("SalarioBase", "col_SalarioBase", EstiloNumerico),
            },
            ["Herramientas"] = new[]
            {
                new ColumnaPersistida("Clave", "col_Clave", EstiloClave),
                // Herramientas no vuelca FormatoNumerico al grid, pero sí debe
                // conservarlo íntegro en la base.
                new ColumnaPersistida(
                    "PrecioUnitario", "col_PrecioUnitario",
                    EstiloNumerico with { FormatoSeAplicaAlGrid = false }),
            },
            ["Maquinaria"] = new[]
            {
                new ColumnaPersistida("Clave", "col_Clave", EstiloClave),
                new ColumnaPersistida("CostoHorario", "col_CostoHorario", EstiloNumerico),
            },
        };

    [TestInitialize]
    public void VerificarApartmentState()
    {
        Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState(),
            "Los tests de preservación construyen formularios WinForms y deben correr en STA.");
    }

    [TestMethod]
    [DataRow("claro-azul")]
    [DataRow("grafito")]
    [DataRow("claro-verde")]
    public void ColumnasPersistidas_ConservanFormato_TrasConstruirReconfigurarYReaplicar(string presetId)
    {
        Assert.IsTrue(SoproTheme.TryParsePreset(presetId, out SoproTemaPreset preset));

        using var escenario = new CatalogosTemaEscenario(preset, SembrarFormatosDeColumnas);
        BaselinePersistida baseline = LeerBaseline(escenario.DbPath, escenario.Proyecto.Id);

        foreach (CatalogoBajoTema catalogo in CatalogoBajoTema.Todos)
        {
            Form form = escenario.Crear(catalogo);
            AssertarColumnasDelGrid(form, catalogo, $"{preset}: {catalogo.Nombre} tras construir");

            // Reconstrucción real de columnas: el chrome de rejilla se
            // reaplica, pero el formato persistido del usuario no cambia.
            CatalogosTemaEntorno.InvocarConfigurarGrid(form, catalogo.TipoFormulario);
            AssertarColumnasDelGrid(
                form, catalogo, $"{preset}: {catalogo.Nombre} tras ConfigurarGrid()");

            // Nueva instancia = reaplicación del tema sobre el mismo estado
            // persistido, sin depender de un método interno del helper de tema.
            Form reconstruido = escenario.Crear(catalogo);
            AssertarColumnasDelGrid(
                reconstruido, catalogo, $"{preset}: {catalogo.Nombre} tras reconstruir el formulario");
        }

        AssertarBaselineSinCambios(escenario.DbPath, escenario.Proyecto.Id, baseline, preset.ToString());
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static void SembrarFormatosDeColumnas(SOPROContext ctx, int proyectoId)
    {
        ColumnasMaterialHelper.CrearColumnasPredeterminadas(ctx, proyectoId);
        ColumnasManoObraHelper.CrearColumnasPredeterminadas(ctx, proyectoId);
        ColumnasHerramientaHelper.CrearColumnasPredeterminadas(ctx, proyectoId);
        ColumnasMaquinariaHelper.CrearColumnasPredeterminadas(ctx, proyectoId);

        foreach (ColumnaMaterial columna in ctx.ColumnasMaterial.Where(c => c.ProyectoId == proyectoId).ToList())
            if (ColumnasPorCatalogo["Materiales"].FirstOrDefault(c => c.NombreInterno == columna.NombreInterno) is { } cfg)
                Personalizar(columna, cfg.Estilo);

        foreach (ColumnaManoObra columna in ctx.ColumnasManoObra.Where(c => c.ProyectoId == proyectoId).ToList())
            if (ColumnasPorCatalogo["Mano de Obra"].FirstOrDefault(c => c.NombreInterno == columna.NombreInterno) is { } cfg)
                Personalizar(columna, cfg.Estilo);

        foreach (ColumnaHerramienta columna in ctx.ColumnasHerramienta.Where(c => c.ProyectoId == proyectoId).ToList())
            if (ColumnasPorCatalogo["Herramientas"].FirstOrDefault(c => c.NombreInterno == columna.NombreInterno) is { } cfg)
                Personalizar(columna, cfg.Estilo);

        foreach (ColumnaMaquinaria columna in ctx.ColumnasMaquinaria.Where(c => c.ProyectoId == proyectoId).ToList())
            if (ColumnasPorCatalogo["Maquinaria"].FirstOrDefault(c => c.NombreInterno == columna.NombreInterno) is { } cfg)
                Personalizar(columna, cfg.Estilo);

        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
    }

    private static void Personalizar(ColumnaMaterial columna, EstiloEsperado estilo)
        => AsignarEstilo(
            estilo,
            v => columna.ColorFondo = v,
            v => columna.ColorFuente = v,
            v => columna.NombreFuente = v,
            v => columna.TamanoFuente = v,
            v => columna.Negrita = v,
            v => columna.Cursiva = v,
            v => columna.WrapTexto = v,
            v => columna.FormatoNumerico = v,
            v => columna.AlineacionVertical = v,
            () => columna.Alineacion = AlineacionColumna.Derecha,
            () => columna.FechaModificacion = new DateTime(2026, 2, 2));

    private static void Personalizar(ColumnaManoObra columna, EstiloEsperado estilo)
        => AsignarEstilo(
            estilo,
            v => columna.ColorFondo = v,
            v => columna.ColorFuente = v,
            v => columna.NombreFuente = v,
            v => columna.TamanoFuente = v,
            v => columna.Negrita = v,
            v => columna.Cursiva = v,
            v => columna.WrapTexto = v,
            v => columna.FormatoNumerico = v,
            v => columna.AlineacionVertical = v,
            () => columna.Alineacion = AlineacionColumna.Derecha,
            () => columna.FechaModificacion = new DateTime(2026, 2, 2));

    private static void Personalizar(ColumnaHerramienta columna, EstiloEsperado estilo)
        => AsignarEstilo(
            estilo,
            v => columna.ColorFondo = v,
            v => columna.ColorFuente = v,
            v => columna.NombreFuente = v,
            v => columna.TamanoFuente = v,
            v => columna.Negrita = v,
            v => columna.Cursiva = v,
            v => columna.WrapTexto = v,
            v => columna.FormatoNumerico = v,
            v => columna.AlineacionVertical = v,
            () => columna.Alineacion = AlineacionColumna.Derecha,
            () => columna.FechaModificacion = new DateTime(2026, 2, 2));

    private static void Personalizar(ColumnaMaquinaria columna, EstiloEsperado estilo)
        => AsignarEstilo(
            estilo,
            v => columna.ColorFondo = v,
            v => columna.ColorFuente = v,
            v => columna.NombreFuente = v,
            v => columna.TamanoFuente = v,
            v => columna.Negrita = v,
            v => columna.Cursiva = v,
            v => columna.WrapTexto = v,
            v => columna.FormatoNumerico = v,
            v => columna.AlineacionVertical = v,
            () => columna.Alineacion = AlineacionColumna.Derecha,
            () => columna.FechaModificacion = new DateTime(2026, 2, 2));

    private static void AsignarEstilo(
        EstiloEsperado estilo,
        Action<string> fondo,
        Action<string> fuente,
        Action<string> nombreFuente,
        Action<int> tamanoFuente,
        Action<bool> negrita,
        Action<bool> cursiva,
        Action<bool> wrap,
        Action<string> formato,
        Action<int> alineacionVertical,
        Action alineacion,
        Action fechaModificacion)
    {
        fondo(estilo.Fondo);
        fuente(estilo.Fuente);
        nombreFuente("Segoe UI");
        tamanoFuente(11);
        negrita(true);
        cursiva(true);
        wrap(true);
        formato(estilo.Formato);
        alineacionVertical(2);
        alineacion();
        fechaModificacion();
    }

    private static void AssertarColumnasDelGrid(Form form, CatalogoBajoTema catalogo, string contexto)
    {
        var grid = (DataGridView)CatalogosTemaEntorno.Buscar(form, catalogo.Grid);

        foreach (ColumnaPersistida columna in ColumnasPorCatalogo[catalogo.Nombre])
        {
            Assert.IsTrue(grid.Columns.Contains(columna.NombreColumna),
                $"{contexto}: debe existir la columna {columna.NombreColumna}.");

            DataGridViewColumn columnaGrid = grid.Columns[columna.NombreColumna]
                ?? throw new AssertFailedException($"{contexto}: no se encontró la columna {columna.NombreColumna}.");
            DataGridViewCellStyle estilo = columnaGrid.DefaultCellStyle;
            Assert.AreEqual(
                ColorTranslator.FromHtml(columna.Estilo.Fondo).ToArgb(),
                estilo.BackColor.ToArgb(),
                $"{contexto}: {columna.NombreColumna} conserva el fondo persistido.");

            Assert.AreEqual(
                ColorTranslator.FromHtml(columna.Estilo.Fuente).ToArgb(),
                estilo.ForeColor.ToArgb(),
                $"{contexto}: {columna.NombreColumna} conserva la fuente persistida.");

            Font fuente = estilo.Font
                ?? throw new AssertFailedException($"{contexto}: {columna.NombreColumna} conserva una fuente.");
            Assert.AreEqual("Segoe UI", fuente.Name, $"{contexto}: familia persistida.");
            Assert.AreEqual(11f, fuente.Size, $"{contexto}: tamaño persistido.");
            Assert.AreEqual(FontStyle.Bold | FontStyle.Italic, fuente.Style,
                $"{contexto}: negrita/cursiva persistidas.");

            Assert.AreEqual(DataGridViewContentAlignment.BottomRight, estilo.Alignment,
                $"{contexto}: alineación persistida.");
            Assert.AreEqual(DataGridViewTriState.True, estilo.WrapMode,
                $"{contexto}: wrap persistido.");

            if (columna.Estilo.FormatoSeAplicaAlGrid)
                Assert.AreEqual(columna.Estilo.Formato, estilo.Format,
                    $"{contexto}: formato numérico persistido.");
        }
    }

    private static BaselinePersistida LeerBaseline(string dbPath, int proyectoId)
    {
        using var ctx = new SOPROContext(dbPath);

        return new BaselinePersistida(
            Materiales: ctx.ColumnasMaterial.AsNoTracking()
                .Where(c => c.ProyectoId == proyectoId).OrderBy(c => c.Orden)
                .Select(c => new FilaPersistida(
                    c.NombreInterno, c.ColorFondo, c.ColorFuente, c.NombreFuente,
                    c.TamanoFuente, c.Negrita, c.Cursiva, c.WrapTexto,
                    c.FormatoNumerico, c.Alineacion, c.AlineacionVertical))
                .ToList(),
            ManoObra: ctx.ColumnasManoObra.AsNoTracking()
                .Where(c => c.ProyectoId == proyectoId).OrderBy(c => c.Orden)
                .Select(c => new FilaPersistida(
                    c.NombreInterno, c.ColorFondo, c.ColorFuente, c.NombreFuente,
                    c.TamanoFuente, c.Negrita, c.Cursiva, c.WrapTexto,
                    c.FormatoNumerico, c.Alineacion, c.AlineacionVertical))
                .ToList(),
            Herramientas: ctx.ColumnasHerramienta.AsNoTracking()
                .Where(c => c.ProyectoId == proyectoId).OrderBy(c => c.Orden)
                .Select(c => new FilaPersistida(
                    c.NombreInterno, c.ColorFondo, c.ColorFuente, c.NombreFuente,
                    c.TamanoFuente, c.Negrita, c.Cursiva, c.WrapTexto,
                    c.FormatoNumerico, c.Alineacion, c.AlineacionVertical))
                .ToList(),
            Maquinaria: ctx.ColumnasMaquinaria.AsNoTracking()
                .Where(c => c.ProyectoId == proyectoId).OrderBy(c => c.Orden)
                .Select(c => new FilaPersistida(
                    c.NombreInterno, c.ColorFondo, c.ColorFuente, c.NombreFuente,
                    c.TamanoFuente, c.Negrita, c.Cursiva, c.WrapTexto,
                    c.FormatoNumerico, c.Alineacion, c.AlineacionVertical))
                .ToList());
    }

    private static void AssertarBaselineSinCambios(
        string dbPath, int proyectoId, BaselinePersistida baseline, string contexto)
    {
        BaselinePersistida actual = LeerBaseline(dbPath, proyectoId);

        AssertarTabla(baseline.Materiales, actual.Materiales, $"{contexto}: ColumnasMaterial");
        AssertarTabla(baseline.ManoObra, actual.ManoObra, $"{contexto}: ColumnasManoObra");
        AssertarTabla(baseline.Herramientas, actual.Herramientas, $"{contexto}: ColumnasHerramienta");
        AssertarTabla(baseline.Maquinaria, actual.Maquinaria, $"{contexto}: ColumnasMaquinaria");
    }

    private static void AssertarTabla(
        IReadOnlyList<FilaPersistida> esperadas,
        IReadOnlyList<FilaPersistida> actuales,
        string contexto)
    {
        Assert.AreEqual(esperadas.Count, actuales.Count,
            $"{contexto}: la construcción no debe sembrar ni duplicar columnas.");

        for (int i = 0; i < esperadas.Count; i++)
        {
            Assert.AreEqual(esperadas[i], actuales[i],
                $"{contexto}: la fila {esperadas[i].NombreInterno} cambió en la base.");
        }
    }

    private sealed record EstiloEsperado(
        string Fondo, string Fuente, string Formato, bool FormatoSeAplicaAlGrid);

    private sealed record ColumnaPersistida(
        string NombreInterno, string NombreColumna, EstiloEsperado Estilo);

    private sealed record FilaPersistida(
        string NombreInterno,
        string ColorFondo,
        string ColorFuente,
        string NombreFuente,
        int TamanoFuente,
        bool Negrita,
        bool Cursiva,
        bool WrapTexto,
        string FormatoNumerico,
        AlineacionColumna Alineacion,
        int AlineacionVertical);

    private sealed record BaselinePersistida(
        IReadOnlyList<FilaPersistida> Materiales,
        IReadOnlyList<FilaPersistida> ManoObra,
        IReadOnlyList<FilaPersistida> Herramientas,
        IReadOnlyList<FilaPersistida> Maquinaria);
}
