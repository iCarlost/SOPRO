using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Formato;

/// <summary>
/// Pruebas del <see cref="FormatoSeleccionCoordinator"/>: decide el DESTINO del
/// formato del ribbon (columna vs selección de celdas), aplica el overlay por
/// celda y produce los pares antes/después deduplicados y filtrados que la Fase
/// 4.1 persistirá. Se usan grids FAKE para no depender de UI real.
/// </summary>
[TestClass]
public class FormatoSeleccionCoordinatorTests
{
    private static readonly ReportStyleOverlay Overlay = new(Negrita: true, ColorFondo: "#112233");

    private static ReportCellAddress Dir(string fila, string columna) => new("Principal", fila, columna);

    // ═════════════════════════════════ Fakes ═════════════════════════════════

    /// <summary>Grid legacy: sólo implementa <see cref="IGridFormato"/>.</summary>
    private class GridSoloColumna : IGridFormato
    {
        public DataGridView GridPrincipal => null!;
        public ColumnaPersonalizada ColumnaSeleccionada { get; set; } = null!;
        public ColumnaPersonalizada? UltimoFormato { get; private set; }

        public void AplicarFormato(ColumnaPersonalizada fmt) => UltimoFormato = fmt;
        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt) { }

#pragma warning disable CS0067 // Evento requerido por el contrato; no se dispara en las pruebas.
        public event EventHandler? ColumnaSeleccionadaCambiada;
#pragma warning restore CS0067

