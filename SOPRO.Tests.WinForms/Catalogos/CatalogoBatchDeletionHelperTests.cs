using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Catalogs;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Catalog;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// Eliminación en lote de los cuatro catálogos de insumos: extracción de Ids
/// desde la selección por celda, etiqueta del comando con el número exacto,
/// confirmación única con impacto y guarda de teclado (Supr/Ctrl+Supr). La misma
/// lógica alimenta el botón de la barra, el menú contextual y el atajo.
/// </summary>
[TestClass]
public class CatalogoBatchDeletionHelperTests
{
    private sealed record FilaPrueba(int Id, string Clave);

    // ═════════════════════ Extracción de la selección ═════════════════════

    [TestMethod]
    public void IdsSeleccionados_CeldasEnDosFilas_DeduplicaOrdenaEIgnoraCeldasRepetidas()
    {
        using var grid = CrearGrid(new[]
        {
            new FilaPrueba(10, "A"),
            new FilaPrueba(20, "B"),
            new FilaPrueba(30, "C"),
        });

        grid.ClearSelection();
        grid.Rows[2].Cells[0].Selected = true;
        grid.Rows[0].Cells[0].Selected = true;
        grid.Rows[0].Cells[0].Selected = true;

        var ids = CatalogoBatchDeletionHelper.IdsSeleccionados<FilaPrueba>(grid, f => f.Id);

        CollectionAssert.AreEqual(new[] { 10, 30 }, ids.ToArray(),
            "La selección por celdas se resuelve a filas distintas, ordenadas y sin repetir.");
    }

    [TestMethod]
    public void IdsSeleccionados_IdNoPositivo_NoParticipaDelLote()
    {
        using var grid = CrearGrid(new[]
        {
            new FilaPrueba(5, "A"),
            new FilaPrueba(0, "Borrador"),
            new FilaPrueba(7, "B"),
        });

        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;
        grid.Rows[1].Cells[0].Selected = true;

        var ids = CatalogoBatchDeletionHelper.IdsSeleccionados<FilaPrueba>(grid, f => f.Id);

        CollectionAssert.AreEqual(new[] { 5 }, ids.ToArray(),
            "Un Id no positivo (borrador) no forma parte de la eliminación.");
    }

    [TestMethod]
    public void IdsSeleccionados_FilaNuevaSeleccionada_NoParticipaDelLote()
    {
        using var grid = new DataGridView { AutoGenerateColumns = false, AllowUserToAddRows = true };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Clave", DataPropertyName = "Clave" });
        grid.Rows.Add("A");

        int indiceNueva = grid.Rows.Count - 1;
        Assert.IsTrue(grid.Rows[indiceNueva].IsNewRow, "La última fila debe ser la fila nueva del grid.");

        grid.ClearSelection();
        grid.Rows[indiceNueva].Cells[0].Selected = true;

        var ids = CatalogoBatchDeletionHelper.IdsSeleccionados<FilaPrueba>(grid, f => f.Id);

        Assert.AreEqual(0, ids.Count, "La fila nueva no se elimina.");
    }

    [TestMethod]
    public void IdsSeleccionados_SinSeleccion_DevuelveListaVacia()
    {
        using var grid = CrearGrid(new[] { new FilaPrueba(1, "A") });
        grid.ClearSelection();

        var ids = CatalogoBatchDeletionHelper.IdsSeleccionados<FilaPrueba>(grid, f => f.Id);

        Assert.AreEqual(0, ids.Count);
    }

    // ═════════════════════ Etiqueta del comando ═════════════════════

    [TestMethod]
    public void EtiquetaEliminar_CoincideConLaCantidadSeleccionada()
    {
        Assert.AreEqual("Eliminar insumo", CatalogoBatchDeletionHelper.EtiquetaEliminar(CatalogItemKind.Material, 1));
        Assert.AreEqual("Eliminar 3 insumos", CatalogoBatchDeletionHelper.EtiquetaEliminar(CatalogItemKind.Material, 3));
        Assert.AreEqual("Eliminar 2 insumos", CatalogoBatchDeletionHelper.EtiquetaEliminar(CatalogItemKind.ManoDeObra, 2));
        Assert.AreEqual("Eliminar 4 insumos", CatalogoBatchDeletionHelper.EtiquetaEliminar(CatalogItemKind.Maquinaria, 4));
        Assert.AreEqual("Eliminar elemento", CatalogoBatchDeletionHelper.EtiquetaEliminar(CatalogItemKind.Herramienta, 1));
        Assert.AreEqual("Eliminar 2 elementos", CatalogoBatchDeletionHelper.EtiquetaEliminar(CatalogItemKind.Herramienta, 2));
    }

