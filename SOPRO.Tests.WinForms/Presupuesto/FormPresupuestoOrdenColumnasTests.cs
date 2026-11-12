using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Orden de columnas del presupuesto: la secuencia visible del grid debe ser la
/// misma que exportan los reportes (ascendente por <c>ColumnaPersonalizada.Orden</c>,
/// filtrando visibles), incluso con órdenes persistidos negativos, positivos y
/// huecos, y tras persistir un reordenamiento del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor del form fija el proyecto global de FormatoHelper.
public class FormPresupuestoOrdenColumnasTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    private string _root = null!;
    private string _dbPath = null!;
    private Proyecto? _proyectoGlobalPrevio;
    private SOPROContext? _contexto;
    private FormPresupuesto? _form;
    private Proyecto _proyecto = null!;

    [TestInitialize]
    public void Inicializar()
    {
        _root = Path.Combine(Path.GetTempPath(), $"sopro_fp_orden_columnas_{Guid.NewGuid():N}");
        _dbPath = Path.Combine(_root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(_root);

        _proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        _contexto = new SOPROContext(_dbPath);
        _contexto.Database.EnsureDeleted();
        _contexto.Database.EnsureCreated();

        _proyecto = CrearProyecto(_contexto, "Proyecto orden columnas");
        CrearConcepto(_contexto, _proyecto.Id);
        SembrarColumnasConOrdenMixto(_contexto, _proyecto.Id);

        _form = new FormPresupuesto(_contexto, _proyecto, CrearWorkspace());
    }

    [TestCleanup]
    public void Limpiar()
    {
        _form?.Dispose();
        _contexto?.Dispose();
        RestaurarProyectoGlobalFormato(_proyectoGlobalPrevio);
        EliminarDirectorio(_root);
    }

    [TestMethod]
    public void OrdenPersistidoMixto_RestauraEnElGridLaMismaSecuenciaVisibleQueExportanLosReportes()
    {
        var grid = _form!.GridPrincipal;

        var visibles = ColumnasVisiblesDelGrid(grid)
            .Select(c => (ColumnaPersonalizada)c.Tag!)
            .ToList();

        var secuenciaExportable = SecuenciaVisibleExportableDelGrid(grid);
        var secuenciaExportada = SecuenciaVisibleExportada();

        CollectionAssert.AreEqual(secuenciaExportada, secuenciaExportable,
            "El grid debe mostrar las columnas visibles en el mismo orden ascendente que PDF/Excel.");

        // Caso que el orden absoluto rompía: Descripción tiene Orden positivo (2)
        // mientras las demás bases visibles conservan negativos; no debe adelantarse.
        Assert.AreEqual("Tipo", visibles[0].NombreInterno);
        Assert.AreEqual("Descripcion", visibles[^1].NombreInterno);
    }

    [TestMethod]
    public void OrdenPersistidoMixto_MantieneFijosIdNumeroYRellenoAlrededorDeLasColumnasVisibles()
    {
        var grid = _form!.GridPrincipal;

        var colId = grid.Columns["colId"];
        var colNumero = grid.Columns["colNumero"];
        var relleno = grid.Columns.Cast<DataGridViewColumn>().Single(c => c.Name == "colRelleno");
        var visibles = ColumnasVisiblesDelGrid(grid);

        Assert.AreEqual(0, colId.DisplayIndex);
        Assert.AreEqual(1, colNumero.DisplayIndex);
        Assert.AreEqual(grid.Columns.Count - 1, relleno.DisplayIndex);

        Assert.IsTrue(visibles.All(c => c.DisplayIndex > colNumero.DisplayIndex),
            "Las columnas visibles deben quedar después de la numeración fija #.");
        Assert.IsTrue(visibles.All(c => c.DisplayIndex < relleno.DisplayIndex),
            "Las columnas visibles deben quedar antes de la columna de relleno.");
    }

    [TestMethod]
    public void PrimerApertura_ConLas23ColumnasPredeterminadas_RestauraElOrdenPersistidoCompleto()
    {
        var proyectoNuevo = CrearProyecto(_contexto!, "Proyecto defaults");
        CrearConcepto(_contexto!, proyectoNuevo.Id);

        using var form = new FormPresupuesto(_contexto!, proyectoNuevo, CrearWorkspace());
        var grid = form.GridPrincipal;

        var esperadoTodas = SecuenciaPersistidaDelProyecto(soloVisibles: false, proyectoNuevo.Id);
        var esperadoVisibles = SecuenciaPersistidaDelProyecto(soloVisibles: true, proyectoNuevo.Id);

        Assert.AreEqual(23, esperadoTodas.Length, "La primera apertura debe sembrar las 23 columnas predeterminadas.");

        CollectionAssert.AreEqual(esperadoTodas,
            SecuenciaColumnasPersonalizadasDelGrid(grid, soloVisibles: false),
            "Las 23 columnas deben quedar en el orden persistido canónico.");

        CollectionAssert.AreEqual(esperadoVisibles,
            SecuenciaColumnasPersonalizadasDelGrid(grid, soloVisibles: true),
            "La secuencia visible inicial debe ser la persistida ascendente.");

        Assert.AreEqual(0, grid.Columns["colId"]!.DisplayIndex);
        Assert.AreEqual(1, grid.Columns["colNumero"]!.DisplayIndex);
        Assert.AreEqual(grid.Columns.Count - 1,
            grid.Columns.Cast<DataGridViewColumn>().Single(c => c.Name == "colRelleno").DisplayIndex);
    }

    [TestMethod]
    public void IndiceDeInsercion_UsaLaPosicionRelativaDeColumnasPersonalizadasSinColumnasFijas()
    {
        var grid = _form!.GridPrincipal;
        var clave = ColumnaVisible(grid, "Clave");

        grid.CurrentCell = grid.Rows[0].Cells[clave.Index];

        Assert.AreEqual(1, _form.ObtenerOrdenColumnaActualParaInsercion(),
            "Clave es la segunda columna personalizada; el índice no debe incluir colId/#.");

        grid.CurrentCell = grid.Rows[0].Cells[grid.Columns["colNumero"]!.Index];

        Assert.AreEqual(-1, _form.ObtenerOrdenColumnaActualParaInsercion(),
            "La numeración fija # no es una columna personalizada insertable.");
    }

    [TestMethod]
    public void EncabezadoSeleccionado_ConCurrentCellEnOtraColumna_InsertaDespuesDeLaColumnaDelEncabezado()
    {
        var grid = _form!.GridPrincipal;
        var clave = ColumnaVisible(grid, "Clave");
        var pu = ColumnaVisible(grid, "PrecioUnitario");

        // Click en el encabezado real: WinForms dispara ColumnHeaderMouseClick y NO
        // mueve CurrentCell, que sigue en Clave.
        grid.CurrentCell = grid.Rows[0].Cells[clave.Index];
        SimularClickEnEncabezado(grid, pu.Index);

        Assert.AreSame(clave, grid.CurrentCell!.OwningColumn,
            "El escenario reproduce el bug: tras el click de encabezado CurrentCell sigue en Clave.");
        Assert.AreEqual(((ColumnaPersonalizada)pu.Tag!).Id, _form.ColumnaSeleccionada!.Id,
            "El encabezado seleccionado debe quedar registrado como columna activa.");

        int rankClave = ColumnasVisiblesDelGrid(grid).IndexOf(clave);
        int rankPu = ColumnasVisiblesDelGrid(grid).IndexOf(pu);
        Assert.AreEqual(1, rankClave, "Clave es la segunda columna personalizada visible.");
        Assert.IsTrue(rankPu > rankClave,
            "P.U. debe estar a la derecha de Clave para que el bug sea observable.");

        Assert.AreEqual(rankPu, _form.ObtenerOrdenColumnaActualParaInsercion(),
            "El punto de inserción debe ser el encabezado seleccionado (P.U.), no la celda actual (Clave).");

        var oculta = grid.Columns.Cast<DataGridViewColumn>()
            .Single(c => c.Tag is ColumnaPersonalizada { Visible: false, NombreInterno: "Indirectos" });
        int ocultaId = ((ColumnaPersonalizada)oculta.Tag!).Id;

        var resultado = BudgetColumnConfigurationService.SetColumnVisibility(
            new ProjectDbContextFactory(), _dbPath, _proyecto.Id, ocultaId,
            visible: true, insertionIndex: rankPu);

        Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);

        _form.RecargarTrasDialogoColumnas();

        var persistida = SecuenciaPersistidaDelProyecto(soloVisibles: true);
        Assert.AreEqual(
            Array.IndexOf(persistida, "PrecioUnitario") + 1,
            Array.IndexOf(persistida, "Indirectos"),
            "La columna mostrada debe quedar inmediatamente a la derecha de P.U., no de Clave.");

        var visibleEnGrid = SecuenciaColumnasPersonalizadasDelGrid(_form.GridPrincipal, soloVisibles: true);
        Assert.AreEqual(
            Array.IndexOf(visibleEnGrid, "PrecioUnitario") + 1,
            Array.IndexOf(visibleEnGrid, "Indirectos"),
            "El grid debe reflejar la columna mostrada inmediatamente a la derecha de P.U.");
    }

    [TestMethod]
    public void MostrarColumnaOculta_LaInsertaDespuesDelFocoYLaRecargaCoincideConElOrdenPersistido()
    {
        var grid = _form!.GridPrincipal;
        var clave = ColumnaVisible(grid, "Clave");
        var oculta = grid.Columns.Cast<DataGridViewColumn>()
            .Single(c => c.Tag is ColumnaPersonalizada { Visible: false, NombreInterno: "Indirectos" });
        int ocultaId = ((ColumnaPersonalizada)oculta.Tag!).Id;

        grid.CurrentCell = grid.Rows[0].Cells[clave.Index];
        int insertionIndex = _form.ObtenerOrdenColumnaActualParaInsercion();
        Assert.AreEqual(1, insertionIndex);

        var resultado = BudgetColumnConfigurationService.SetColumnVisibility(
            new ProjectDbContextFactory(), _dbPath, _proyecto.Id, ocultaId,
            visible: true, insertionIndex: insertionIndex);

        Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);

        _form.RecargarTrasDialogoColumnas();

        var esperado = SecuenciaPersistidaDelProyecto(soloVisibles: false);
        var actual = SecuenciaColumnasPersonalizadasDelGrid(_form.GridPrincipal, soloVisibles: false);

        CollectionAssert.AreEqual(esperado, actual,
            "Tras mostrar una columna oculta, el grid debe restaurar exactamente el orden persistido.");

        int posicionClave = Array.IndexOf(actual, "Clave");
        int posicionOculta = Array.IndexOf(actual, "Indirectos");
        Assert.AreEqual(posicionClave + 1, posicionOculta,
            "La columna mostrada debe quedar inmediatamente a la derecha de la columna enfocada.");
    }

    [TestMethod]
    public void MostrarColumnaOculta_ConFocoDesplazadoTrasMoverYRecargar_RespetaElRankVisualPersistido()
    {
        var grid = _form!.GridPrincipal;
        var clave = ColumnaVisible(grid, "Clave");
        var importe = ColumnaVisible(grid, "Importe");

        clave.DisplayIndex = importe.DisplayIndex + 1;
        Assert.IsTrue(_form.GuardarOrdenPrevioADialogoColumnas());
        _form.RecargarTrasDialogoColumnas();

        grid = _form.GridPrincipal;
        var claveRecargada = ColumnaVisible(grid, "Clave");
        var visibles = ColumnasVisiblesDelGrid(grid);
        int rankEsperado = visibles.IndexOf(claveRecargada);

        Assert.AreNotEqual(1, rankEsperado,
            "Tras mover Clave a la derecha de Importe su rank visual ya no es el inicial.");

        grid.CurrentCell = grid.Rows[0].Cells[claveRecargada.Index];
        Assert.AreEqual(rankEsperado, _form.ObtenerOrdenColumnaActualParaInsercion());

        var oculta = grid.Columns.Cast<DataGridViewColumn>()
            .Single(c => c.Tag is ColumnaPersonalizada { Visible: false, NombreInterno: "Indirectos" });
        int ocultaId = ((ColumnaPersonalizada)oculta.Tag!).Id;

        var resultado = BudgetColumnConfigurationService.SetColumnVisibility(
            new ProjectDbContextFactory(), _dbPath, _proyecto.Id, ocultaId,
            visible: true, insertionIndex: rankEsperado);

        Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);

        _form.RecargarTrasDialogoColumnas();

        var esperado = SecuenciaPersistidaDelProyecto(soloVisibles: false);
        var actual = SecuenciaColumnasPersonalizadasDelGrid(_form.GridPrincipal, soloVisibles: false);
        CollectionAssert.AreEqual(esperado, actual);

        int posicionClave = Array.IndexOf(actual, "Clave");
        int posicionOculta = Array.IndexOf(actual, "Indirectos");
        Assert.AreEqual(posicionClave + 1, posicionOculta,
            "El foco desplazado debe seguir siendo el punto de inserción de la columna mostrada.");
    }

    [TestMethod]
    [Timeout(120000)]
    public void OrdenPersistidoAleatorio_ConHuecosYRepetidos_RestauraLaSecuenciaExactaAntesYDespuesDeRecargar()
    {
        var semillas = new Random(20261112);

        for (int iteracion = 0; iteracion < 10; iteracion++)
        {
            _form!.Dispose();

            int semilla = semillas.Next();
            using (var escritura = new SOPROContext(_dbPath))
            {
                var columnas = escritura.ColumnasPersonalizadas
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .ToList();
                var aleatorio = new Random(semilla);

                foreach (var columna in columnas)
                    columna.Orden = aleatorio.Next(-50, 51);

                escritura.SaveChanges();
            }

            _form = new FormPresupuesto(_contexto!, _proyecto, CrearWorkspace());

            var todasEsperadas = SecuenciaPersistidaDelProyecto(soloVisibles: false);
            var visiblesEsperadas = SecuenciaPersistidaDelProyecto(soloVisibles: true);

            CollectionAssert.AreEqual(
                todasEsperadas,
                SecuenciaColumnasPersonalizadasDelGrid(_form.GridPrincipal, soloVisibles: false),
                $"iteración {iteracion} (semilla {semilla}): todas las columnas personalizadas.");

            CollectionAssert.AreEqual(
                visiblesEsperadas,
                SecuenciaColumnasPersonalizadasDelGrid(_form.GridPrincipal, soloVisibles: true),
                $"iteración {iteracion} (semilla {semilla}): columnas visibles.");

            _form.RecargarTrasDialogoColumnas();

            CollectionAssert.AreEqual(
                todasEsperadas,
                SecuenciaColumnasPersonalizadasDelGrid(_form.GridPrincipal, soloVisibles: false),
                $"iteración {iteracion} (semilla {semilla}) tras recargar: todas las columnas personalizadas.");

            CollectionAssert.AreEqual(
                visiblesEsperadas,
                SecuenciaColumnasPersonalizadasDelGrid(_form.GridPrincipal, soloVisibles: true),
                $"iteración {iteracion} (semilla {semilla}) tras recargar: columnas visibles.");
        }
    }

    [TestMethod]
    public void ReordenarEnGrid_PersisteLaMismaSecuenciaRelativaQueExportanLosReportesYSeRestauraAlRecargar()
    {
        var grid = _form!.GridPrincipal;
        var clave = ColumnaVisible(grid, "Clave");
        var importe = ColumnaVisible(grid, "Importe");

        clave.DisplayIndex = importe.DisplayIndex + 1;

        var secuenciaEsperada = SecuenciaVisibleExportableDelGrid(grid);

        Assert.IsTrue(_form.GuardarOrdenPrevioADialogoColumnas(),
            "El orden del grid debe persistirse por el servicio antes de recargar.");

        _form.RecargarTrasDialogoColumnas();

        var secuenciaRecargada = SecuenciaVisibleExportableDelGrid(_form.GridPrincipal);

        CollectionAssert.AreEqual(secuenciaEsperada, secuenciaRecargada,
            "Tras recargar, el grid debe restaurar la secuencia reordenada.");
        CollectionAssert.AreEqual(SecuenciaVisibleExportada(), secuenciaRecargada,
            "La secuencia persistida por el usuario debe ser la misma que exportan PDF/Excel.");
    }

    [TestMethod]
    public void SnapshotParaExportacion_TrasArrastrarSinCerrarNiAbrirDialogo_ReflejaElOrdenActualDelGrid()
    {
        var grid = _form!.GridPrincipal;
        var clave = ColumnaVisible(grid, "Clave");
        var importe = ColumnaVisible(grid, "Importe");

        clave.DisplayIndex = importe.DisplayIndex + 1;

        var esperadoGrid = SecuenciaVisibleExportableDelGrid(grid);
        var snapshot = _form.ConstruirSnapshotPresupuestoParaExportacion();

        Assert.IsNotNull(snapshot, "El orden visual actual debe persistirse antes de construir el snapshot.");

        var exportada = snapshot.Columnas
            .Where(c => c.Visible)
            .OrderBy(c => c.Orden)
            .Select(c => c.Identificador)
            .ToArray();

        CollectionAssert.AreEqual(esperadoGrid, exportada,
            "PDF/Excel deben leer el orden que el usuario ve en el grid, sin cerrar ni abrir el diálogo.");

        CollectionAssert.AreEqual(
            SecuenciaColumnasPersonalizadasDelGrid(grid, soloVisibles: false),
            SecuenciaPersistidaDelProyecto(soloVisibles: false),
            "El helper de exportación debe persistir el orden completo antes de leerlo.");
    }

    [TestMethod]
    public void SnapshotParaExportacion_SiElOrdenNoSePuedePersistir_AbortaSinSnapshot()
    {
        var mensajes = new List<string>();
        _form!.MostrarMensajeColumnasParaPrueba = mensajes.Add;
        _form.MarcarColumnasBloqueadasPorResultadoIncierto();

        var snapshot = _form.ConstruirSnapshotPresupuestoParaExportacion();

        Assert.IsNull(snapshot,
            "La exportación debe abortar en vez de generar PDF/Excel con un orden persistido viejo.");
        Assert.IsTrue(mensajes.Count > 0,
            "El fallo debe informarse con el comportamiento existente de columnas.");
    }

    // ───────────────────────────── Infraestructura ─────────────────────────────

    /// <summary>
    /// Dispara el mismo evento que un click real en el encabezado del grid, para que
    /// la prueba ejercite el handler de producción (no sólo su seam).
    /// </summary>
    private static void SimularClickEnEncabezado(DataGridView grid, int colIndex)
    {
        var metodo = typeof(DataGridView).GetMethod(
            "OnColumnHeaderMouseClick",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "No se encontró DataGridView.OnColumnHeaderMouseClick.");

        metodo.Invoke(grid, new object[]
        {
            new DataGridViewCellMouseEventArgs(colIndex, -1, 0, 0,
                new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0))
        });
    }

    private static DataGridViewColumn ColumnaVisible(DataGridView grid, string nombreInterno)
        => grid.Columns.Cast<DataGridViewColumn>()
            .Single(c => c.Tag is ColumnaPersonalizada cfg
                      && cfg.Visible
                      && string.Equals(cfg.NombreInterno, nombreInterno, StringComparison.OrdinalIgnoreCase));

    private static List<DataGridViewColumn> ColumnasVisiblesDelGrid(DataGridView grid)
        => grid.Columns.Cast<DataGridViewColumn>()
            .Where(c => c.Tag is ColumnaPersonalizada { Visible: true })
            .OrderBy(c => c.DisplayIndex)
            .ToList();

    private string[] SecuenciaPersistidaDelProyecto(bool soloVisibles, int? proyectoId = null)
    {
        int idProyecto = proyectoId ?? _proyecto.Id;
        using var lectura = new SOPROContext(_dbPath);
        var columnas = lectura.ColumnasPersonalizadas.AsNoTracking()
            .Where(c => c.ProyectoId == idProyecto);

        if (soloVisibles)
            columnas = columnas.Where(c => c.Visible);

        return columnas
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Id)
            .Select(c => c.NombreInterno)
            .ToArray();
    }

    private static string[] SecuenciaColumnasPersonalizadasDelGrid(
        DataGridView grid, bool soloVisibles)
        => grid.Columns.Cast<DataGridViewColumn>()
            .Where(c => c.Tag is ColumnaPersonalizada cfg
                     && (!soloVisibles || cfg.Visible))
            .OrderBy(c => c.DisplayIndex)
            .Select(c => ((ColumnaPersonalizada)c.Tag!).NombreInterno)
            .ToArray();

    /// <summary>
    /// Secuencia visible del grid que sí exporta el reporte: excluye las columnas
    /// internas (Tipo/relleno), igual que <c>PresupuestoReportSnapshotBuilder</c>.
    /// </summary>
    private static string[] SecuenciaVisibleExportableDelGrid(DataGridView grid)
        => ColumnasVisiblesDelGrid(grid)
            .Select(c => (ColumnaPersonalizada)c.Tag!)
            .Where(c => !ReportColumnDefinitionMapper.EsColumnaInterna(c.NombreInterno))
            .Select(c => ReportColumnDefinitionMapper.NormalizarIdentificador(c.NombreInterno))
            .ToArray();

    private string[] SecuenciaVisibleExportada()
    {
        using var lectura = new SOPROContext(_dbPath);
        var columnas = lectura.ColumnasPersonalizadas.AsNoTracking()
            .Where(c => c.ProyectoId == _proyecto.Id)
            .ToList();

        return new PresupuestoReportSnapshotBuilder()
            .Build(_proyecto.Id, "Presupuesto", columnas)
            .Columnas
            .Where(c => c.Visible)
            .OrderBy(c => c.Orden)
            .Select(c => c.Identificador)
            .ToArray();
    }

    private static void SembrarColumnasConOrdenMixto(SOPROContext context, int proyectoId)
    {
        int ordenOculto = 100;

        foreach (var definicion in ColumnasPresupuestoHelper.CrearDefinicionesPredeterminadas())
        {
            var columna = definicion.ToEntity(proyectoId);

            // Descripción (visible) con orden positivo dentro del rango que antes se
            // interpretaba como DisplayIndex absoluto; el resto de bases conserva los
            // negativos canónicos. Las ocultas quedan lejos para no aportar ruido.
            columna.Orden = definicion.NombreInterno switch
            {
                "Descripcion" => 2,
                _ when definicion.Visible => definicion.Orden,
                _ => ordenOculto++
            };

            context.ColumnasPersonalizadas.Add(columna);
        }

        context.SaveChanges();
    }

    private ProjectWorkspaceService CrearWorkspace()
        => new(Path.Combine(_root, "sopro"), Path.Combine(_root, "local"));

    private static Proyecto CrearProyecto(SOPROContext context, string nombre)
    {
        var proyecto = new Proyecto
        {
            Nombre = nombre,
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
            FechaModificacion = new DateTime(2026, 1, 1)
        };

        context.Proyectos.Add(proyecto);
        context.SaveChanges();
        return proyecto;
    }

    private static void CrearConcepto(SOPROContext context, int proyectoId)
    {
        context.ConceptosPresupuesto.Add(new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = "C-001",
            Descripcion = "Concepto de prueba",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 1,
            Unidad = "m3",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 1m,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 100m,
            PrecioUnitario = 100m,
            ImporteTotal = 100m,
            FechaModificacion = new DateTime(2026, 1, 1)
        });
        context.SaveChanges();
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)CampoProyectoGlobal().GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => CampoProyectoGlobal().SetValue(null, previo);

    private static FieldInfo CampoProyectoGlobal()
        => typeof(FormatoHelper).GetField(
               CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)
           ?? throw new InvalidOperationException(
               $"No se encontró el campo FormatoHelper.{CampoProyectoGlobalFormato}.");

    private static void EliminarDirectorio(string root)
    {
        for (int intento = 0; intento < 5; intento++)
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException) when (intento < 4)
            {
                System.Threading.Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException) when (intento < 4)
            {
                System.Threading.Thread.Sleep(200);
            }
        }
    }
}