        public bool GenerarReporteExcel() => false;
    }

    /// <summary>Grid que además implementa la selección de celdas.</summary>
    private sealed class GridCeldas : GridSoloColumna, IGridSeleccionFormato
    {
        public bool TieneSeleccionCeldas { get; set; }
        public List<ReportCellAddress> Direcciones { get; } = new();
        public HashSet<ReportCellAddress> NoFormateables { get; } = new();
        public Dictionary<ReportCellAddress, ReportStyleOverlay> Overlays { get; } = new();
        public List<ReportCellAddress> Pintadas { get; } = new();

        public IReadOnlyList<ReportCellAddress> ObtenerDireccionesSeleccionadas() => Direcciones;
        public bool EsCeldaFormateable(ReportCellAddress direccion) => !NoFormateables.Contains(direccion);
        public ReportStyleOverlay? ObtenerOverlayActual(ReportCellAddress direccion)
            => Overlays.TryGetValue(direccion, out var overlay) ? overlay : null;

        public void AplicarOverlayCelda(ReportCellAddress direccion, ReportStyleOverlay overlay)
        {
            Pintadas.Add(direccion);
            Overlays[direccion] = overlay;
        }
    }

    private static FormatoSeleccionCoordinator Coordinador(IGridFormato grid)
    {
        var coordinador = new FormatoSeleccionCoordinator();
        coordinador.EstablecerGrid(grid);
        return coordinador;
    }

    // ═════════════════════════════════ Pruebas ═════════════════════════════════

    [TestMethod]
    public void EncabezadoSeleccionado_DestinoEsColumna_NoAplicaASeleccion()
    {
        var grid = new GridCeldas
        {
            TieneSeleccionCeldas = false,
            ColumnaSeleccionada = new ColumnaPersonalizada { Nombre = "Costo" },
        };
        var coordinador = Coordinador(grid);

        Assert.AreEqual(DestinoFormatoGrid.Columna, coordinador.Destino, "El encabezado conserva el destino por columna.");
        Assert.IsFalse(coordinador.PuedeAplicarASeleccion);
        StringAssert.Contains(coordinador.DescripcionDestino, "columna");

        var cambios = coordinador.AplicarASeleccion(Overlay);

        Assert.AreEqual(0, cambios.Count);
        Assert.AreEqual(0, grid.Pintadas.Count, "No debe pintar celdas cuando el destino es la columna.");
    }

    [TestMethod]
    public void UnaCeldaSeleccionada_AplicaOverlaySoloAEsaCelda_YParesCorrectos()
    {
        var grid = new GridCeldas { TieneSeleccionCeldas = true };
        var direccion = Dir("C:1", "Importe");
        grid.Direcciones.Add(direccion);
        grid.Overlays[direccion] = new ReportStyleOverlay(Negrita: false);
        var coordinador = Coordinador(grid);

        Assert.AreEqual(DestinoFormatoGrid.Celdas, coordinador.Destino);
        var cambios = coordinador.AplicarASeleccion(Overlay);

        Assert.AreEqual(1, cambios.Count);
        Assert.AreEqual(direccion, cambios[0].Direccion);
        Assert.IsNotNull(cambios[0].Antes);
        Assert.IsFalse(cambios[0].Antes!.Negrita, "El 'antes' debe recuperar el overlay previo.");
        Assert.IsTrue(cambios[0].Despues.Negrita);
        Assert.AreEqual("#112233", cambios[0].Despues.ColorFondo);

        Assert.AreEqual(1, grid.Pintadas.Count, "Debe pintar exactamente la celda seleccionada.");
        Assert.AreEqual(direccion, grid.Pintadas[0]);
        Assert.AreEqual(1, coordinador.UltimosCambios.Count, "Los pares quedan listos para el undo/persistencia.");
    }

    [TestMethod]
    public void RangoContiguoShift_TodasLasCeldasConDireccionCorrecta()
    {
        var grid = new GridCeldas { TieneSeleccionCeldas = true };
        var d1 = Dir("C:1", "Importe");
        var d2 = Dir("C:2", "Importe");
        var d3 = Dir("C:3", "Importe");
        grid.Direcciones.AddRange(new[] { d1, d2, d3 });
        var coordinador = Coordinador(grid);

        var cambios = coordinador.AplicarASeleccion(Overlay);

        Assert.AreEqual(3, cambios.Count);
        CollectionAssert.AreEqual(
            new[] { d1, d2, d3 },
            cambios.Select(c => c.Direccion).ToArray());
        Assert.AreEqual(3, grid.Pintadas.Count);
    }

    [TestMethod]
    public void SeleccionDiscontinuaCtrl_DeduplicaDireccionesRepetidas()
    {
        var grid = new GridCeldas { TieneSeleccionCeldas = true };
        var d1 = Dir("C:1", "Importe");
        var d2 = Dir("C:2", "Importe");
        grid.Direcciones.AddRange(new[] { d1, d2, d1, d1, d2 });
        var coordinador = Coordinador(grid);

        var cambios = coordinador.AplicarASeleccion(Overlay);

        Assert.AreEqual(2, cambios.Count, "Las direcciones repetidas se deduplican.");
        CollectionAssert.AreEqual(
            new[] { d1, d2 },
            cambios.Select(c => c.Direccion).ToArray());
        Assert.AreEqual(2, grid.Pintadas.Count, "No debe pintar dos veces la misma dirección.");
    }

    [TestMethod]
    public void FilaVaciaOColumnaNoExportable_SeIgnora()
    {
        var grid = new GridCeldas { TieneSeleccionCeldas = true };
        var valida = Dir("C:1", "Importe");
        var filaVacia = Dir("C:2", "Importe");
        var columnaNoExportable = Dir("C:1", "Interno");
        grid.Direcciones.AddRange(new[] { valida, filaVacia, columnaNoExportable });
        grid.NoFormateables.Add(filaVacia);
        grid.NoFormateables.Add(columnaNoExportable);
        var coordinador = Coordinador(grid);

        var cambios = coordinador.AplicarASeleccion(Overlay);

        Assert.AreEqual(1, cambios.Count);
        Assert.AreEqual(valida, cambios[0].Direccion);
        Assert.AreEqual(1, grid.Pintadas.Count);
        Assert.AreEqual(valida, grid.Pintadas[0]);
    }

    [TestMethod]
    public void GridSinIGridSeleccionFormato_NoLanzaYConservaColumna()
    {
        var grid = new GridSoloColumna
        {
            ColumnaSeleccionada = new ColumnaPersonalizada { Nombre = "Cantidad" },
        };
        var coordinador = Coordinador(grid);

        Assert.IsFalse(coordinador.PuedeAplicarASeleccion, "Un grid legacy no cambia de comportamiento.");
        Assert.AreEqual(DestinoFormatoGrid.Columna, coordinador.Destino);

        var cambios = coordinador.AplicarASeleccion(Overlay);

        Assert.AreEqual(0, cambios.Count);
        Assert.IsNull(grid.UltimoFormato, "El coordinador no aplica formato por columna; lo hace el ribbon.");
    }

    [TestMethod]
    public void SeleccionConValoresDistintos_ExponeEstadoMixto()
    {
        var grid = new GridCeldas { TieneSeleccionCeldas = true };
        var d1 = Dir("C:1", "Importe");
        var d2 = Dir("C:2", "Importe");
        grid.Direcciones.AddRange(new[] { d1, d2 });
        grid.Overlays[d1] = new ReportStyleOverlay(Negrita: true);
        grid.Overlays[d2] = new ReportStyleOverlay(Negrita: false);
        var coordinador = Coordinador(grid);

        Assert.IsTrue(coordinador.EstadoMixto);
    }

    [TestMethod]
    public void SeleccionConMismosValores_NoExponeEstadoMixto()
    {
        var grid = new GridCeldas { TieneSeleccionCeldas = true };
        var d1 = Dir("C:1", "Importe");
        var d2 = Dir("C:2", "Importe");
        grid.Direcciones.AddRange(new[] { d1, d2 });
        grid.Overlays[d1] = new ReportStyleOverlay(Negrita: true);
        grid.Overlays[d2] = new ReportStyleOverlay(Negrita: true);
        var coordinador = Coordinador(grid);

        Assert.IsFalse(coordinador.EstadoMixto);
    }

    [TestMethod]
    public void ConstruirOverlay_MapeaFormatoDelRibbonAOverlayNeutral()
    {
        var fmt = new ColumnaPersonalizada
        {
            NombreFuente = "Arial",
            TamanoFuente = 12,
            Negrita = true,
            Cursiva = true,
            Alineacion = AlineacionColumna.Derecha,
            AlineacionVertical = 2,
            ColorFuente = "#000000",
            ColorFondo = "#FFFFFF",
            WrapTexto = true,
        };

        var overlay = FormatoSeleccionCoordinator.ConstruirOverlay(fmt);

        Assert.AreEqual("Arial", overlay.Fuente);
        Assert.AreEqual(12f, overlay.TamanoFuente);
        Assert.IsTrue(overlay.Negrita);
        Assert.IsTrue(overlay.Cursiva);
        Assert.AreEqual(ReportTextAlignment.Derecha, overlay.AlineacionHorizontal);
        Assert.AreEqual(ReportVerticalAlignment.Inferior, overlay.AlineacionVertical);
        Assert.AreEqual("#000000", overlay.ColorFuente);
        Assert.AreEqual("#FFFFFF", overlay.ColorFondo);
        Assert.IsTrue(overlay.Wrap);
    }
}