    // ═════════════════════ Confirmación única ═════════════════════

    [TestMethod]
    public void ConstruirConfirmacion_NombraCantidadImpactoYAdvierteIrreversible()
    {
        var preview = new CatalogBatchDeletionPreview(
            CatalogItemKind.Material,
            new[] { 1, 2, 3 },
            SelectedCount: 3,
            AffectedComponentCount: 7,
            AffectedMatrixCount: 2,
            RecalculatedMatrixCount: 4,
            AffectedConceptCount: 5,
            AffectedAggregatorCount: 1,
            Fingerprint: "huella");

        string mensaje = CatalogoBatchDeletionHelper.ConstruirConfirmacion(preview);

        StringAssert.Contains(mensaje, "3 insumos seleccionados");
        StringAssert.Contains(mensaje, "7 componente(s)");
        StringAssert.Contains(mensaje, "2 matriz/matrices");
        StringAssert.Contains(mensaje, "4 matriz/matrices");
        StringAssert.Contains(mensaje, "5 concepto(s) del presupuesto");
        StringAssert.Contains(mensaje, "1 agrupador(es)");
        StringAssert.Contains(mensaje, "no se puede deshacer");
    }

    [TestMethod]
    public void ConstruirConfirmacion_SinUso_NoInventaImpactoNiRecalculo()
    {
        var preview = new CatalogBatchDeletionPreview(
            CatalogItemKind.Herramienta,
            new[] { 9 },
            SelectedCount: 1,
            AffectedComponentCount: 0,
            AffectedMatrixCount: 0,
            RecalculatedMatrixCount: 0,
            AffectedConceptCount: 0,
            AffectedAggregatorCount: 0,
            Fingerprint: "huella");

        string mensaje = CatalogoBatchDeletionHelper.ConstruirConfirmacion(preview);

        StringAssert.Contains(mensaje, "1 elemento seleccionado");
        StringAssert.Contains(mensaje, "no está en uso en ninguna matriz");
        StringAssert.Contains(mensaje, "No se requiere recálculo del presupuesto");
        StringAssert.Contains(mensaje, "no se puede deshacer");
    }

    // ═════════════════════ Guarda de teclado ═════════════════════

    [TestMethod]
    public void DebeDespacharSupr_SuprYCtrlSupr_ConSeleccionYFueraDeEdicion()
    {
        Assert.IsTrue(CatalogoBatchDeletionHelper.DebeDespacharSupr(
            Keys.Delete, shift: false, alt: false, enEdicionCelda: false, tieneSeleccion: true));
        Assert.IsTrue(CatalogoBatchDeletionHelper.DebeDespacharSupr(
            Keys.Delete, shift: false, alt: false, enEdicionCelda: false, tieneSeleccion: true),
            "Ctrl+Supr usa la misma ruta: la guarda no distingue el modificador Control.");
    }

    [TestMethod]
    public void DebeDespacharSupr_NoDespachaSinSeleccionEnEdicionONoSiendoSupr()
    {
        Assert.IsFalse(CatalogoBatchDeletionHelper.DebeDespacharSupr(
            Keys.Delete, shift: false, alt: false, enEdicionCelda: false, tieneSeleccion: false));
        Assert.IsFalse(CatalogoBatchDeletionHelper.DebeDespacharSupr(
            Keys.Delete, shift: false, alt: false, enEdicionCelda: true, tieneSeleccion: true));
        Assert.IsFalse(CatalogoBatchDeletionHelper.DebeDespacharSupr(
            Keys.Back, shift: false, alt: false, enEdicionCelda: false, tieneSeleccion: true));
        Assert.IsFalse(CatalogoBatchDeletionHelper.DebeDespacharSupr(
            Keys.Delete, shift: true, alt: false, enEdicionCelda: false, tieneSeleccion: true));
        Assert.IsFalse(CatalogoBatchDeletionHelper.DebeDespacharSupr(
            Keys.Delete, shift: false, alt: true, enEdicionCelda: false, tieneSeleccion: true));
    }

