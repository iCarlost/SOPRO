using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Undo;

/// <summary>
/// Todas las rutas Undo/Redo de formato por celda comparten la MISMA fábrica de
/// lote (<see cref="FormatoLoteCambios"/>): el destino debe ser el estado "antes"
/// (undo) o "después" (do/redo) COMPLETO y el comando debe viajar en
/// <see cref="OverlayApplyMode.Replace"/> para que un undo quite también las
/// propiedades añadidas por la edición. Si algún formulario vuelve a construir su
/// lote a mano y pierde el modo, esta prueba lo detecta.
/// </summary>
[TestClass]
public class FormatoLoteCambiosTests
{
    [TestMethod]
    public void TodosLosFormularios_UsanReplaceConElEstadoCompletoAntesYDespues()
    {
        var direccion = new ReportCellAddress(string.Empty, "C:1", "ImporteTotal");
        var antes = new ReportStyleOverlay(ColorFondo: "#111111");
        var despues = new ReportStyleOverlay(Negrita: true, ColorFondo: "#111111");
        var cambios = new[] { new CambioFormatoSeleccion(direccion, antes, despues) };

        var fabricas = Fabricas().ToList();
        Assert.AreEqual(11, fabricas.Count, "Faltan formularios con formato por celda en la validación.");

        foreach (var (nombre, construir) in fabricas)
        {
            var undo = construir(cambios, true).Single();
            Assert.AreEqual(OverlayApplyMode.Replace, undo.ApplyMode,
                $"{nombre}: undo debe reemplazar el overlay (no fusionar el 'antes' parcial).");
            Assert.IsTrue(Equals(despues, undo.ExpectedOverlay),
                $"{nombre}: la base esperada del undo es el 'después'.");
            Assert.AreEqual(antes, undo.ProposedOverlay, $"{nombre}: el undo propone el estado completo anterior.");

            var redo = construir(cambios, false).Single();
            Assert.AreEqual(OverlayApplyMode.Replace, redo.ApplyMode,
                $"{nombre}: redo también reemplaza el overlay completo.");
            Assert.IsTrue(Equals(antes, redo.ExpectedOverlay),
                $"{nombre}: la base esperada del redo es el 'antes'.");
            Assert.AreEqual(despues, redo.ProposedOverlay, $"{nombre}: el redo propone el estado completo posterior.");
        }
    }

    [TestMethod]
    public void FilaSinOverridePrevio_UndoProponeNullParaBorrarla()
    {
        var direccion = new ReportCellAddress(string.Empty, "C:2", "ImporteTotal");
        var despues = new ReportStyleOverlay(Negrita: true);
        var cambios = new[] { new CambioFormatoSeleccion(direccion, null, despues) };

        foreach (var (nombre, construir) in Fabricas())
        {
            var undo = construir(cambios, true).Single();
            Assert.IsNull(undo.ProposedOverlay, $"{nombre}: sin estado anterior el undo BORRA el override.");
            Assert.IsTrue(Equals(despues, undo.ExpectedOverlay),
                $"{nombre}: la base esperada es el 'después'.");
        }
    }

    private static IEnumerable<(
        string Nombre,
        Func<IEnumerable<CambioFormatoSeleccion>, bool, IReadOnlyList<CambioFormatoCeldaCommand>> Construir)> Fabricas()
    {
        yield return ("Materiales", FormCatalogoMateriales.ConstruirLoteCambios);
        yield return ("Mano de Obra", FormCatalogoManoObra.ConstruirLoteCambios);
        yield return ("Herramientas", FormCatalogoHerramientas.ConstruirLoteCambios);
        yield return ("Maquinaria", FormCatalogoMaquinaria.ConstruirLoteCambios);
        yield return ("Explosión de Insumos", FormExplosionInsumos.ConstruirLoteCambios);
        yield return ("Financiamiento", FormFinanciamiento.ConstruirLoteCambios);
        yield return ("Indirectos", FormIndirectos.ConstruirLoteCambios);
        yield return ("Matrices", FormMatrices.ConstruirLoteCambios);
        yield return ("Presupuesto", FormPresupuesto.ConstruirLoteCambios);
        yield return ("Programa de Insumos", FormProgramaInsumos.ConstruirLoteCambios);
        yield return ("Programa de Obra", FormProgramaObra.ConstruirLoteCambios);
    }
}
