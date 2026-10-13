using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Paso 4.1: formato de celdas por SELECCIÓN en el grid de Presupuesto.
/// Verifica el mapeo selección → direcciones ESTABLES (C:{Id} para conceptos,
/// SUB:{Id} para agrupadores), el filtrado de filas sin entidad / columnas no
/// exportables, la persistencia
/// con <see cref="FormatoReporteService"/> y el undo/redo como UNA sola acción
/// compuesta. Se usan grids fake / controles no mostrados para no depender de UI
/// real, igual que las pruebas del coordinador (paso 3.1).
/// </summary>
[TestClass]
public class FormatoCeldasPresupuestoTests
{
    private const string Tipo = "Presupuesto";
    private const string Vista = ""; // vista por defecto / comodín de ReportCellAddress

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static SOPROContext CrearContexto()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_fmt_pres_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static int AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto formato",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto.Id;
    }

    private static DataGridView CrearGrid(params string[] columnasFormateables)
    {
        var grid = new DataGridView { AllowUserToAddRows = false };
        foreach (var nombre in columnasFormateables)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = nombre,
                Tag = new ColumnaPersonalizada { Nombre = nombre, NombreInterno = nombre },
            });
        }
        return grid;
    }

    /// <summary>
    /// Grid con la columna sintética de numeración "#" (<c>colNumero</c>, SIN
    /// <see cref="ColumnaPersonalizada"/>) más columnas personalizadas.
    /// </summary>
    private static DataGridView CrearGridConNumero(params string[] columnasFormateables)
    {
        var grid = new DataGridView { AllowUserToAddRows = false };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNumero", HeaderText = "#", Width = 40 });
        foreach (var nombre in columnasFormateables)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = nombre,
                Tag = new ColumnaPersonalizada { Nombre = nombre, NombreInterno = nombre },
            });
        }
        return grid;
    }

    private static int AgregarFila(DataGridView grid, ConceptoPresupuesto? concepto)
    {
        var index = grid.Rows.Add();
        grid.Rows[index].Tag = concepto;
        return index;
    }

    private static ConceptoPresupuesto Concepto(int id, bool agrupador = false, string? clave = null)
        => new() { Id = id, EsAgrupador = agrupador, Clave = clave ?? string.Empty };

    // ═════════════════════════ Selección → direcciones ═════════════════════════

    [TestMethod]
    public void UnaCelda_GeneraDireccionEstableConClaveDeConcepto()
    {
        using var grid = CrearGrid("Importe");
        var fila = AgregarFila(grid, Concepto(7));

        var direcciones = FormPresupuesto.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[fila].Cells[0] });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("C:7", direcciones[0].ClaveFila);
        Assert.AreEqual("Importe", direcciones[0].NombreColumna);
        Assert.AreEqual(Vista, direcciones[0].ClaveVista);
    }

    [TestMethod]
    public void RangoContiguoShift_GeneraUnaDireccionPorFila()
    {
        using var grid = CrearGrid("Importe");
        var i0 = AgregarFila(grid, Concepto(1));
        var i1 = AgregarFila(grid, Concepto(2));
        var i2 = AgregarFila(grid, Concepto(3));

        var direcciones = FormPresupuesto.ConstruirDireccionesSeleccionadas(grid, new[]
        {
            grid.Rows[i0].Cells[0],
            grid.Rows[i1].Cells[0],
            grid.Rows[i2].Cells[0],
        });

        CollectionAssert.AreEqual(
            new[] { "C:1", "C:2", "C:3" },
            direcciones.Select(d => d.ClaveFila).ToArray());
    }

    [TestMethod]
    public void MultiRangoCtrl_DeduplicaPorDireccionEstable()
    {
        using var grid = CrearGrid("Importe");
        var i0 = AgregarFila(grid, Concepto(1));
        var i1 = AgregarFila(grid, Concepto(2));
        var i2 = AgregarFila(grid, Concepto(3));

        // Rango discontinuo con direcciones repetidas (como Ctrl + clic).
        var fake = new GridSeleccionFake(grid, new[]
        {
            grid.Rows[i0].Cells[0],
            grid.Rows[i2].Cells[0],
            grid.Rows[i0].Cells[0],
            grid.Rows[i1].Cells[0],
        });
        var coordinador = new FormatoSeleccionCoordinator();
        coordinador.EstablecerGrid(fake);

        var cambios = coordinador.AplicarASeleccion(new ReportStyleOverlay(Negrita: true));

        Assert.AreEqual(DestinoFormatoGrid.Celdas, coordinador.Destino);
        Assert.AreEqual(3, cambios.Count, "Las direcciones repetidas se deduplican.");
        CollectionAssert.AreEqual(
            new[] { "C:1", "C:3", "C:2" },
            cambios.Select(c => c.Direccion.ClaveFila).ToArray());
    }

    [TestMethod]
    public void FilaSinEntidadYColumnaNoExportable_SeIgnoran()
    {
        using var grid = new DataGridView { AllowUserToAddRows = false };
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Importe",
            Tag = new ColumnaPersonalizada { Nombre = "Importe", NombreInterno = "Importe" },
        });
        // Columna auxiliar sin Tag → no exportable / no formateable.
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colId" });

        var conEntidad = AgregarFila(grid, Concepto(42));
        var sinEntidad = AgregarFila(grid, null);

        var direcciones = FormPresupuesto.ConstruirDireccionesSeleccionadas(grid, new[]
        {
            grid.Rows[conEntidad].Cells[0],   // válida
            grid.Rows[conEntidad].Cells[1],   // columna no exportable
            grid.Rows[sinEntidad].Cells[0],   // fila sin entidad
        });

        Assert.AreEqual(1, direcciones.Count);
        Assert.AreEqual("C:42", direcciones[0].ClaveFila);
        Assert.AreEqual("Importe", direcciones[0].NombreColumna);
    }

    // ═══════════ Regresión H1: la columna "#" es formateable ═══════════

    [TestMethod]
    public void ColumnaNumero_SeMapeaAClaveCanonicaYEsFormateable()
    {
        using var grid = CrearGridConNumero("Importe");
        var fila = AgregarFila(grid, Concepto(9));

        var direcciones = FormPresupuesto.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[fila].Cells["colNumero"] });

        Assert.AreEqual(1, direcciones.Count,
            "La celda de la columna '#' debe mapearse a una dirección formateable.");
        Assert.AreEqual("C:9", direcciones[0].ClaveFila);
        Assert.AreEqual("Numero", direcciones[0].NombreColumna,
            "La columna '#' usa la clave canónica 'Numero' (paridad con PDF/Excel).");

        Assert.IsTrue(FormPresupuesto.EsNombreColumnaFormateable(grid, "Numero"),
            "La clave canónica 'Numero' debe ser formateable.");
        Assert.IsFalse(FormPresupuesto.EsNombreColumnaFormateable(grid, "colNumero"),
            "El nombre del control no es la clave de formato.");
    }

    [TestMethod]
    public void ColumnaNumero_SeleccionDeCeldas_EsDestinoCeldasYAplicaOverride()
    {
        using var grid = CrearGridConNumero("Importe");
        var i0 = AgregarFila(grid, Concepto(1));
        var i1 = AgregarFila(grid, Concepto(2));

        var fake = new GridSeleccionFake(grid, new[]
        {
            grid.Rows[i0].Cells["colNumero"],
            grid.Rows[i1].Cells["colNumero"],
        })
        {
            // El fake usa la MISMA lógica de formateabilidad del Presupuesto.
            EsCeldaFormateableOverride = d => FormPresupuesto.EsNombreColumnaFormateable(grid, d.NombreColumna),
        };
        var coordinador = new FormatoSeleccionCoordinator();
        coordinador.EstablecerGrid(fake);

        Assert.AreEqual(DestinoFormatoGrid.Celdas, coordinador.Destino,
            "Seleccionar la columna '#' debe habilitar el formato por celdas (Destino==Celdas).");

        var cambios = coordinador.AplicarASeleccion(new ReportStyleOverlay(Negrita: true, ColorFondo: "#ABCDEF"));

        Assert.AreEqual(2, cambios.Count);
        Assert.IsTrue(cambios.All(c => c.Direccion.NombreColumna == "Numero"),
            "El override de la columna '#' se persiste con la clave canónica 'Numero'.");
    }

    [TestMethod]
    public void Agrupador_UsaClaveUnicaPorId_NoPorClave()
    {
        using var grid = CrearGrid("Importe");
        var fila = AgregarFila(grid, Concepto(5, agrupador: true, clave: "01"));

        var direcciones = FormPresupuesto.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[fila].Cells[0] });

        Assert.AreEqual(1, direcciones.Count);
        // La clave del agrupador es ÚNICA por fila (deriva del Id), no de la Clave
        // visible: así dos agrupadores con la misma Clave NO colisionan.
        Assert.AreEqual("SUB:5", direcciones[0].ClaveFila);
    }

    [TestMethod]
    public void AgrupadoresConMismaClave_GeneranClavesDistintas()
    {
        using var grid = CrearGrid("Tipo");
        var iA = AgregarFila(grid, Concepto(31, agrupador: true, clave: string.Empty));
        var iB = AgregarFila(grid, Concepto(32, agrupador: true, clave: string.Empty));

        var direcciones = FormPresupuesto.ConstruirDireccionesSeleccionadas(grid, new[]
        {
            grid.Rows[iA].Cells[0],
            grid.Rows[iB].Cells[0],
        });

        CollectionAssert.AreEqual(
            new[] { "SUB:31", "SUB:32" },
            direcciones.Select(d => d.ClaveFila).ToArray());
        CollectionAssert.AllItemsAreUnique(direcciones.Select(d => d.ClaveFila).ToArray());
    }

    // ═══════════════════ Persistencia + undo/reproducir pintado ═══════════════════

    [TestMethod]
    public void AplicarFormato_PersisteYSeRecarga()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);
        var overlay = new ReportStyleOverlay(Negrita: true, ColorFondo: "#112233");

        var cambios = new List<CambioFormatoSeleccion>
        {
            new(new ReportCellAddress(Vista, ClaveFilaReporte.Concepto(1), "Importe"), null, overlay),
            new(new ReportCellAddress(Vista, ClaveFilaReporte.Concepto(2), "Importe"), null, overlay),
        };

        service.AplicarCeldas(proyectoId, Tipo, FormPresupuesto.ConstruirLoteCambios(cambios, aplicarAntes: false));

        // Recargar: nuevo servicio sobre el mismo contexto (como reabrir el form).
        var recargado = new FormatoReporteService(ctx).CargarCeldas(proyectoId, Tipo);

        Assert.AreEqual(2, recargado.Count);
        var leido = recargado[new ReportCellAddress(Vista, "C:1", "Importe")];
        Assert.IsTrue(leido.Negrita);
        Assert.AreEqual("#112233", leido.ColorFondo);
    }

    [TestMethod]
    public void PipelineSeleccion_PersisteYPintaAlRecargar()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        using var grid = CrearGrid("Importe");
        var i0 = AgregarFila(grid, Concepto(1));
        var i1 = AgregarFila(grid, Concepto(2));

        var fake = new GridSeleccionFake(grid, new[]
        {
            grid.Rows[i0].Cells[0],
            grid.Rows[i1].Cells[0],
        });
        var coordinador = new FormatoSeleccionCoordinator();
        coordinador.EstablecerGrid(fake);

        var overlay = new ReportStyleOverlay(Negrita: true, ColorFondo: "#0A0B0C");
        var cambios = coordinador.AplicarASeleccion(overlay);

        // Flush que haría FormPresupuesto: persistir el lote producido por el coordinador.
        service.AplicarCeldas(proyectoId, Tipo, FormPresupuesto.ConstruirLoteCambios(cambios, aplicarAntes: false));

        // "Recargar" → diccionario en memoria repoblado y celda re-pintada con precedencia.
        var recargado = new FormatoReporteService(ctx).CargarCeldas(proyectoId, Tipo);
        var direccion = new ReportCellAddress(Vista, "C:1", "Importe");
        Assert.IsTrue(recargado.ContainsKey(direccion));

        var estilo = new DataGridViewCellStyle { BackColor = Color.Purple };
        FormPresupuesto.AplicarOverlayACellStyle(estilo, recargado[direccion]);

        Assert.AreEqual(Color.FromArgb(0x0A, 0x0B, 0x0C), estilo.BackColor);
        Assert.IsTrue(estilo.Font!.Bold);
    }

    [TestMethod]
    public void UndoRedo_FormatoDeRango_EsUnaSolaAccion()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);
        var overlay = new ReportStyleOverlay(ColorFondo: "#FF0000");

        var cambios = Enumerable.Range(1, 10)
            .Select(i => new CambioFormatoSeleccion(
                new ReportCellAddress(Vista, ClaveFilaReporte.Concepto(i), "Importe"), null, overlay))
            .ToList();

        var manager = new UndoManager();
        var accion = new CompositeUndoableAction(
            "Formato de 10 celdas",
            cambios.Select(c => new UndoChange(c.Antes, c.Despues)).ToList(),
            _ => service.AplicarCeldas(proyectoId, Tipo, FormPresupuesto.ConstruirLoteCambios(cambios, aplicarAntes: true)),
            _ => service.AplicarCeldas(proyectoId, Tipo, FormPresupuesto.ConstruirLoteCambios(cambios, aplicarAntes: false)));

        Assert.IsTrue(manager.Execute(accion));
        Assert.AreEqual(1, manager.UndoCount, "El rango de 10 celdas es UNA sola acción.");
        Assert.AreEqual(10, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId));

        Assert.IsTrue(manager.Undo());
        ctx.ChangeTracker.Clear();
        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId),
            "Undo revierte TODO el lote.");

        Assert.IsTrue(manager.Redo());
        ctx.ChangeTracker.Clear();
        Assert.AreEqual(10, ctx.FormatosCeldaReporte.Count(f => f.ProyectoId == proyectoId),
            "Redo reaplica TODO el lote.");
        Assert.AreEqual(1, manager.UndoCount);
        Assert.AreEqual(0, manager.RedoCount);
    }

    [TestMethod]
    public void EstiloVacioEnElLote_EliminaElOverride()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        service.UpsertCelda(proyectoId, Tipo, Vista, "C:1", "Importe",
            new ReportStyleOverlay(ColorFondo: "#123456"));
        Assert.AreEqual(1, ctx.FormatosCeldaReporte.Count());

        var cambios = new[]
        {
            new CambioFormatoSeleccion(new ReportCellAddress(Vista, "C:1", "Importe"), null, new ReportStyleOverlay()),
        };
        service.AplicarCeldas(proyectoId, Tipo, FormPresupuesto.ConstruirLoteCambios(cambios, aplicarAntes: false));
        ctx.ChangeTracker.Clear();

        Assert.AreEqual(0, ctx.FormatosCeldaReporte.Count(), "Un overlay vacío elimina la fila de formato.");
    }

    // ═══════════════════ Precedencia de pintado (override > default) ═══════════════════

    [TestMethod]
    public void AplicarOverlayACellStyle_GanaSobreElEstiloBaseHardcodeado()
    {
        // Color de fila agrupador hardcodeado (p. ej. Capítulo = Purple).
        var estilo = new DataGridViewCellStyle
        {
            BackColor = Color.Purple,
            ForeColor = Color.White,
        };

        FormPresupuesto.AplicarOverlayACellStyle(
            estilo, new ReportStyleOverlay(ColorFondo: "#112233", Negrita: true, Wrap: true));

        Assert.AreEqual(Color.FromArgb(0x11, 0x22, 0x33), estilo.BackColor,
            "El override de celda gana sobre el color hardcodeado del agrupador.");
        Assert.IsTrue(estilo.Font!.Bold);
        Assert.AreEqual(DataGridViewTriState.True, estilo.WrapMode);
    }

    [TestMethod]
    public void AplicarOverlayACellStyle_NoPisaPropiedadesNulas()
    {
        var estilo = new DataGridViewCellStyle
        {
            BackColor = Color.LightBlue,
            ForeColor = Color.Black,
        };

        FormPresupuesto.AplicarOverlayACellStyle(estilo, new ReportStyleOverlay(Negrita: true));

        Assert.AreEqual(Color.LightBlue, estilo.BackColor, "Una propiedad null debe heredar (no pisar).");
        Assert.AreEqual(Color.Black, estilo.ForeColor);
        Assert.IsTrue(estilo.Font!.Bold);
    }

    // ═══════════════ Regresión: ediciones de celda independientes (Bug A) ═══════════

    /// <summary>
    /// BUG A: aplicar el fondo a una celda y luego cambiar SÓLO la alineación de
    /// otra NO debe arrastrar el fondo (retenido en el ribbon) ni contaminar la
    /// primera celda. Se comprueba con el delta por propiedad + la fusión del
    /// coordinador, que es lo que hace el ribbon real.
    /// </summary>
    [TestMethod]
    public void EditarAlineacion_NoArrastraNiPisaElFondoDeOtraCelda()
    {
        using var grid = CrearGrid("Importe");
        var iX = AgregarFila(grid, Concepto(1));
        var iY = AgregarFila(grid, Concepto(2));
        var dirX = new ReportCellAddress(Vista, "C:1", "Importe");
        var dirY = new ReportCellAddress(Vista, "C:2", "Importe");

        // Estado del ribbon: primero fondo amarillo; después el usuario sólo cambia
        // la alineación, pero el color de fondo queda RETENIDO en el ribbon.
        var fmtFondo = new ColumnaPersonalizada
        {
            ColorFondo = "#FFFF00",
            ColorFuente = "#000000",
            Alineacion = AlineacionColumna.Izquierda,
        };
        var fmtAlineacion = new ColumnaPersonalizada
        {
            ColorFondo = "#FFFF00",
            ColorFuente = "#000000",
            Alineacion = AlineacionColumna.Centro,
        };

        var deltaFondo = FormProyecto.RestringirAPropiedad(
            FormatoSeleccionCoordinator.ConstruirOverlay(fmtFondo),
            FormProyecto.PropiedadFormatoRibbon.ColorFondo);
        var deltaAlineacion = FormProyecto.RestringirAPropiedad(
            FormatoSeleccionCoordinator.ConstruirOverlay(fmtAlineacion),
            FormProyecto.PropiedadFormatoRibbon.AlineacionHorizontal);

        Assert.IsNull(deltaAlineacion.ColorFondo,
            "El delta de alineación NO debe arrastrar el color de fondo retenido en el ribbon.");

        var fakeX = new GridSeleccionFake(grid, new[] { grid.Rows[iX].Cells[0] });
        var coordX = new FormatoSeleccionCoordinator();
        coordX.EstablecerGrid(fakeX);
        coordX.AplicarASeleccion(deltaFondo);

        var fakeY = new GridSeleccionFake(grid, new[] { grid.Rows[iY].Cells[0] });
        var coordY = new FormatoSeleccionCoordinator();
        coordY.EstablecerGrid(fakeY);
        coordY.AplicarASeleccion(deltaAlineacion);

        var overlayX = fakeX.ObtenerOverlayActual(dirX);
        var overlayY = fakeY.ObtenerOverlayActual(dirY);

        Assert.IsNotNull(overlayX);
        Assert.AreEqual("#FFFF00", overlayX!.ColorFondo, "La celda X conserva su fondo.");
        Assert.IsNull(overlayX.AlineacionHorizontal,
            "La edición de Y NO contamina la alineación de X.");

        Assert.IsNotNull(overlayY);
        Assert.AreEqual(ReportTextAlignment.Centro, overlayY!.AlineacionHorizontal,
            "La celda Y tiene su alineación.");
        Assert.IsNull(overlayY.ColorFondo,
            "La alineación de Y NO arrastra el fondo de X.");
    }

    [TestMethod]
    public void SegundoDelta_FusionaSinPerderLoYaAplicadoALaMismaCelda()
    {
        using var grid = CrearGrid("Importe");
        var iX = AgregarFila(grid, Concepto(1));
        var dirX = new ReportCellAddress(Vista, "C:1", "Importe");

        var fake = new GridSeleccionFake(grid, new[] { grid.Rows[iX].Cells[0] });
        var coord = new FormatoSeleccionCoordinator();
        coord.EstablecerGrid(fake);

        coord.AplicarASeleccion(new ReportStyleOverlay(ColorFondo: "#FFFF00"));
        coord.AplicarASeleccion(new ReportStyleOverlay(AlineacionHorizontal: ReportTextAlignment.Centro));

        var overlay = fake.ObtenerOverlayActual(dirX);
        Assert.IsNotNull(overlay);
        Assert.AreEqual("#FFFF00", overlay!.ColorFondo, "El fondo aplicado antes se conserva.");
        Assert.AreEqual(ReportTextAlignment.Centro, overlay.AlineacionHorizontal,
            "La alineación nueva gana y convive con el fondo previo.");
    }

    [TestMethod]
    public void PersistenciaYRecarga_EdicionesPorCeldaQuedanSinContaminarse()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        var dirX = new ReportCellAddress(Vista, ClaveFilaReporte.Concepto(1), "Importe");
        var dirY = new ReportCellAddress(Vista, ClaveFilaReporte.Concepto(2), "Importe");

        service.AplicarCeldas(proyectoId, Tipo, FormPresupuesto.ConstruirLoteCambios(
            new[] { new CambioFormatoSeleccion(dirX, null, new ReportStyleOverlay(ColorFondo: "#FFFF00")) },
            aplicarAntes: false));
        service.AplicarCeldas(proyectoId, Tipo, FormPresupuesto.ConstruirLoteCambios(
            new[] { new CambioFormatoSeleccion(dirY, null, new ReportStyleOverlay(AlineacionHorizontal: ReportTextAlignment.Centro)) },
            aplicarAntes: false));

        var recargado = new FormatoReporteService(ctx).CargarCeldas(proyectoId, Tipo);

        Assert.AreEqual("#FFFF00", recargado[dirX].ColorFondo, "X conserva su fondo tras recargar.");
        Assert.IsNull(recargado[dirX].AlineacionHorizontal, "X no recibió la alineación de Y.");
        Assert.AreEqual(ReportTextAlignment.Centro, recargado[dirY].AlineacionHorizontal, "Y conserva su alineación.");
        Assert.IsNull(recargado[dirY].ColorFondo, "Y no recibió el fondo de X.");
    }

    // ═══════════════ Regresión: agrupadores con clave única (Bug B) ═══════════════

    /// <summary>
    /// BUG B: editar la celda de un agrupador/subcapítulo (Clave vacía o repetida)
    /// NO debe afectar al equivalente de OTRO agrupador. La clave del agrupador
    /// deriva del Id (única por fila), de modo que la persistencia no se pisa.
    /// </summary>
    [TestMethod]
    public void PersistenciaYRecarga_AgrupadoresVecinosQuedanIndependientes()
    {
        using var ctx = CrearContexto();
        var proyectoId = AgregarProyecto(ctx);
        var service = new FormatoReporteService(ctx);

        using var grid = CrearGrid("Tipo");
        var iA = AgregarFila(grid, Concepto(31, agrupador: true, clave: string.Empty));
        var iB = AgregarFila(grid, Concepto(32, agrupador: true, clave: string.Empty));

        var dirA = FormPresupuesto.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[iA].Cells[0] }).Single();
        var dirB = FormPresupuesto.ConstruirDireccionesSeleccionadas(
            grid, new[] { grid.Rows[iB].Cells[0] }).Single();
        Assert.AreNotEqual(dirA, dirB, "Las claves de agrupadores distintos deben ser únicas.");

        service.AplicarCeldas(proyectoId, Tipo, FormPresupuesto.ConstruirLoteCambios(
            new[] { new CambioFormatoSeleccion(dirA, null, new ReportStyleOverlay(ColorFondo: "#FFFF00")) },
            aplicarAntes: false));

        var recargado = new FormatoReporteService(ctx).CargarCeldas(proyectoId, Tipo);

        Assert.IsTrue(recargado.ContainsKey(dirA), "El agrupador editado conserva su override.");
        Assert.IsFalse(recargado.ContainsKey(dirB),
            "Editar la celda de un agrupador NO cambia el equivalente de otro.");
    }

    // ═══════════════════ Estilo EFECTIVO de la selección (P2) ═══════════════════

    private static DataGridViewTextBoxColumn Col(string nombre, AlineacionColumna alineacion = AlineacionColumna.Izquierda)
        => new()
        {
            Name = nombre,
            Tag = new ColumnaPersonalizada { Nombre = nombre, NombreInterno = nombre, Alineacion = alineacion },
        };

    private static DataGridView CrearGridConTipo(params (string nombre, AlineacionColumna alin)[] columnas)
    {
        var grid = new DataGridView { AllowUserToAddRows = false };
        foreach (var (nombre, alin) in columnas)
            grid.Columns.Add(Col(nombre, alin));
        return grid;
    }

    private static int AgregarFilaTipo(DataGridView grid, ConceptoPresupuesto? concepto, string tipo)
    {
        var idx = grid.Rows.Add();
        grid.Rows[idx].Tag = concepto;
        grid.Rows[idx].Cells["Tipo"].Value = tipo;
        return idx;
    }

    [TestMethod]
    public void EstiloEfectivo_ColumnaSeReflejaSinOverride()
    {
        using var grid = CrearGridConTipo(("Tipo", AlineacionColumna.Izquierda), ("Importe", AlineacionColumna.Centro));
        AgregarFilaTipo(grid, Concepto(7), "Concepto");

        var dir = new ReportCellAddress(Vista, "C:7", "Importe");
        var estilo = FormPresupuesto.ResolverEstiloEfectivoPresupuesto(
            grid, new Dictionary<ReportCellAddress, ReportStyleOverlay>(), null, dir);

        Assert.IsNotNull(estilo);
        Assert.AreEqual(ReportTextAlignment.Centro, estilo!.AlineacionHorizontal,
            "Sin override, el ribbon debe reflejar la alineación de la columna.");
    }

    [TestMethod]
    public void EstiloEfectivo_OverrideDeCeldaGanaSobreColumnaYrol()
    {
        using var grid = CrearGridConTipo(("Tipo", AlineacionColumna.Izquierda), ("Importe", AlineacionColumna.Izquierda));
        AgregarFilaTipo(grid, Concepto(7), "Concepto");

        var dir = new ReportCellAddress(Vista, "C:7", "Importe");
        var overlays = new Dictionary<ReportCellAddress, ReportStyleOverlay>
        {
            [dir] = new ReportStyleOverlay(Negrita: true, ColorFondo: "#008000", AlineacionHorizontal: ReportTextAlignment.Centro),
        };

        var estilo = FormPresupuesto.ResolverEstiloEfectivoPresupuesto(grid, overlays, null, dir);

        Assert.IsNotNull(estilo);
        Assert.IsTrue(estilo!.Negrita);
        Assert.AreEqual("#008000", estilo.ColorFondo);
        Assert.AreEqual(ReportTextAlignment.Centro, estilo.AlineacionHorizontal);
    }

    [TestMethod]
    public void EstiloEfectivo_RolDeAgrupadorAportaFondoYNegrita()
    {
        using var grid = CrearGridConTipo(("Tipo", AlineacionColumna.Izquierda), ("Importe", AlineacionColumna.Derecha));
        AgregarFilaTipo(grid, Concepto(5, agrupador: true, clave: "01"), "Capitulo");

        var dir = new ReportCellAddress(Vista, "SUB:5", "Importe");
        var estilo = FormPresupuesto.ResolverEstiloEfectivoPresupuesto(
            grid, new Dictionary<ReportCellAddress, ReportStyleOverlay>(), null, dir);

        Assert.IsNotNull(estilo);
        Assert.AreEqual("#800080", estilo!.ColorFondo, "El rol Capítulo aporta su fondo por defecto.");
        Assert.IsTrue(estilo.Negrita);
    }

    [TestMethod]
    public void EstiloEfectivo_CeldaSinFilaResoluble_NoLanza()
    {
        using var grid = CrearGridConTipo(("Tipo", AlineacionColumna.Izquierda), ("Importe", AlineacionColumna.Izquierda));
        AgregarFilaTipo(grid, Concepto(7), "Concepto");

        var inexistente = new ReportCellAddress(Vista, "C:999", "Importe");
        var estilo = FormPresupuesto.ResolverEstiloEfectivoPresupuesto(
            grid, new Dictionary<ReportCellAddress, ReportStyleOverlay>(), null, inexistente);

        Assert.IsNotNull(estilo, "Sin fila resoluble igualmente resuelve columna/rol vacío.");
    }

    // ═════════════════════════════════ Fakes ═════════════════════════════════

    /// <summary>
    /// Grid de prueba que implementa <see cref="IGridSeleccionFormato"/> sobre el
    /// mapeo REAL de <see cref="FormPresupuesto.ConstruirDireccionesSeleccionadas"/>
    /// y guarda los overlays en memoria, para ejercitar el coordinador y el
    /// pipeline de persistencia sin UI real.
    /// </summary>
    private sealed class GridSeleccionFake : IGridFormato, IGridSeleccionFormato
    {
        private readonly DataGridView _grid;
        private readonly IReadOnlyList<DataGridViewCell> _celdas;
        private readonly Dictionary<ReportCellAddress, ReportStyleOverlay> _overlays = new();

        public GridSeleccionFake(DataGridView grid, IReadOnlyList<DataGridViewCell> celdas)
        {
            _grid = grid;
            _celdas = celdas;
        }

        public bool TieneSeleccionCeldas => _celdas.Count > 0;

        /// <summary>Predicado opcional de formateabilidad; por defecto TODA celda lo es.</summary>
        public Func<ReportCellAddress, bool>? EsCeldaFormateableOverride { get; set; }

        public IReadOnlyList<ReportCellAddress> ObtenerDireccionesSeleccionadas()
            => FormPresupuesto.ConstruirDireccionesSeleccionadas(_grid, _celdas);

        public bool EsCeldaFormateable(ReportCellAddress direccion)
            => EsCeldaFormateableOverride?.Invoke(direccion) ?? true;

        public ReportStyleOverlay? ObtenerOverlayActual(ReportCellAddress direccion)
            => _overlays.TryGetValue(direccion, out var overlay) ? overlay : null;

        public void AplicarOverlayCelda(ReportCellAddress direccion, ReportStyleOverlay overlay)
            => _overlays[direccion] = overlay;

        // ── IGridFormato (stubs: el destino de estas pruebas es CELDAS) ──
        public DataGridView GridPrincipal => _grid;

        public ColumnaPersonalizada ColumnaSeleccionada => null!;

        public void AplicarFormato(ColumnaPersonalizada fmt) { }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt) { }

        public event EventHandler? ColumnaSeleccionadaCambiada { add { } remove { } }

        public bool GenerarReporteExcel() => false;
    }
}
