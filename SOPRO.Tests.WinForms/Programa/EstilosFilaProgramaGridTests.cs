using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Programa;

/// <summary>
/// Paso 6.2 — el GRID del Programa de Obra pinta las filas de AGRUPADOR/RESUMEN con
/// los DEFAULTS NEUTRALES por rol (<see cref="EstilosFilaPredeterminados"/>)
/// combinados con el override PERSISTIDO del usuario. Precedencia: override de celda
/// &gt; rol del usuario &gt; default del rol. Sin configuración el gris es idéntico al
/// histórico (#F0F0F0 = 240,240,240).
/// </summary>
[TestClass]
public class EstilosFilaProgramaGridTests
{
    private static readonly Font FuenteBase = new("Segoe UI", 9f);

    private static DataGridViewCellStyle Estilo(bool esResumen, IReadOnlyDictionary<string, ReportStyleOverlay>? usuario = null)
    {
        var estilo = new DataGridViewCellStyle { BackColor = Color.White, ForeColor = Color.Black };
        FormProgramaObra.AplicarEstiloRolProgramaObra(estilo, FuenteBase, esResumen, usuario);
        return estilo;
    }

    private static IReadOnlyDictionary<string, ReportStyleOverlay> Usuario(string rol, ReportStyleOverlay overlay)
        => new Dictionary<string, ReportStyleOverlay>(System.StringComparer.OrdinalIgnoreCase) { [rol] = overlay };

    // ─────────────────────────── Defaults (sin configuración) ───────────────────────────

    [TestMethod]
    public void Resumen_SinConfig_UsaElDefaultGrisYNegrita()
    {
        var estilo = Estilo(esResumen: true);

        Assert.AreEqual(ColorTranslator.FromHtml("#F0F0F0").ToArgb(), estilo.BackColor.ToArgb());
        Assert.IsTrue(estilo.Font!.Bold);
    }

    [TestMethod]
    public void Actividad_SinConfig_NoTieneColorDeRol()
    {
        var estilo = Estilo(esResumen: false);

        Assert.AreEqual(Color.White.ToArgb(), estilo.BackColor.ToArgb(), "La actividad hereda el color (no lo pisa).");
        Assert.IsFalse(estilo.Font!.Bold);
    }

    // ─────────────────────────── Override del usuario gana al default ───────────────────────────

    [TestMethod]
    public void Resumen_ConConfigDelUsuario_GanaElUsuarioYParcialHeredaDefault()
    {
        var estilo = Estilo(esResumen: true, Usuario(RolFilaReporte.Resumen, new ReportStyleOverlay(ColorFondo: "#010203")));

        Assert.AreEqual(Color.FromArgb(0x01, 0x02, 0x03).ToArgb(), estilo.BackColor.ToArgb(), "El fondo del usuario gana al default.");
        Assert.IsTrue(estilo.Font!.Bold, "La negrita hereda del default.");
    }

    [TestMethod]
    public void Resumen_ConConfigDelUsuarioSinNegrita_RespetaAlUsuario()
    {
        var estilo = Estilo(esResumen: true, Usuario(RolFilaReporte.Resumen, new ReportStyleOverlay(Negrita: false)));

        Assert.IsFalse(estilo.Font!.Bold, "El usuario puede quitar la negrita del rol.");
        Assert.AreEqual(ColorTranslator.FromHtml("#F0F0F0").ToArgb(), estilo.BackColor.ToArgb(), "El fondo sigue siendo el default.");
    }

    // ─────────────────────────── Override de celda gana a todo ───────────────────────────

    [TestMethod]
    public void OverrideDeCelda_GanaSobreElRol()
    {
        var estilo = Estilo(esResumen: true);
        FormPresupuesto.AplicarOverlayACellStyle(estilo, new ReportStyleOverlay(ColorFondo: "#ABCDEF"));

        Assert.AreEqual(Color.FromArgb(0xAB, 0xCD, 0xEF).ToArgb(), estilo.BackColor.ToArgb(),
            "El override de celda del paso 6.2 gana sobre el default del rol.");
    }

    // ─────────────────────────── Mapeo tipo → rol ───────────────────────────

    [TestMethod]
    public void RolFilaFormato_MapeaAgrupadorYActividad()
    {
        Assert.AreEqual(RolFilaReporte.Resumen, FormProgramaObra.RolFilaFormato(true));
        Assert.AreEqual(RolFilaReporte.Concepto, FormProgramaObra.RolFilaFormato(false));
    }

    [TestMethod]
    public void RolFilaFormatoInsumos_MapeaAgrupadorYActividad()
    {
        Assert.AreEqual(RolFilaReporte.Resumen, FormProgramaInsumos.RolFilaFormato(true));
        Assert.AreEqual(RolFilaReporte.Concepto, FormProgramaInsumos.RolFilaFormato(false));
    }
}