    [TestMethod]
    public void PresupuestoSupr_EnEdicionOFocoEnPanelTexto_NoDespacha()
    {
        Assert.IsTrue(PresupuestoDeleteKeyHelper.DebeDespacharSupr(
            Keys.Delete, shift: false, alt: false, enEdicionCelda: false, focoEnEntradaTextoPanel: false));

        Assert.IsFalse(PresupuestoDeleteKeyHelper.DebeDespacharSupr(
            Keys.Delete, shift: false, alt: false, enEdicionCelda: true, focoEnEntradaTextoPanel: false),
            "Con la celda en edición, Supr y Ctrl+Supr pertenecen al TextBox.");
        Assert.IsFalse(PresupuestoDeleteKeyHelper.DebeDespacharSupr(
            Keys.Delete, shift: false, alt: false, enEdicionCelda: false, focoEnEntradaTextoPanel: true),
            "Con foco en una entrada de texto del panel embebido no se borra la fila.");
    }

    // ═════════════════════ Paridad entre los cuatro catálogos ═════════════════════

    [TestMethod]
    public void LosCuatroCatalogos_DerivanLosMismosIdsDesdeLaSeleccionPorCelda()
    {
        using var ctx = CrearContexto();

        using (var form = new FormCatalogoMateriales(ctx))
        {
            var grid = form.GridPrincipal;
            grid.BindingContext = new BindingContext();
            grid.DataSource = new List<SOPRO.Application.UseCases.Materials.MaterialListItem>
            {
                new(1, "MAT-1", "A", "kg", 1m, "", OrigenInsumo.Proyecto),
                new(2, "MAT-2", "B", "kg", 2m, "", OrigenInsumo.Proyecto),
                new(3, "MAT-3", "C", "kg", 3m, "", OrigenInsumo.Proyecto),
            };

            SeleccionarFilasExtremas(grid);
            CollectionAssert.AreEqual(new[] { 1, 3 }, form.ObtenerIdsSeleccionados().ToArray(), "Materiales");
        }

        using (var form = new FormCatalogoManoObra(ctx))
        {
            var grid = form.GridPrincipal;
            grid.BindingContext = new BindingContext();
            grid.DataSource = new List<ManoDeObra>
            {
                new() { Id = 1, Clave = "MO-1", Descripcion = "A", Unidad = "jor" },
                new() { Id = 2, Clave = "MO-2", Descripcion = "B", Unidad = "jor" },
                new() { Id = 3, Clave = "MO-3", Descripcion = "C", Unidad = "jor" },
            };

            SeleccionarFilasExtremas(grid);
            CollectionAssert.AreEqual(new[] { 1, 3 }, form.ObtenerIdsSeleccionados().ToArray(), "Mano de Obra");
        }

        using (var form = new FormCatalogoHerramientas(ctx))
        {
            var grid = form.GridPrincipal;
            grid.BindingContext = new BindingContext();
            grid.DataSource = new List<Herramienta>
            {
                new() { Id = 1, Clave = "H-1", Descripcion = "A", Unidad = "hr" },
                new() { Id = 2, Clave = "H-2", Descripcion = "B", Unidad = "hr" },
                new() { Id = 3, Clave = "H-3", Descripcion = "C", Unidad = "hr" },
            };

            SeleccionarFilasExtremas(grid);
            CollectionAssert.AreEqual(new[] { 1, 3 }, form.ObtenerIdsSeleccionados().ToArray(), "Herramientas");
        }

        using (var form = new FormCatalogoMaquinaria(ctx))
        {
            var grid = form.GridPrincipal;
            grid.BindingContext = new BindingContext();
            grid.DataSource = new List<Maquinaria>
            {
                new() { Id = 1, Clave = "M-1", Descripcion = "A" },
                new() { Id = 2, Clave = "M-2", Descripcion = "B" },
                new() { Id = 3, Clave = "M-3", Descripcion = "C" },
            };

            SeleccionarFilasExtremas(grid);
            CollectionAssert.AreEqual(new[] { 1, 3 }, form.ObtenerIdsSeleccionados().ToArray(), "Maquinaria");
        }
    }

    // ═════════════════════ Guarda de contexto compartido ═════════════════════

