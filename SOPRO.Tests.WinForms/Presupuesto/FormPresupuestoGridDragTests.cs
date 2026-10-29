using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Services;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Arrastre de filas del presupuesto con el <see cref="FormPresupuesto"/> real sobre
/// SQLite y workspace temporales. Cubre la multi-selección capturada antes del colapso
/// de WinForms, la expansión jerárquica, los destinos arriba/abajo/final, el no-op
/// dentro del bloque, la selección posterior y la persistencia única del reorden.
/// Nunca se usan datos del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class FormPresupuestoGridDragTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    private string _root = null!;
    private string _dbPath = null!;
    private Proyecto? _proyectoGlobalPrevio;
    private SOPROContext? _contexto;
    private FormPresupuesto? _form;
    private Escenario _escenario = null!;

    [TestInitialize]
    public void Inicializar()
    {
        _root = Path.Combine(Path.GetTempPath(), $"sopro_fp_drag_{Guid.NewGuid():N}");
        _dbPath = Path.Combine(_root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(_root);

        _proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        _contexto = new SOPROContext(_dbPath);
        _contexto.Database.EnsureDeleted();
        _contexto.Database.EnsureCreated();

        _escenario = CrearEscenario(_contexto);
        _form = new FormPresupuesto(_contexto, _escenario.Proyecto, new ProjectWorkspaceService(
            Path.Combine(_root, "sopro"),
            Path.Combine(_root, "local")));
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
    public void MoverDosFilasArriba_ReordenaElBloqueCompletoYSeleccionQuedaIntegra()
    {
        var grid = _form!.GridPrincipal;

        // Origen: C y D (filas 4 y 5). Destino: antes de B (fila 2).
        SeleccionarFilas(4, 5);
        var bloque = _form.CapturarBloqueArrastre(4);

        Assert.IsTrue(bloque.IsMultiSelection);
        CollectionAssert.AreEqual(new[] { 4, 5 }, bloque.Rows.ToArray());

        Assert.IsTrue(_form.AplicarSoltarArrastre(2));

        CollectionAssert.AreEqual(
            new[] { _escenario.Capitulo1.Id, _escenario.ConceptoA.Id, _escenario.ConceptoC.Id,
                    _escenario.ConceptoD.Id, _escenario.ConceptoB.Id, _escenario.Capitulo2.Id },
            ObtenerIdsEnGrid());

        var seleccionadas = ObtenerFilasSeleccionadas(grid);
        CollectionAssert.AreEqual(new[] { 2, 3 }, seleccionadas,
            "Tras mover, ambas filas movidas deben quedar seleccionadas bajo CellSelect.");

        Assert.AreEqual(BudgetOperationStatus.Committed, _form.UltimoEstadoReorden);
        Assert.IsFalse(_form.HayReordenSinConfirmar);
        CollectionAssert.AreEqual(
            new[] { _escenario.Capitulo1.Id, _escenario.ConceptoA.Id, _escenario.ConceptoC.Id,
                    _escenario.ConceptoD.Id, _escenario.ConceptoB.Id, _escenario.Capitulo2.Id },
            LeerIdsEnBase().ToArray(),
            "El reorden se persiste una sola vez con el orden confirmado.");
    }

    [TestMethod]
    public void MoverDosFilasAbajo_ReordenaElBloqueCompletoYSeleccionQuedaIntegra()
    {
        var grid = _form!.GridPrincipal;

        // Origen: A y B (filas 1 y 2). Destino: después del capítulo 2 (fila 3).
        SeleccionarFilas(1, 2);
        var bloque = _form.CapturarBloqueArrastre(1);

        Assert.IsTrue(bloque.IsMultiSelection);
        CollectionAssert.AreEqual(new[] { 1, 2 }, bloque.Rows.ToArray());

        Assert.IsTrue(_form.AplicarSoltarArrastre(3));

        CollectionAssert.AreEqual(
            new[] { _escenario.Capitulo1.Id, _escenario.Capitulo2.Id, _escenario.ConceptoA.Id,
                    _escenario.ConceptoB.Id, _escenario.ConceptoC.Id, _escenario.ConceptoD.Id },
            ObtenerIdsEnGrid());

        CollectionAssert.AreEqual(new[] { 2, 3 }, ObtenerFilasSeleccionadas(grid));
        Assert.AreEqual(BudgetOperationStatus.Committed, _form.UltimoEstadoReorden);
        CollectionAssert.AreEqual(
            new[] { _escenario.Capitulo1.Id, _escenario.Capitulo2.Id, _escenario.ConceptoA.Id,
                    _escenario.ConceptoB.Id, _escenario.ConceptoC.Id, _escenario.ConceptoD.Id },
            LeerIdsEnBase().ToArray());
    }

    [TestMethod]
    public void MoverDosFilasAlFinal_LleganTrasLaUltimaFilaDeContenido()
    {
        var grid = _form!.GridPrincipal;

        SeleccionarFilas(1, 2);
        _form.CapturarBloqueArrastre(1);

        Assert.IsTrue(_form.AplicarSoltarArrastre(5));

        CollectionAssert.AreEqual(
            new[] { _escenario.Capitulo1.Id, _escenario.Capitulo2.Id, _escenario.ConceptoC.Id,
                    _escenario.ConceptoD.Id, _escenario.ConceptoA.Id, _escenario.ConceptoB.Id },
            ObtenerIdsEnGrid());

        CollectionAssert.AreEqual(new[] { 4, 5 }, ObtenerFilasSeleccionadas(grid));
        Assert.AreEqual(BudgetOperationStatus.Committed, _form.UltimoEstadoReorden);
    }

    [TestMethod]
    public void ArrastrarAgrupadorConHijos_SeMueveElGrupoCompletoYSinHuerfanos()
    {
        var grid = _form!.GridPrincipal;

        // Selección multi-fila: capítulo 1 y su primer concepto. La jerarquía suma a B.
        SeleccionarFilas(0, 1);
        var bloque = _form.CapturarBloqueArrastre(0);

        Assert.IsTrue(bloque.IsMultiSelection);
        Assert.IsTrue(bloque.ExpandedHierarchy);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, bloque.Rows.ToArray());

        // Destino: después del capítulo 2 (fila 3 original).
        Assert.IsTrue(_form.AplicarSoltarArrastre(3));

        CollectionAssert.AreEqual(
            new[] { _escenario.Capitulo2.Id, _escenario.Capitulo1.Id, _escenario.ConceptoA.Id,
                    _escenario.ConceptoB.Id, _escenario.ConceptoC.Id, _escenario.ConceptoD.Id },
            ObtenerIdsEnGrid());

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, ObtenerFilasSeleccionadas(grid),
            "El grupo movido queda seleccionado completo.");
        Assert.AreEqual(BudgetOperationStatus.Committed, _form.UltimoEstadoReorden);
        CollectionAssert.AreEqual(
            new[] { _escenario.Capitulo2.Id, _escenario.Capitulo1.Id, _escenario.ConceptoA.Id,
                    _escenario.ConceptoB.Id, _escenario.ConceptoC.Id, _escenario.ConceptoD.Id },
            LeerIdsEnBase().ToArray());
    }

    [TestMethod]
    public void SeleccionNoContigua_MueveSoloLaFilaDeOrigen()
    {
        var grid = _form!.GridPrincipal;

        // A (1) y C (4) no son contiguas: degrada a mover solo A.
        SeleccionarFilas(1, 4);
        var bloque = _form.CapturarBloqueArrastre(1);

        Assert.IsFalse(bloque.IsMultiSelection);
        CollectionAssert.AreEqual(new[] { 1 }, bloque.Rows.ToArray());

        Assert.IsTrue(_form.AplicarSoltarArrastre(3));

        CollectionAssert.AreEqual(
            new[] { _escenario.Capitulo1.Id, _escenario.ConceptoB.Id, _escenario.Capitulo2.Id,
                    _escenario.ConceptoA.Id, _escenario.ConceptoC.Id, _escenario.ConceptoD.Id },
            ObtenerIdsEnGrid());

        CollectionAssert.AreEqual(new[] { 3 }, ObtenerFilasSeleccionadas(grid),
            "La selección dispersa no debe convertirse en multi-movimiento.");
    }

    [TestMethod]
    public void RangoDeCeldas_DegradaAMoverSoloLaFilaDeOrigen()
    {
        var grid = _form!.GridPrincipal;

        var columnaClave = grid.Columns
            .Cast<DataGridViewColumn>()
            .First(c => c.Tag is ColumnaPersonalizada { NombreInterno: "Clave" });

        grid.ClearSelection();
        grid.CurrentCell = grid.Rows[1].Cells[columnaClave.Index];
        grid.Rows[1].Cells[columnaClave.Index].Selected = true;
        grid.Rows[2].Cells[columnaClave.Index].Selected = true;

        var bloque = _form.CapturarBloqueArrastre(1);

        Assert.IsFalse(bloque.IsMultiSelection,
            "Un rango parcial de celdas no es una selección de filas completas.");
        CollectionAssert.AreEqual(new[] { 1 }, bloque.Rows.ToArray());
    }

    [TestMethod]
    public void SoltarDentroDelMismoBloque_NoMueveNiPersisteNada()
    {
        var grid = _form!.GridPrincipal;

        SeleccionarFilas(1, 2);
        _form.CapturarBloqueArrastre(1);

        Assert.IsFalse(_form.AplicarSoltarArrastre(2),
            "Soltar dentro del bloque es un no-op.");

        CollectionAssert.AreEqual(
            new[] { _escenario.Capitulo1.Id, _escenario.ConceptoA.Id, _escenario.ConceptoB.Id,
                    _escenario.Capitulo2.Id, _escenario.ConceptoC.Id, _escenario.ConceptoD.Id },
            ObtenerIdsEnGrid());

        CollectionAssert.AreEqual(new[] { 1, 2 }, ObtenerFilasSeleccionadas(grid),
            "La selección original permanece intacta.");
        Assert.IsNull(_form.UltimoEstadoReorden,
            "Un no-op no debe ejecutar el comando de reordenamiento.");
        CollectionAssert.AreEqual(
            new[] { _escenario.Capitulo1.Id, _escenario.ConceptoA.Id, _escenario.ConceptoB.Id,
                    _escenario.Capitulo2.Id, _escenario.ConceptoC.Id, _escenario.ConceptoD.Id },
            LeerIdsEnBase().ToArray(),
            "La base conserva el orden original.");
    }

    [TestMethod]
    public void SoltarSobreFilaFueraDelGrid_NoReordenaNiPersiste()
    {
        var grid = _form!.GridPrincipal;
        int filas = grid.Rows.Count;

        SeleccionarFilas(1);
        var bloque = _form.CapturarBloqueArrastre(1);
        Assert.IsTrue(bloque.IsValid);

        Assert.IsFalse(_form.AplicarSoltarArrastre(filas),
            "Una fila destino igual a Rows.Count no es un destino de soltar válido.");

        CollectionAssert.AreEqual(
            new[] { _escenario.Capitulo1.Id, _escenario.ConceptoA.Id, _escenario.ConceptoB.Id,
                    _escenario.Capitulo2.Id, _escenario.ConceptoC.Id, _escenario.ConceptoD.Id },
            ObtenerIdsEnGrid(),
            "Un destino fuera del grid no reordena la vista.");

        Assert.IsNull(_form.UltimoEstadoReorden,
            "Un destino inválido no ejecuta el comando de reordenamiento.");
        CollectionAssert.AreEqual(
            new[] { _escenario.Capitulo1.Id, _escenario.ConceptoA.Id, _escenario.ConceptoB.Id,
                    _escenario.Capitulo2.Id, _escenario.ConceptoC.Id, _escenario.ConceptoD.Id },
            LeerIdsEnBase().ToArray(),
            "La base conserva el orden original.");

        Assert.IsFalse(_form.AplicarSoltarArrastre(filas + 5),
            "Una fila destino mayor que el grid tampoco es válida.");
        CollectionAssert.AreEqual(
            new[] { _escenario.Capitulo1.Id, _escenario.ConceptoA.Id, _escenario.ConceptoB.Id,
                    _escenario.Capitulo2.Id, _escenario.ConceptoC.Id, _escenario.ConceptoD.Id },
            LeerIdsEnBase().ToArray());
    }

    [TestMethod]
    public void ArrastreSimple_ConservaElComportamientoDeUnaSolaFilaSeleccionada()
    {
        var grid = _form!.GridPrincipal;

        SeleccionarFilas(1);
        var bloque = _form.CapturarBloqueArrastre(1);

        Assert.IsFalse(bloque.IsMultiSelection);
        CollectionAssert.AreEqual(new[] { 1 }, bloque.Rows.ToArray());

        Assert.IsTrue(_form.AplicarSoltarArrastre(3));

        CollectionAssert.AreEqual(
            new[] { _escenario.Capitulo1.Id, _escenario.ConceptoB.Id, _escenario.Capitulo2.Id,
                    _escenario.ConceptoA.Id, _escenario.ConceptoC.Id, _escenario.ConceptoD.Id },
            ObtenerIdsEnGrid());

        CollectionAssert.AreEqual(new[] { 3 }, ObtenerFilasSeleccionadas(grid),
            "Un arrastre simple mantiene una sola fila seleccionada como antes.");
        Assert.AreEqual(BudgetOperationStatus.Committed, _form.UltimoEstadoReorden);
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private void SeleccionarFilas(params int[] filas)
    {
        var grid = _form!.GridPrincipal;
        grid.ClearSelection();
        if (filas.Length == 0) return;

        var columnasVisibles = grid.Columns
            .Cast<DataGridViewColumn>()
            .Where(c => c.Visible)
            .Select(c => c.Index)
            .ToList();

        grid.CurrentCell = grid.Rows[filas[0]].Cells[columnasVisibles[0]];

        // Bajo CellSelect se seleccionan todas las celdas visibles de cada fila
        // para representar una selección completa de filas (clic en encabezado).
        foreach (int fila in filas)
        {
            foreach (int columna in columnasVisibles)
                grid.Rows[fila].Cells[columna].Selected = true;
        }
    }

    private static int[] ObtenerFilasSeleccionadas(DataGridView grid)
    {
        var indices = new List<int>();
        var columnasVisibles = grid.Columns
            .Cast<DataGridViewColumn>()
            .Where(c => c.Visible)
            .Select(c => c.Index)
            .ToList();

        for (int i = 0; i < grid.Rows.Count; i++)
        {
            if (columnasVisibles.Count == 0) break;

            bool completa = columnasVisibles.All(c => grid.Rows[i].Cells[c].Selected);
            if (completa)
                indices.Add(i);
        }

        return indices.ToArray();
    }

    private int[] ObtenerIdsEnGrid()
    {
        var grid = _form!.GridPrincipal;
        var ids = new List<int>();
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            if (grid.Rows[i].Tag is ConceptoPresupuesto concepto)
                ids.Add(concepto.Id);
        }

        return ids.ToArray();
    }

    private int[] LeerIdsEnBase()
    {
        using var verificacion = new SOPROContext(_dbPath);
        return verificacion.ConceptosPresupuesto
            .AsNoTracking()
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Id)
            .Select(c => c.Id)
            .ToArray();
    }

    private sealed class Escenario
    {
        public Proyecto Proyecto { get; init; } = null!;
        public ConceptoPresupuesto Capitulo1 { get; init; } = null!;
        public ConceptoPresupuesto Capitulo2 { get; init; } = null!;
        public ConceptoPresupuesto ConceptoA { get; init; } = null!;
        public ConceptoPresupuesto ConceptoB { get; init; } = null!;
        public ConceptoPresupuesto ConceptoC { get; init; } = null!;
        public ConceptoPresupuesto ConceptoD { get; init; } = null!;
    }

    private static Escenario CrearEscenario(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto arrastre múltiple",
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

        var capitulo1 = CrearConcepto(proyecto.Id, "CAP-01", "Capítulo 1", esAgrupador: true, nivel: 0, orden: 0);
        var conceptoA = CrearConcepto(proyecto.Id, "C-001", "Concepto A", esAgrupador: false, nivel: 1, orden: 1);
        var conceptoB = CrearConcepto(proyecto.Id, "C-002", "Concepto B", esAgrupador: false, nivel: 1, orden: 2);
        var capitulo2 = CrearConcepto(proyecto.Id, "CAP-02", "Capítulo 2", esAgrupador: true, nivel: 0, orden: 3);
        var conceptoC = CrearConcepto(proyecto.Id, "C-003", "Concepto C", esAgrupador: false, nivel: 1, orden: 4);
        var conceptoD = CrearConcepto(proyecto.Id, "C-004", "Concepto D", esAgrupador: false, nivel: 1, orden: 5);

        ctx.ConceptosPresupuesto.AddRange(capitulo1, conceptoA, conceptoB, capitulo2, conceptoC, conceptoD);
        ctx.SaveChanges();

        return new Escenario
        {
            Proyecto = proyecto,
            Capitulo1 = capitulo1,
            Capitulo2 = capitulo2,
            ConceptoA = conceptoA,
            ConceptoB = conceptoB,
            ConceptoC = conceptoC,
            ConceptoD = conceptoD,
        };
    }

    private static ConceptoPresupuesto CrearConcepto(
        int proyectoId,
        string clave,
        string descripcion,
        bool esAgrupador,
        int nivel,
        int orden)
    {
        return new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = descripcion,
            EsAgrupador = esAgrupador,
            Nivel = nivel,
            Orden = orden,
            Unidad = esAgrupador ? string.Empty : "m3",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = esAgrupador ? 0m : 1m,
            CostoDirectoUnitario = esAgrupador ? 0m : 100m,
            CostoDirectoTotal = esAgrupador ? 0m : 100m,
            PrecioUnitario = esAgrupador ? 0m : 100m,
            ImporteTotal = esAgrupador ? 0m : 100m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)CampoProyectoGlobal().GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => CampoProyectoGlobal().SetValue(null, previo);

    private static System.Reflection.FieldInfo CampoProyectoGlobal()
        => typeof(SOPRO.WinForms.Helpers.FormatoHelper).GetField(
               CampoProyectoGlobalFormato,
               System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
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
