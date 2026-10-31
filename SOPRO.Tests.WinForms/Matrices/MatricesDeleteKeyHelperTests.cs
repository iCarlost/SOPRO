using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Matrices;

/// <summary>
/// Issue #58: en el catálogo de matrices (grid ReadOnly) Supr debe ejecutar la
/// eliminación múltiple igual que Ctrl+Supr. La guarda compartida por el handler
/// prueba que ambos modificadores llegan a la misma ruta y que Shift/Alt, la
/// edición de celda y la ausencia de matriz objetivo no despachan el comando.
/// </summary>
[TestClass]
public class MatricesDeleteKeyHelperTests
{
    [TestMethod]
    public void SuprYCtrlSupr_DespachanLaMismaEliminacion()
    {
        Assert.IsTrue(MatricesDeleteKeyHelper.DebeDespacharSupr(
            Keys.Delete, control: false, shift: false, alt: false,
            enEdicionCelda: false, tieneObjetivo: true),
            "Supr sin modificadores debe ejecutar la eliminación.");

        Assert.IsTrue(MatricesDeleteKeyHelper.DebeDespacharSupr(
            Keys.Delete, control: true, shift: false, alt: false,
            enEdicionCelda: false, tieneObjetivo: true),
            "Ctrl+Supr conserva el alias y ejecuta la misma ruta.");
    }

    [TestMethod]
    public void Supr_NoDespachaConShiftAltEdicionOSinObjetivo()
    {
        Assert.IsFalse(MatricesDeleteKeyHelper.DebeDespacharSupr(
            Keys.Delete, control: false, shift: true, alt: false,
            enEdicionCelda: false, tieneObjetivo: true));

        Assert.IsFalse(MatricesDeleteKeyHelper.DebeDespacharSupr(
            Keys.Delete, control: false, shift: false, alt: true,
            enEdicionCelda: false, tieneObjetivo: true));

        Assert.IsFalse(MatricesDeleteKeyHelper.DebeDespacharSupr(
            Keys.Delete, control: false, shift: false, alt: false,
            enEdicionCelda: true, tieneObjetivo: true),
            "Mientras la celda está en edición, Supr pertenece al control de texto.");

        Assert.IsFalse(MatricesDeleteKeyHelper.DebeDespacharSupr(
            Keys.Delete, control: false, shift: false, alt: false,
            enEdicionCelda: false, tieneObjetivo: false),
            "Sin selección ni matriz vigente no se ejecuta la eliminación.");

        Assert.IsFalse(MatricesDeleteKeyHelper.DebeDespacharSupr(
            Keys.Back, control: false, shift: false, alt: false,
            enEdicionCelda: false, tieneObjetivo: true));
    }
}