    [TestMethod]
    public void HayCambiosSinGuardar_DetectaEdicionPendienteSinGuardarlaNiLimpiarla()
    {
        using var ctx = CrearContexto();
        var proyecto = AgregarProyecto(ctx);

        Assert.IsFalse(CatalogoBatchDeletionHelper.HayCambiosSinGuardar(ctx),
            "Un contexto recién guardado no tiene trabajo pendiente.");

        proyecto.Descripcion = "edición pendiente de la UI";

        Assert.IsTrue(CatalogoBatchDeletionHelper.HayCambiosSinGuardar(ctx),
            "La guarda detecta la edición pendiente.");
        Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
            "La guarda no guarda ni limpia el tracker: el trabajo del usuario sigue pendiente.");
    }

    // ═════════════════════ Refresco del catálogo de matrices ═════════════════════

    [TestMethod]
    public void MatrixCatalogRefresh_ConTrackerLimpio_RecargaPrecioRecalculadoDesdeLaBase()
    {
        var root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"sopro_matrix_refresh_{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(root);
        var dbPath = System.IO.Path.Combine(root, "proyecto.soproproj");

        try
        {
            using var ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var proyecto = AgregarProyecto(ctx);
            var matriz = AgregarMatrizConComponente(ctx, proyecto.Id);

            var servicio = new MatrixCatalogViewService();
            var filtro = new MatrixCatalogFilterInput { ProyectoId = proyecto.Id };

            var inicial = servicio.LoadAsync(ctx, filtro).GetAwaiter().GetResult();
            Assert.AreEqual(100m, inicial.Rows.Single().CostoDirecto);
            Assert.AreEqual(1, inicial.Rows.Single().NumInsumos);

            // Escritura externa equivalente a la eliminación en lote: contexto nuevo,
            // recálculo del cierre y commit. El contexto de la UI no se entera.
            using (var escritura = new SOPROContext(dbPath))
            {
                var matrizFresca = escritura.Matrices
                    .Include(m => m.Componentes)
                    .Single(m => m.Id == matriz.Id);
                matrizFresca.CostoDirecto = 250m;
                escritura.ComponentesMatriz.RemoveRange(matrizFresca.Componentes);
                escritura.SaveChanges();
            }

            var rastreada = servicio.LoadAsync(ctx, filtro).GetAwaiter().GetResult();
            Assert.AreEqual(100m, rastreada.Rows.Single().CostoDirecto,
                "El identity map devuelve el valor viejo mientras la matriz siga rastreada.");
            Assert.AreEqual(1, rastreada.Rows.Single().NumInsumos,
                "La colección rastreada tampoco ve el componente eliminado.");

            Assert.IsTrue(MatrixCatalogRefreshHelper.DescartarRastreados(ctx),
                "Con el tracker limpio se pueden descartar matrices/componentes rastreados.");

            var fresca = servicio.LoadAsync(ctx, filtro).GetAwaiter().GetResult();
            Assert.AreEqual(250m, fresca.Rows.Single().CostoDirecto,
                "Tras descartar los rastreados, el catálogo muestra el precio recalculado.");
            Assert.AreEqual(0, fresca.Rows.Single().NumInsumos,
                "Tras descartar los rastreados, la colección refleja los componentes eliminados.");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { System.IO.Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public void MatrixCatalogRefresh_ConCambiosPendientes_NoDescartaNada()
    {
        using var ctx = CrearContexto();
        var proyecto = AgregarProyecto(ctx);
        AgregarMatrizConComponente(ctx, proyecto.Id);

        var rastreada = ctx.Matrices.Include(m => m.Componentes).Single();
        rastreada.Descripcion = "edición pendiente";

        Assert.IsFalse(MatrixCatalogRefreshHelper.DescartarRastreados(ctx),
            "Con trabajo pendiente no se descarta el tracker.");
        Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
            "La guarda no guarda ni limpia las ediciones pendientes.");
    }

    // ═════════════════════ Elegibilidad de eliminación (alcance, no Origen) ═════════════════════

    [TestMethod]
    public void Catalogos_CopiaImportadaDelMaestro_HabilitaEliminacionSinMirarOrigen()
    {
        using var ctx = CrearContexto();
        var proyecto = AgregarProyecto(ctx);

        using (var form = new FormCatalogoMateriales(ctx, proyecto.Id))
        {
            EnlazarYSeleccionarPrimeraFila(form.GridPrincipal, new List<SOPRO.Application.UseCases.Materials.MaterialListItem>
            {
                new(1, "MAT-1", "Copia importada", "kg", 10m, "IMPORTADO", OrigenInsumo.Maestro),
            });

            Assert.IsTrue(BotonEliminar(form).Enabled,
                "Materiales: una copia importada (Origen Maestro) con ProyectoId local debe poder eliminarse.");
            CollectionAssert.AreEqual(new[] { 1 }, form.ObtenerIdsSeleccionados().ToArray());
        }

        using (var form = new FormCatalogoManoObra(ctx, proyecto.Id))
        {
            EnlazarYSeleccionarPrimeraFila(form.GridPrincipal, new List<ManoDeObra>
            {
                new() { Id = 1, ProyectoId = proyecto.Id, Clave = "MO-1", Descripcion = "Copia importada", Unidad = "jor", Origen = OrigenInsumo.Maestro },
            });

            Assert.IsTrue(BotonEliminar(form).Enabled,
                "Mano de obra: una copia importada (Origen Maestro) con ProyectoId local debe poder eliminarse.");
            CollectionAssert.AreEqual(new[] { 1 }, form.ObtenerIdsSeleccionados().ToArray());
        }

        using (var form = new FormCatalogoHerramientas(ctx, proyecto.Id))
        {
            EnlazarYSeleccionarPrimeraFila(form.GridPrincipal, new List<Herramienta>
            {
                new() { Id = 1, ProyectoId = proyecto.Id, Clave = "H-1", Descripcion = "Copia importada", Unidad = "hr", Origen = OrigenInsumo.Maestro },
            });

            Assert.IsTrue(BotonEliminar(form).Enabled,
                "Herramientas: una copia importada (Origen Maestro) con ProyectoId local debe poder eliminarse.");
            CollectionAssert.AreEqual(new[] { 1 }, form.ObtenerIdsSeleccionados().ToArray());
        }

        using (var form = new FormCatalogoMaquinaria(ctx, proyecto.Id))
        {
            EnlazarYSeleccionarPrimeraFila(form.GridPrincipal, new List<Maquinaria>
            {
                new() { Id = 1, ProyectoId = proyecto.Id, Clave = "M-1", Descripcion = "Copia importada", Origen = OrigenInsumo.Maestro },
            });

            Assert.IsTrue(BotonEliminar(form).Enabled,
                "Maquinaria: una copia importada (Origen Maestro) con ProyectoId local debe poder eliminarse.");
            CollectionAssert.AreEqual(new[] { 1 }, form.ObtenerIdsSeleccionados().ToArray());
        }
    }

    // ═════════════════════ Infraestructura ═════════════════════

    private static DataGridView CrearGrid(IReadOnlyList<FilaPrueba> filas)
    {
        var grid = new DataGridView
        {
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Clave", DataPropertyName = "Clave" });
        grid.BindingContext = new BindingContext();
        grid.DataSource = filas.ToList();
        return grid;
    }

    private static void SeleccionarFilasExtremas(DataGridView grid)
    {
        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;
        grid.Rows[grid.Rows.Count - 1].Cells[0].Selected = true;
    }

    private static void EnlazarYSeleccionarPrimeraFila<T>(DataGridView grid, IReadOnlyList<T> filas)
    {
        grid.BindingContext = new BindingContext();
        grid.DataSource = filas.ToList();
        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;

        var form = grid.FindForm();
        form?.GetType()
            .GetMethod("ActualizarEstadoBotones", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(form, null);
    }

    private static ToolStripButton BotonEliminar(Form form)
        => (ToolStripButton)(form.GetType()
               .GetField("btnEliminar", BindingFlags.Instance | BindingFlags.NonPublic)
               ?.GetValue(form)
           ?? throw new InvalidOperationException("No se encontró el botón Eliminar del catálogo."));

    private static Proyecto AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto refresco matrices",
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
        return proyecto;
    }

    private static Matriz AgregarMatrizConComponente(SOPROContext ctx, int proyectoId)
    {
        var matriz = new Matriz
        {
            ProyectoId = proyectoId,
            Clave = "APU-01",
            Descripcion = "Matriz de prueba",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 100m,
        };

        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            Cantidad = 1m,
            Importe = 100m,
            Orden = 0,
        });

        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();
        return matriz;
    }

    private static SOPROContext CrearContexto()
    {
        var dbPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"sopro_batch_del_ui_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }
}
