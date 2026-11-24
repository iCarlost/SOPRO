using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Inicio;

/// <summary>
/// Contrato visual del header del selector de proyectos (FormPrincipal): la
/// banda oscura panelTop queda al ras de la ventana arriba y redondea SOLO
/// sus dos esquinas inferiores, recalculando la Región ante cambios de
/// tamaño/alto dinámico del header, sin tocar la tarjeta de recientes ni el
/// grid de proyectos. Verificable a 96 DPI con puntos de muestreo.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class FormPrincipalHeaderRedondeadoTests
{
    [TestMethod]
    public void Header_SoloEsquinasInferioresRedondeadas_YAlRasArriba()
    {
        using EscenarioInicio escenario = new();
        Panel panelTop = escenario.PanelTop;

        Assert.IsNotNull(panelTop.Region, "panelTop debe tener Región de esquinas inferiores redondeadas.");

        int ancho = panelTop.Width;
        int alto = panelTop.Height;
        Assert.IsTrue(ancho > 100 && alto >= 88,
            $"Precondición: header con tamaño real ({panelTop.Size}).");

        // Esquinas superiores CUADRADAS y al ras de la ventana.
        Assert.IsTrue(panelTop.Region!.IsVisible(1, 1),
            "La esquina superior izquierda debe seguir visible (cuadrada, al ras).");
        Assert.IsTrue(panelTop.Region.IsVisible(ancho - 2, 1),
            "La esquina superior derecha debe seguir visible (cuadrada, al ras).");

        // Esquinas inferiores REDONDEADAS: los puntos pegados a la esquina
        // quedan fuera del arco del radio del tema (8 a 96 DPI).
        Assert.IsFalse(panelTop.Region.IsVisible(1, alto - 1),
            "La esquina inferior izquierda debe quedar recortada por el arco.");
        Assert.IsFalse(panelTop.Region.IsVisible(ancho - 2, alto - 1),
            "La esquina inferior derecha debe quedar recortada por el arco.");

        // Centro, borde inferior medio (separador) y flancos siguen visibles.
        Assert.IsTrue(panelTop.Region.IsVisible(ancho / 2, alto / 2),
            "El centro del header debe seguir visible bajo la Región.");
        Assert.IsTrue(panelTop.Region.IsVisible(ancho / 2, alto - 1),
            "El separador inferior debe seguir visible en su tramo recto.");
        Assert.IsTrue(panelTop.Region.IsVisible(1, alto / 2),
            "El flanco izquierdo debe seguir visible bajo la Región.");
        Assert.IsTrue(panelTop.Region.IsVisible(ancho - 2, alto / 2),
            "El flanco derecho debe seguir visible bajo la Región.");
    }

    [TestMethod]
    public void Header_RegionSeRecalcula_ConAltoDinamicoYResize()
    {
        using EscenarioInicio escenario = new();
        Panel panelTop = escenario.PanelTop;
        Form form = escenario.Form;

        // El header crece dinámicamente (fallback de botones / DPI): la Región
        // debe recalcularse con el nuevo alto en cada SizeChanged.
        int altoOriginal = panelTop.Height;
        panelTop.Height = altoOriginal + 32;
        System.Windows.Forms.Application.DoEvents();

        Assert.IsFalse(panelTop.Region!.IsVisible(1, panelTop.Height - 1),
            "Con el alto dinámico, la esquina inferior izquierda sigue redondeada.");
        Assert.IsTrue(panelTop.Region.IsVisible(1, 1),
            "Con el alto dinámico, la esquina superior sigue cuadrada y al ras.");

        // Cambio de ancho de la ventana: la Región cubre el nuevo borde derecho.
        form.ClientSize = new Size(760, form.ClientSize.Height);
        System.Windows.Forms.Application.DoEvents();
        Assert.IsTrue(panelTop.Region.IsVisible(panelTop.Width - 2, 1),
            "Tras estrechar la ventana, la esquina superior derecha sigue visible.");
        Assert.IsFalse(panelTop.Region.IsVisible(panelTop.Width - 2, panelTop.Height - 1),
            "Tras estrechar la ventana, la esquina inferior derecha sigue redondeada.");

        panelTop.Height = altoOriginal;
    }

    [TestMethod]
    public void SelectorProyectos_GeometriaYTarjetaQuedanIntactas()
    {
        using EscenarioInicio escenario = new();
        DataGridView dgv = escenario.GridRecientes;
        GroupBox grp = escenario.GrupoRecientes;
        Panel panelCenter = escenario.PanelCenter;

        // La tarjeta de recientes y su contenedor NO se redondean (alcance).
        Assert.IsNull(grp.Region, "grpRecientes no debe redondearse en este alcance.");
        Assert.IsNull(panelCenter.Region, "panelCenter no debe redondearse en este alcance.");

        // Geometría/behavior del grid de proyectos intactos.
        Assert.IsFalse(dgv.AllowUserToAddRows);
        Assert.IsFalse(dgv.AllowUserToDeleteRows);
        Assert.IsTrue(dgv.ReadOnly);
        Assert.AreEqual(DataGridViewSelectionMode.FullRowSelect, dgv.SelectionMode);
        Assert.IsFalse(dgv.MultiSelect);
        Assert.AreEqual(BorderStyle.None, dgv.BorderStyle);
        Assert.IsFalse(dgv.RowHeadersVisible);
        Assert.IsTrue(dgv.Dock == DockStyle.Fill, "El grid sigue acoplado a la tarjeta.");

        // Los tres botones de acción del header no participan en este cambio:
        // SoproButton gestiona su propia Región según su layout, no se tocan.
        Assert.AreEqual(typeof(SoproButton), escenario.PanelTop.Controls["btnNuevoProyecto"]!.GetType());
        Assert.AreEqual(typeof(SoproButton), escenario.PanelTop.Controls["btnAbrirProyecto"]!.GetType());
        Assert.AreEqual(typeof(SoproButton), escenario.PanelTop.Controls["btnApoyarKoFi"]!.GetType());
    }

    private sealed class EscenarioInicio : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), $"sopro_inicio_redondeado_{Guid.NewGuid():N}");
        private readonly FormPrincipal _form;

        public EscenarioInicio()
        {
            Directory.CreateDirectory(_root);
            var workspace = new ProjectWorkspaceService(
                Path.Combine(_root, "sopro"), Path.Combine(_root, "local"));
            _form = new FormPrincipal(
                workspace,
                new ProjectLifecycleService(workspace),
                initialProjectPath: null)
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-4000, -4000),
                ShowInTaskbar = false,
            };
            _form.Show();
            System.Windows.Forms.Application.DoEvents();
        }

        public Form Form => _form;
        public Panel PanelTop => _form.panelTop;
        public Panel PanelCenter => _form.panelCenter;
        public GroupBox GrupoRecientes => _form.grpRecientes;
        public DataGridView GridRecientes => _form.dgvRecientes;

        public void Dispose()
        {
            for (int intento = 0; intento < 5; intento++)
            {
                try
                {
                    _form.Dispose();
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    Directory.Delete(_root, recursive: true);
                    return;
                }
                catch (IOException) when (intento < 4)
                {
                    Thread.Sleep(200);
                }
            }
        }
    }
}
