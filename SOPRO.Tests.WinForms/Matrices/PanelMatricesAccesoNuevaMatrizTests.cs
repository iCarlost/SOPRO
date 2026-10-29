using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Matrices;

/// <summary>
/// UI del panel embebido de matrices en el workspace del presupuesto:
/// <list type="bullet">
/// <item>un concepto sin APU muestra la acción "Nueva matriz" visible, habilitada y sin
/// recortarse contra el header; el clic notifica al host;</item>
/// <item>el host abre la captura (vista crea) y Volver/Cancelar repone el estado
/// "Concepto sin APU asignada" con el acceso de nuevo visible;</item>
/// <item>cuando el concepto SÍ tiene matriz (o el panel entra en edición de cabecera),
/// el acceso queda oculto;</item>
/// <item>el selector APU embebido mantiene los tres comandos (Nueva Matriz / Editar /
/// Volver) sobre la barra, sin colapsar al overflow.</item>
/// </list>
/// Sólo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class PanelMatricesAccesoNuevaMatrizTests
{
    [TestMethod]
    public void ConceptoSinMatriz_MuestraAccesoVisibleHabilitadoYNoRecortado()
    {
        var previo = LeerProyectoGlobalFormato();
        using var root = new DirectorioTemporal("acceso_nueva_matriz");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx, conMatriz: false);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root.Ruta, "sopro"), Path.Combine(root.Ruta, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.IsTrue(fila >= 0, "El concepto debe existir como fila del presupuesto.");
            panel.NotificarFilaCambiada(fila);

            Assert.AreEqual("Concepto sin APU asignada", panel.TituloCabeceraParaPrueba,
                "La vista de matrices debe explicar por qué está vacía.");
            Assert.IsTrue(panel.AccesoNuevaMatrizVisible,
                "El concepto sin APU debe exponer la acción Nueva matriz.");

            var btn = panel.AccesoNuevaMatrizParaPrueba;
            Assert.IsNotNull(btn);
            Assert.IsTrue(btn.Enabled, "La acción Nueva matriz debe estar habilitada.");

            // No recortado ni encimado: el botón queda completo dentro de la fila de
            // navegación (coordenadas del contenedor, estables aunque el ancho del
            // workspace no se haya asentado aún) y su orden de flujo es correcto.
            var contenedor = btn.Parent!;
            var area = btn.Bounds;
            Assert.IsTrue(contenedor.ClientSize.Width >= area.X + area.Width,
                "La acción primaria debe caber completa en la fila de navegación.");
            var botonesNav = contenedor.Controls.OfType<Button>().Where(b => b != btn).ToList();
            Assert.IsTrue(botonesNav.Count > 0, "La navegación debe seguir en el header.");
            foreach (var otro in botonesNav)
            {
                Assert.IsFalse(area.IntersectsWith(otro.Bounds),
                    "La acción primaria no debe quedar parcialmente cubierta por la navegación.");
                Assert.IsTrue(otro.Bounds.X >= area.X + area.Width,
                    "La navegación debe quedar a la derecha de la acción primaria.");
            }

            // Jerarquía: la acción primaria no es más pequeña que la navegación.
            Assert.IsTrue(btn.Height >= botonesNav.Max(b => b.Height),
                "La acción primaria debe pesar visualmente igual o más que la navegación.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    [TestMethod]
    public void ClicAccesoAbreCreacionYVolverReponeEstadoConceptoSinMatriz()
    {
        var previo = LeerProyectoGlobalFormato();
        using var root = new DirectorioTemporal("acceso_creacion_volver");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx, conMatriz: false);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root.Ruta, "sopro"), Path.Combine(root.Ruta, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Concepto.Id));
            var btn = panel.AccesoNuevaMatrizParaPrueba;
            Assert.IsNotNull(btn);

            bool notificado = false;
            panel.SolicitudNuevaMatriz += (_, __) => notificado = true;
            btn.PerformClick();
            Assert.IsTrue(notificado, "El clic debe notificar SolicitudNuevaMatriz al host.");
            Assert.AreEqual("Nueva APU", panel.TituloCabeceraParaPrueba,
                "El host debe haber abierto la captura de matriz.");
            Assert.IsTrue(panel.CabeceraDatosVisiblesParaPrueba,
                "Los campos de captura deben quedar visibles.");
            Assert.IsFalse(panel.AccesoNuevaMatrizVisible,
                "Durante la creación el acceso no debe duplicarse en el header.");

            // Volver: Cancelar repone el estado del concepto sin APU, acceso visible.
            panel.CancelarCabeceraParaPrueba();
            Assert.AreEqual("Concepto sin APU asignada", panel.TituloCabeceraParaPrueba);
            Assert.IsFalse(panel.CabeceraDatosVisiblesParaPrueba);
            Assert.IsTrue(panel.AccesoNuevaMatrizVisible,
                "Tras Volver, la acción Nueva matriz debe seguir disponible.");

            // Volver no crea ni asigna nada: la fila conserva MatrizId nulo.
            using var verificacion = new SOPROContext(root.DbPath);
            var conceptoDb = verificacion.ConceptosPresupuesto
                .AsNoTracking()
                .Single(c => c.Id == escenario.Concepto.Id);
            Assert.IsNull(conceptoDb.MatrizId, "Volver no debe persistir ninguna matriz.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    [TestMethod]
    public void ConceptoConMatriz_YEstadoDeEdicion_OcultanAccesoNuevaMatriz()
    {
        var previo = LeerProyectoGlobalFormato();
        using var root = new DirectorioTemporal("acceso_oculto");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var escenario = CrearEscenario(ctx, conMatriz: true);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root.Ruta, "sopro"), Path.Combine(root.Ruta, "local")));
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Concepto.Id));
            Assert.IsFalse(panel.AccesoNuevaMatrizVisible,
                "Con matriz cargada no hay motivo para ofrecer la creación.");

            // Estado de creación programática: el acceso se mantiene oculto aunque la
            // cabecera de captura quede disponible.
            panel.BeginCreateMatrix(TipoMatriz.Basico, onSaved: null, onCancelled: null);
            Assert.IsFalse(panel.AccesoNuevaMatrizVisible);
            Assert.IsNull(panel.MatrizVisibleEnPanel);

            // Y Volver (sin matriz previa) cae al estado genérico, sin acceso.
            panel.CancelarCabeceraParaPrueba();
            Assert.IsFalse(panel.AccesoNuevaMatrizVisible);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
        }
    }

    [TestMethod]
    public void SelectorApuEmbebido_TresComandosSobreLaBarra_SinOverflow()
    {
        using var selector = new SelectorApuEmbebidoControl();

        Assert.AreEqual(ToolStripItemOverflow.Never, selector.BtnNuevaMatrizItem.Overflow,
            "Nueva Matriz debe verse directamente en la barra, no colapsada al overflow.");
        Assert.AreEqual(ToolStripItemOverflow.Never, selector.BtnEditarItem.Overflow,
            "Editar debe verse directamente en la barra, no colapsado al overflow.");
        Assert.AreEqual(ToolStripItemOverflow.Never, selector.BtnVolverItem.Overflow,
            "Volver debe verse directamente en la barra, no colapsado al overflow.");

        Assert.IsTrue(selector.BtnNuevaMatrizItem.Font.Bold,
            "La acción primaria Nueva Matriz se distingue con negrita.");
        Assert.IsFalse(selector.BtnEditarItem.Enabled,
            "Editar permanece bloqueado sin selección de matriz.");
        Assert.IsTrue(selector.BtnVolverItem.Enabled, "Volver siempre disponible.");
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private sealed record EscenarioAcceso(Proyecto Proyecto, ConceptoPresupuesto Concepto, Matriz? MatrizApu);

    private static EscenarioAcceso CrearEscenario(SOPROContext ctx, bool conMatriz)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto acceso nueva matriz",
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

        Matriz? matriz = null;
        if (conMatriz)
        {
            matriz = new Matriz
            {
                ProyectoId = proyecto.Id,
                Clave = "APU-001",
                Descripcion = "Concreto simple",
                Unidad = "m3",
                Tipo = TipoMatriz.APU,
                Notas = string.Empty,
                CostoDirecto = 0m,
            };
            ctx.Matrices.Add(matriz);
            ctx.SaveChanges();
        }

        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-001",
            Descripcion = "Concreto predosificado",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 1,
            Unidad = "m3",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 10m,
            MatrizId = matriz?.Id,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.ConceptosPresupuesto.Add(concepto);
        ctx.SaveChanges();

        return new EscenarioAcceso(proyecto, concepto, matriz);
    }

    private static int BuscarFilaPorConcepto(DataGridView grid, int conceptoId)
    {
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            if (grid.Rows[i].Tag is ConceptoPresupuesto concepto && concepto.Id == conceptoId)
                return i;
        }

        return -1;
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)CampoProyectoGlobal().GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => CampoProyectoGlobal().SetValue(null, previo);

    private static System.Reflection.FieldInfo CampoProyectoGlobal()
        => typeof(FormatoHelper).GetField("_proyectoActual",
               System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
           ?? throw new InvalidOperationException(
               "No se encontró el campo FormatoHelper._proyectoActual.");
}

internal sealed class DirectorioTemporal : IDisposable
{
    public string Ruta { get; }
    public string DbPath { get; }

    public DirectorioTemporal(string nombre)
    {
        Ruta = Path.Combine(Path.GetTempPath(), $"sopro_{nombre}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Ruta);
        DbPath = Path.Combine(Ruta, "proyecto-temporal.soproproj");
    }

    public void Dispose()
    {
        for (int intento = 0; intento < 5; intento++)
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(Ruta))
                    Directory.Delete(Ruta, recursive: true);
                return;
            }
            catch (IOException) when (intento < 4)
            {
                System.Threading.Thread.Sleep(200);
            }
        }
    }
}
