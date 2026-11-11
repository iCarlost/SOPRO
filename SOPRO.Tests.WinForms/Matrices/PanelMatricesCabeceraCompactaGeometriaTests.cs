using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;

namespace SOPRO.Tests.WinForms.Matrices;

/// <summary>
/// GEOMETRÍA del header compacto del panel embebido de matrices (issue #103,
/// Concepto A: badge de tipo integrado + banda única de metadatos).
/// <list type="bullet">
/// <item>El chrome nunca se encima: sin solapes entre regiones visibles.</item>
/// <item>Prioridad dock Abajo: la altura por defecto del workspace deja >= 3 filas
/// completas del grid cuando hay datos.</item>
/// <item>En el piso degradado del workspace (150 px) no hay solapes y el panel ofrece
/// un fallback explícito (desplazamiento vertical) que deja ver el grid.</item>
/// <item>En ancho mínimo (320 px) los campos se re-apilan y la Descripción conserva
/// ancho útil; nunca queda un campo de 1 px.</item>
/// <item>Bajo escala de fuente (proxy de 125 % DPI) el panel reorganiza sin solapes y
/// conserva el fallback de grid.</item>
/// </list>
/// Las aserciones son RELACIONALES (sin igualdad de píxeles frágil).
/// Sólo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class PanelMatricesCabeceraCompactaGeometriaTests
{
    [TestMethod]
    public void DockAbajoPorDefecto_DejaAlMenosTresFilasDeGrid_SinSolapes()
    {
        using var escenario = new EscenarioGeometria();
        var panel = escenario.Panel;
        escenario.Redimensionar(991, 241); // alto por defecto de workspace Abajo a 1366x768
        escenario.CargarMatrizConComponentes();

        AssertVerificarSinSolapes(panel);
        Assert.IsTrue(panel.AlturaGridParaPrueba >= panel.AlturaFilaGridParaPrueba * 3 + panel.AlturaEncabezadoGridParaPrueba,
            $"El dock Abajo por defecto debe mostrar >= 3 filas completas (grid={panel.AlturaGridParaPrueba}px).");
        Assert.IsFalse(panel.ScrollPisoActivoParaPrueba,
            "Con la altura por defecto no debe activarse el fallback de scroll.");
        Assert.IsTrue(panel.AnchoDescripcionParaPrueba >= 150,
            $"La Descripción debe conservar ancho útil ({panel.AnchoDescripcionParaPrueba}px).");
        // Con ancho funcional el desglose de totales cabe: Mat/M.O./Maq./Bas. visibles.
        Assert.IsTrue(panel.DesgloseTotalesVisibleParaPrueba,
            "A ancho completo el desglose de totales permanece visible.");
        Assert.IsTrue(panel.TodoAgregarDentroViewportParaPrueba,
            "Todas las acciones Agregar caben en el viewport.");
    }

    [TestMethod]
    public void DockDerecho478_ReapilaCampos_SinSolapesYDescripcionUtil()
    {
        using var escenario = new EscenarioGeometria();
        var panel = escenario.Panel;
        escenario.Redimensionar(478, 616);
        escenario.CargarMatrizConComponentes();

        AssertVerificarSinSolapes(panel);
        Assert.IsTrue(panel.CamposApiladosParaPrueba,
            "A 478 px la banda de campos se re-apila para conservar la Descripción útil.");
        Assert.IsTrue(panel.AnchoDescripcionParaPrueba >= 150);
        // Fallback de denso estrecho: la banda Agregar envuelve PERO ninguna acción
        // queda fuera del viewport; Totales colapsa al Costo Directo.
        Assert.IsTrue(panel.TodoAgregarDentroViewportParaPrueba,
            "A 478 px todas las acciones Agregar deben caber (envueltas, no recortadas).");
        Assert.IsTrue(panel.CostoDirectoVisibleParaPrueba,
            "El Costo Directo permanece visible aunque el ancho sea estrecho.");
        Assert.IsFalse(panel.DesgloseTotalesVisibleParaPrueba,
            "A 478 px el desglose (Mat/M.O./Maq./Bas.) colapsa al Costo Directo.");
        // Con los anchos mínimos de columna el grid excede el ancho: la barra
        // horizontal REAL del grid debe estar materializada y ACCESIBLE (no basta
        // que un control interno diga Visible): alto > 0, capacidad > visor y
        // alcance real hasta ColEliminar al llevar el desplazamiento al final.
        var grid = panel.GridComponentesParaPrueba;
        var hbar = grid.Controls.OfType<HScrollBar>().FirstOrDefault();
        Assert.IsNotNull(hbar, "El DataGridView debe tener su barra horizontal.");
        Assert.IsTrue(hbar.Visible && hbar.Bounds.Height > 2 && hbar.Bounds.Width > 40,
            $"Barra horizontal materializada (bounds={hbar.Bounds}).");
        Assert.IsTrue(grid.ClientRectangle.Contains(hbar.Bounds),
            $"La barra debe estar DENTRO del viewport del grid (bounds={hbar.Bounds}, cliente={grid.ClientRectangle}).");
        int anchoTotalColumnas = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Sum(c => c.Width);
        Assert.IsTrue(anchoTotalColumnas > grid.ClientSize.Width,
            $"Contenido con desbordamiento horizontal real (columnas={anchoTotalColumnas}px, cliente={grid.ClientSize.Width}px).");
        int offMax = Math.Max(0, anchoTotalColumnas - grid.ClientSize.Width);
        grid.HorizontalScrollingOffset = offMax;
        System.Windows.Forms.Application.DoEvents();
        var rectElim = grid.GetColumnDisplayRectangle(grid.Columns["ColEliminar"].Index, false);
        Assert.IsTrue(grid.ClientRectangle.IntersectsWith(new Rectangle(rectElim.X + 2, rectElim.Y, rectElim.Width - 4, 8)),
            $"ColEliminar debe quedar alcanzable con el scroll al fondo (rect={rectElim}, cliente={grid.ClientRectangle}).");
        Assert.IsFalse(panel.ScrollHorizontalHostVisibleParaPrueba,
            "A 478 px la barra horizontal es la del grid, nunca una del host.");
    }

    [TestMethod]
    public void PisoDegradado150_SinSolapesYConFallbackDeScroll()
    {
        using var escenario = new EscenarioGeometria();
        var panel = escenario.Panel;
        escenario.Redimensionar(991, 119); // panel resultante del piso del workspace (150 - toolbar 31)
        escenario.CargarMatrizConComponentes();

        AssertVerificarSinSolapes(panel);
        Assert.IsTrue(panel.ScrollPisoActivoParaPrueba,
            "En el piso degradado el panel debe ofrecer desplazamiento para alcanzar el grid.");
        Assert.IsTrue(panel.AlturaContenidoRaizParaPrueba > panel.Height,
            "El contenido debe exceder el viewport; el scroll es el fallback explícito.");
        Assert.IsFalse(panel.ScrollHorizontalHostVisibleParaPrueba,
            "El fallback del panel es solo vertical: nunca una barra horizontal del host.");
    }

    [TestMethod]
    public void PisoDegradado478_ConScrollHorizontalDelGrid_NoTapaLaPrimeraFila()
    {
        using var escenario = new EscenarioGeometria();
        var panel = escenario.Panel;
        escenario.Redimensionar(478, 119);
        escenario.CargarMatrizConComponentes();

        AssertVerificarSinSolapes(panel);
        Assert.IsTrue(panel.ScrollPisoActivoParaPrueba,
            "El piso degradado con ancho estrecho mantiene el fallback vertical.");
        Assert.IsFalse(panel.ScrollHorizontalHostVisibleParaPrueba,
            "La barra horizontal del host no debe existir.");

        var grid = panel.GridComponentesParaPrueba;
        var hbar = grid.Controls.OfType<HScrollBar>().FirstOrDefault();
        Assert.IsNotNull(hbar, "El grid estrecho conserva su barra horizontal propia.");
        Assert.IsTrue(hbar.Visible, "La barra horizontal del grid debe estar materializada.");
        Assert.IsTrue(grid.Rows.Count > 0, "El escenario carga una matriz con componentes.");

        var fila0 = grid.GetRowDisplayRectangle(0, false);
        Assert.IsTrue(fila0.Height > 0,
            $"La primera fila debe tener alto visible (fila={fila0}).");
        Assert.IsTrue(fila0.Bottom <= hbar.Bounds.Top,
            $"La primera fila no puede quedar debajo de la barra horizontal del grid (fila={fila0}, barra={hbar.Bounds}).");
    }

    [TestMethod]
    public void AnchoMinimo320_ReapilaCampos_YLaDescripcionConservaAncho()
    {
        using var escenario = new EscenarioGeometria();
        var panel = escenario.Panel;
        escenario.Redimensionar(320, 616);
        escenario.CargarMatrizConComponentes();

        AssertVerificarSinSolapes(panel);
        Assert.IsTrue(panel.CamposApiladosParaPrueba,
            "A 320 px los campos deben re-apilarse a dos líneas.");
        Assert.IsTrue(panel.AnchoDescripcionParaPrueba >= 150,
            $"La Descripción no debe colapsar ({panel.AnchoDescripcionParaPrueba}px).");
        Assert.IsTrue(panel.TodoAgregarDentroViewportParaPrueba,
            "A 320 px todas las acciones Agregar deben caber (envueltas, no recortadas).");
        Assert.IsTrue(panel.CostoDirectoVisibleParaPrueba,
            "El Costo Directo también permanece visible a 320 px.");
        Assert.IsFalse(panel.ScrollHorizontalHostVisibleParaPrueba,
            "A 320 px el host sigue siendo solo vertical.");
    }

    [TestMethod]
    public void EscalaFuente125_SinSolapes_YFallbackIntacto()
    {
        using var escenario = new EscenarioGeometria();
        var panel = escenario.Panel;
        escenario.Redimensionar(991, 241);
        escenario.CargarMatrizConComponentes();

        // Proxy de 125 % DPI: escala geométrica de la sub-árbol (fuente + tamaños fijos).
        panel.Scale(new SizeF(1.25f, 1.25f));
        escenario.AplicarLayout();

        AssertVerificarSinSolapes(panel);
        bool gridOk = panel.AlturaGridParaPrueba >= panel.AlturaFilaGridParaPrueba + panel.AlturaEncabezadoGridParaPrueba;
        Assert.IsTrue(gridOk || panel.ScrollPisoActivoParaPrueba,
            "A escala 125 % se ve al menos una fila o el fallback de scroll queda activo.");
        Assert.IsTrue(panel.AnchoDescripcionParaPrueba >= 100,
            $"La Descripción sigue útil a 125 % ({panel.AnchoDescripcionParaPrueba}px).");
    }

    // ═══════════════════ helpers ═══════════════════

    private static void AssertVerificarSinSolapes(Control raiz)
    {
        // Los solapes geométricos relevantes son ENTRE HERMANOS DIRECTOS (chrome
        // contra grid, filas del chrome entre sí). Un hijo que excede el viewport de
        // su padre envolvente es el fallback de scroll previsto y no es solape.
        foreach (Control padre in Recorrer(raiz))
        {
            var hermanos = new System.Collections.Generic.List<Control>();
            foreach (Control hijo in padre.Controls)
                if (hijo.Visible) hermanos.Add(hijo);
            for (int i = 0; i < hermanos.Count; i++)
            {
                for (int j = i + 1; j < hermanos.Count; j++)
                {
                    var ra = hermanos[i].Bounds;
                    var rb = hermanos[j].Bounds;
                    // Contención legítima (etiqueta dentro de su fila, etc.) no es solape.
                    bool contencion = ra.Contains(rb) || rb.Contains(ra);
                    Assert.IsFalse(!contencion && ra.IntersectsWith(rb),
                        $"Solape entre hermanos: {hermanos[i].GetType().Name}{ra} contra {hermanos[j].GetType().Name}{rb} (padre {padre.GetType().Name}).");
                }
            }
        }
    }

    private static System.Collections.Generic.IEnumerable<Control> Recorrer(Control raiz)
    {
        yield return raiz;
        foreach (Control hijo in raiz.Controls)
        {
            foreach (var nieto in Recorrer(hijo))
                yield return nieto;
        }
    }

    private sealed class EscenarioGeometria : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            $"sopro_panel_geom_{Guid.NewGuid():N}");
        private readonly SOPROContext _ctx;
        private readonly PanelMatricesEmbebido _panel;
        private int _proyectoId;
        private int _matrizId;

        private Form _host = null!;

        public PanelMatricesEmbebido Panel => _panel;

        public EscenarioGeometria()
        {
            Directory.CreateDirectory(_root);
            _ctx = new SOPROContext(Path.Combine(_root, "proyecto-temporal.soproproj"));
            _ctx.Database.EnsureCreated();

            var proyecto = new Proyecto
            {
                Nombre = "Proyecto geometría",
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
            _ctx.Proyectos.Add(proyecto);
            _ctx.SaveChanges();
            _proyectoId = proyecto.Id;

            var material = new Material
            {
                ProyectoId = proyecto.Id,
                Clave = "MAT-GEO",
                Descripcion = "Insumo de prueba para geometría del panel",
                Unidad = "kg",
                PrecioUnitario = 60m,
                Notas = string.Empty,
            };
            _ctx.Materiales.Add(material);
            _ctx.SaveChanges();

            var matriz = new Matriz
            {
                ProyectoId = proyecto.Id,
                Clave = "APU-GEO-001",
                Descripcion = "Muro de tabique rojo recocido 14 cm con asiento de mortero",
                Unidad = "m2",
                Tipo = TipoMatriz.APU,
                CostoDirecto = 120m,
                Notas = string.Empty,
                FechaModificacion = new DateTime(2026, 1, 1),
            };
            _ctx.Matrices.Add(matriz);
            _ctx.SaveChanges();
            _matrizId = matriz.Id;

            for (int i = 1; i <= 8; i++)
            {
                _ctx.ComponentesMatriz.Add(new ComponenteMatriz
                {
                    MatrizId = matriz.Id,
                    TipoComponente = TipoComponenteMatriz.Material,
                    MaterialId = material.Id,
                    Material = material,
                    Cantidad = 1m * i,
                    Importe = 60m * i,
                    Orden = i,
                    Notas = string.Empty,
                });
            }
            _ctx.SaveChanges();

            _panel = new PanelMatricesEmbebido(_ctx, proyecto.Id);
            _host = new Form
            {
                Text = "Escenario geometría",
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-4000, -4000),
                ShowInTaskbar = false,
                ClientSize = new Size(991, 241),
            };
            _host.Controls.Add(_panel);
        }

        public void Redimensionar(int ancho, int alto)
        {
            if (!_host.Visible)
            {
                _host.Show();
                System.Windows.Forms.Application.DoEvents();
            }
            _host.ClientSize = new Size(ancho, alto);
            System.Windows.Forms.Application.DoEvents();
            _panel.PerformLayout();
            System.Windows.Forms.Application.DoEvents();
        }

        public void CargarMatrizConComponentes()
        {
            _panel.BeginEditMatrix(_matrizId);
            System.Windows.Forms.Application.DoEvents();
        }

        public void AplicarLayout()
        {
            Redimensionar(_panel.Width, _panel.Height);
        }

        public void Dispose()
        {
            for (int intento = 0; intento < 5; intento++)
            {
                try
                {
                    _host.Dispose();
                    _panel.Dispose();
                    _ctx.Database.EnsureDeleted();
                    _ctx.Dispose();
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    Directory.Delete(_root, recursive: true);
                    return;
                }
                catch (IOException) when (intento < 4)
                {
                    Thread.Sleep(200);
                }
                catch (UnauthorizedAccessException) when (intento < 4)
                {
                    Thread.Sleep(200);
                }
            }
        }
    }
}
