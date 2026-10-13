using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Paso 4.3 — el GRID de Presupuesto pinta las filas agrupador con los DEFAULTS
/// NEUTRALES por rol (<see cref="EstilosFilaPredeterminados"/>) combinados con el
/// override PERSISTIDO del usuario. Precedencia: override de celda &gt; rol del
/// usuario &gt; default del rol. Sin configuración, el color es idéntico al
/// histórico (Capítulo #800080, Subcapítulo #ADD8E6, Niveles #D3D3D3).
/// </summary>
[TestClass]
public class EstilosFilaPresupuestoGridTests
{
    private static readonly Font FuenteBase = new("Segoe UI", 9f);

    private static DataGridViewCellStyle Estilo(string tipo, IReadOnlyDictionary<string, ReportStyleOverlay>? usuario = null)
    {
        var estilo = new DataGridViewCellStyle();
        FormPresupuesto.AplicarEstiloRolPresupuesto(estilo, FuenteBase, tipo, usuario);
        return estilo;
    }

    private static IReadOnlyDictionary<string, ReportStyleOverlay> Usuario(string rol, ReportStyleOverlay overlay)
        => new Dictionary<string, ReportStyleOverlay>(System.StringComparer.OrdinalIgnoreCase) { [rol] = overlay };

    // ─────────────────────────── Defaults (sin configuración) ───────────────────────────

    [TestMethod]
    public void Capitulo_SinConfig_UsaElDefaultDelRol()
    {
        var estilo = Estilo("Capitulo");

        Assert.AreEqual(Color.Purple.ToArgb(), estilo.BackColor.ToArgb());
        Assert.AreEqual(Color.White.ToArgb(), estilo.ForeColor.ToArgb());
        Assert.IsTrue(estilo.Font!.Bold);
    }

    [TestMethod]
    public void Subcapitulo_SinConfig_UsaElDefaultDelRol()
    {
        var estilo = Estilo("Subcapitulo");

        Assert.AreEqual(ColorTranslator.FromHtml("#ADD8E6").ToArgb(), estilo.BackColor.ToArgb());
        Assert.AreEqual(Color.Black.ToArgb(), estilo.ForeColor.ToArgb());
        Assert.IsTrue(estilo.Font!.Bold);
    }

    [TestMethod]
    public void Niveles_SinConfig_UsanElDefaultGris()
    {
        foreach (var tipo in new[] { "Nivel 1", "Nivel 2", "Nivel 3" })
        {
            var estilo = Estilo(tipo);
            Assert.AreEqual(ColorTranslator.FromHtml("#D3D3D3").ToArgb(), estilo.BackColor.ToArgb(), tipo);
            Assert.IsTrue(estilo.Font!.Bold, tipo);
        }
    }

    [TestMethod]
    public void Concepto_SinConfig_NoTieneColorDeRol()
    {
        var estilo = Estilo("Concepto");

        Assert.AreEqual(Color.White.ToArgb(), estilo.BackColor.ToArgb());
        Assert.AreEqual(Color.Black.ToArgb(), estilo.ForeColor.ToArgb());
        Assert.IsFalse(estilo.Font!.Bold);
    }

    // ─────────────────────────── Override del usuario gana al default ───────────────────────────

    [TestMethod]
    public void Capitulo_ConConfigDelUsuario_GanaElUsuarioYParcialHeredaDefault()
    {
        var estilo = Estilo("Capitulo", Usuario(RolFilaReporte.Capitulo, new ReportStyleOverlay(ColorFondo: "#010203")));

        Assert.AreEqual(Color.FromArgb(0x01, 0x02, 0x03).ToArgb(), estilo.BackColor.ToArgb(), "El fondo del usuario gana al default.");
        Assert.AreEqual(Color.White.ToArgb(), estilo.ForeColor.ToArgb(), "El color de texto hereda del default.");
        Assert.IsTrue(estilo.Font!.Bold, "La negrita hereda del default.");
    }

    [TestMethod]
    public void Capitulo_ConConfigDelUsuarioSinNegrita_RespetaAlUsuario()
    {
        var estilo = Estilo("Capitulo", Usuario(RolFilaReporte.Capitulo, new ReportStyleOverlay(Negrita: false)));

        Assert.IsFalse(estilo.Font!.Bold, "El usuario puede quitar la negrita del rol.");
        Assert.AreEqual(Color.Purple.ToArgb(), estilo.BackColor.ToArgb(), "El fondo sigue siendo el default.");
    }

    // ─────────────────────────── Override de celda gana a todo ───────────────────────────

    [TestMethod]
    public void OverrideDeCelda_GanaSobreElRol()
    {
        var estilo = Estilo("Capitulo");
        FormPresupuesto.AplicarOverlayACellStyle(estilo, new ReportStyleOverlay(ColorFondo: "#ABCDEF"));

        Assert.AreEqual(Color.FromArgb(0xAB, 0xCD, 0xEF).ToArgb(), estilo.BackColor.ToArgb(),
            "El override de celda del paso 4.1 gana sobre el default del rol.");
    }

    // ─────────────────────────── Mapeo tipo → rol ───────────────────────────

    [TestMethod]
    public void ObtenerRolFilaFormato_MapeaLosTiposDelGrid()
    {
        Assert.AreEqual(RolFilaReporte.Capitulo, FormPresupuesto.ObtenerRolFilaFormato("Capitulo"));
        Assert.AreEqual(RolFilaReporte.Subcapitulo, FormPresupuesto.ObtenerRolFilaFormato("Subcapitulo"));
        Assert.AreEqual(RolFilaReporte.Nivel1, FormPresupuesto.ObtenerRolFilaFormato("Nivel 1"));
        Assert.AreEqual(RolFilaReporte.Nivel2, FormPresupuesto.ObtenerRolFilaFormato("Nivel 2"));
        Assert.AreEqual(RolFilaReporte.Nivel3, FormPresupuesto.ObtenerRolFilaFormato("Nivel 3"));
        Assert.AreEqual(RolFilaReporte.Concepto, FormPresupuesto.ObtenerRolFilaFormato("Concepto"));
        Assert.IsNull(FormPresupuesto.ObtenerRolFilaFormato("Otro"));
        Assert.IsNull(FormPresupuesto.ObtenerRolFilaFormato(null));
    }

    [TestMethod]
    public void RolDeGrid_CoincideConElDeReportes_PorNivel()
    {
        // El rol que infiere PDF/Excel (PresupuestoReportSnapshotBuilder.RolFila)
        // para cada nivel coincide con el que mapea el grid para su tipo.
        Assert.AreEqual(
            Application.UseCases.Reporting.PresupuestoReportSnapshotBuilder.RolFila(
                new Core.Entities.ConceptoPresupuesto { EsAgrupador = true, Nivel = 0 }),
            FormPresupuesto.ObtenerRolFilaFormato("Capitulo"));
        Assert.AreEqual(
            Application.UseCases.Reporting.PresupuestoReportSnapshotBuilder.RolFila(
                new Core.Entities.ConceptoPresupuesto { EsAgrupador = true, Nivel = 1 }),
            FormPresupuesto.ObtenerRolFilaFormato("Subcapitulo"));
        Assert.AreEqual(
            Application.UseCases.Reporting.PresupuestoReportSnapshotBuilder.RolFila(
                new Core.Entities.ConceptoPresupuesto { EsAgrupador = true, Nivel = 2 }),
            FormPresupuesto.ObtenerRolFilaFormato("Nivel 1"));
    }
}
