using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Indirectos;

/// <summary>
/// Paso 5.1 — el GRID de Indirectos pinta las filas de GRUPO con los DEFAULTS
/// NEUTRALES por rol (<see cref="EstilosFilaPredeterminados"/>) combinados con el
/// override PERSISTIDO del usuario. Precedencia: override de celda &gt; rol del
/// usuario &gt; default del rol. Sin configuración, el gris es idéntico al
/// histórico (#E6E6E6 = 230,230,230).
/// </summary>
[TestClass]
public class EstilosFilaIndirectosGridTests
{
    private static readonly Font FuenteBase = new("Segoe UI", 9f);

    private static DataGridViewCellStyle Estilo(string? tipo, IReadOnlyDictionary<string, ReportStyleOverlay>? usuario = null)
    {
        var estilo = new DataGridViewCellStyle { BackColor = Color.White, ForeColor = Color.Black };
        FormIndirectos.AplicarEstiloRolIndirectos(estilo, FuenteBase, tipo, usuario);
        return estilo;
    }

    private static IReadOnlyDictionary<string, ReportStyleOverlay> Usuario(string rol, ReportStyleOverlay overlay)
        => new Dictionary<string, ReportStyleOverlay>(System.StringComparer.OrdinalIgnoreCase) { [rol] = overlay };

    // ─────────────────────────── Defaults (sin configuración) ───────────────────────────

    [TestMethod]
    public void Grupo_SinConfig_UsaElDefaultGrisYNegrita()
    {
        var estilo = Estilo("GRUPO");

        Assert.AreEqual(ColorTranslator.FromHtml("#E6E6E6").ToArgb(), estilo.BackColor.ToArgb());
        Assert.IsTrue(estilo.Font!.Bold);
    }

    [TestMethod]
    public void Concepto_SinConfig_NoTieneColorDeRol()
    {
        var estilo = Estilo("CONCEPTO");

        Assert.AreEqual(Color.White.ToArgb(), estilo.BackColor.ToArgb(), "El concepto hereda el color (no lo pisa).");
        Assert.IsFalse(estilo.Font!.Bold);
    }

    // ─────────────────────────── Override del usuario gana al default ───────────────────────────

    [TestMethod]
    public void Grupo_ConConfigDelUsuario_GanaElUsuarioYParcialHeredaDefault()
    {
        var estilo = Estilo("GRUPO", Usuario(RolFilaReporte.Grupo, new ReportStyleOverlay(ColorFondo: "#010203")));

        Assert.AreEqual(Color.FromArgb(0x01, 0x02, 0x03).ToArgb(), estilo.BackColor.ToArgb(), "El fondo del usuario gana al default.");
        Assert.IsTrue(estilo.Font!.Bold, "La negrita hereda del default.");
    }

    [TestMethod]
    public void Grupo_ConConfigDelUsuarioSinNegrita_RespetaAlUsuario()
    {
        var estilo = Estilo("GRUPO", Usuario(RolFilaReporte.Grupo, new ReportStyleOverlay(Negrita: false)));

        Assert.IsFalse(estilo.Font!.Bold, "El usuario puede quitar la negrita del rol.");
        Assert.AreEqual(ColorTranslator.FromHtml("#E6E6E6").ToArgb(), estilo.BackColor.ToArgb(), "El fondo sigue siendo el default.");
    }

    // ─────────────────────────── Override de celda gana a todo ───────────────────────────

    [TestMethod]
    public void OverrideDeCelda_GanaSobreElRol()
    {
        var estilo = Estilo("GRUPO");
        FormPresupuesto.AplicarOverlayACellStyle(estilo, new ReportStyleOverlay(ColorFondo: "#ABCDEF"));

        Assert.AreEqual(Color.FromArgb(0xAB, 0xCD, 0xEF).ToArgb(), estilo.BackColor.ToArgb(),
            "El override de celda del paso 5.1 gana sobre el default del rol.");
    }

    // ─────────────────────────── Mapeo tipo → rol ───────────────────────────

    [TestMethod]
    public void ObtenerRolFilaFormato_MapeaLosTiposDelGrid()
    {
        Assert.AreEqual(RolFilaReporte.Grupo, FormIndirectos.ObtenerRolFilaFormato("GRUPO"));
        Assert.AreEqual(RolFilaReporte.Concepto, FormIndirectos.ObtenerRolFilaFormato("CONCEPTO"));
        Assert.IsNull(FormIndirectos.ObtenerRolFilaFormato("Otro"));
        Assert.IsNull(FormIndirectos.ObtenerRolFilaFormato(null));
    }
}
