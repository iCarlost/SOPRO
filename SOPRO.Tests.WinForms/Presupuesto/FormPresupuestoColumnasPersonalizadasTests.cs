using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Contrato de UI de columnas del presupuesto sobre SQLite temporal: abrir el
/// presupuesto no deja columnas rastreadas en el contexto compartido, cambiar ancho
/// o formato global no confirma un Concepto pendiente, y el diálogo escribe por el
/// servicio y recarga la lista tras una mutación confirmada. La confirmación del
/// reset se verifica sin mutar cuando el usuario cancela.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor del form fija el proyecto global de FormatoHelper.
public class FormPresupuestoColumnasPersonalizadasTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    private string _root = null!;
    private string _dbPath = null!;
    private Proyecto? _proyectoGlobalPrevio;
    private SOPROContext? _contexto;
    private FormPresupuesto? _form;
    private Proyecto _proyecto = null!;
    private ConceptoPresupuesto _concepto = null!;

    [TestInitialize]
    public void Inicializar()
    {
        _root = Path.Combine(Path.GetTempPath(), $"sopro_fp_columnas_{Guid.NewGuid():N}");
        _dbPath = Path.Combine(_root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(_root);

        _proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        _contexto = new SOPROContext(_dbPath);
        _contexto.Database.EnsureDeleted();
        _contexto.Database.EnsureCreated();

        _proyecto = CrearProyecto(_contexto, "Proyecto columnas");
        _concepto = CrearConcepto(_contexto, _proyecto.Id);

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
    public void AbrirPresupuesto_CargaColumnasSinDejarEntidadesRastreadas()
    {
        Assert.AreEqual(0, _contexto!.ChangeTracker.Entries<ColumnaPersonalizada>().Count(),
            "La carga de columnas debe usar snapshots (AsNoTracking), no entidades rastreadas.");

        var columnas = _contexto.ColumnasPersonalizadas.AsNoTracking()
            .Where(c => c.ProyectoId == _proyecto.Id)
            .ToList();

        Assert.AreEqual(23, columnas.Count);
        Assert.AreEqual(23, columnas.Select(c => c.NombreInterno).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var columnasGrid = _form!.GridPrincipal.Columns
            .Cast<DataGridViewColumn>()
            .Count(c => c.Tag is ColumnaPersonalizada);

        Assert.AreEqual(23, columnasGrid);
    }

    [TestMethod]
    public void CambiarAnchoYCerrar_PersisteConfiguracionSinConfirmarConceptoPendiente()
    {
        var concepto = _contexto!.ConceptosPresupuesto.Single(c => c.Id == _concepto.Id);
        concepto.Cantidad = 123m;
        Assert.AreEqual(EntityState.Modified, _contexto.Entry(concepto).State);

        var columna = _form!.GridPrincipal.Columns
            .Cast<DataGridViewColumn>()
            .First(c => c.Tag is ColumnaPersonalizada { NombreInterno: "Clave" });

        columna.Width = 177;

        _form.Close();

        using (var verificacion = new SOPROContext(_dbPath))
        {
            var clave = verificacion.ColumnasPersonalizadas.AsNoTracking()
                .Single(c => c.ProyectoId == _proyecto.Id && c.NombreInterno == "Clave");
            Assert.AreEqual(177, clave.AnchoColumna, "El ancho debe persistirse por servicio al cambiar.");

            var conceptos = verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == _concepto.Id);
            Assert.AreEqual(1m, conceptos.Cantidad,
                "Cerrar el presupuesto no debe confirmar Conceptos pendientes del contexto compartido.");
        }

        Assert.AreEqual(EntityState.Modified, _contexto.Entry(concepto).State,
            "El contexto compartido conserva su cambio pendiente sin limpiarse.");
    }

    [TestMethod]
    public void AplicarFormatoGlobal_NoConfirmaConceptoPendienteDelContextoCompartido()
    {
        var concepto = _contexto!.ConceptosPresupuesto.Single(c => c.Id == _concepto.Id);
        concepto.Cantidad = 321m;
        Assert.AreEqual(EntityState.Modified, _contexto.Entry(concepto).State);

        _form!.AplicarFormatoGlobal(new ColumnaPersonalizada
        {
            NombreFuente = "Verdana",
            TamanoFuente = 11,
            Negrita = true,
            Cursiva = false,
            Alineacion = AlineacionColumna.Centro,
            ColorFuente = "#123456",
            ColorFondo = "#ABCDEF",
            WrapTexto = true,
            AlineacionVertical = 2
        });

        using (var verificacion = new SOPROContext(_dbPath))
        {
            var columnas = verificacion.ColumnasPersonalizadas.AsNoTracking()
                .Where(c => c.ProyectoId == _proyecto.Id)
                .ToList();

            Assert.IsTrue(columnas.All(c => c.NombreFuente == "Verdana"));
            Assert.IsTrue(columnas.All(c => c.ColorFuente == "#123456"));
            Assert.IsTrue(columnas.All(c => c.ColorFondo == "#FFFFFF"),
                "El formato global no copia el color de fondo.");

            Assert.AreEqual(1m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == _concepto.Id).Cantidad);
        }

        Assert.AreEqual(EntityState.Modified, _contexto.Entry(concepto).State);
    }

    [TestMethod]
    public void Dialogo_EliminarColumna_EscribePorServicioYRecargaLaLista()
    {
        int totalAntes;

        using (var lectura = new SOPROContext(_dbPath))
            totalAntes = lectura.ColumnasPersonalizadas.AsNoTracking().Count(c => c.ProyectoId == _proyecto.Id);

        using var dialogo = new FormColumnasPersonalizadas(
            new ProjectDbContextFactory(), _dbPath, _proyecto.Id,
            ColumnasPresupuestoHelper.CrearDefinicionesPredeterminadas(), -1, _ => true);

        dialogo.Show();

        var grid = ObtenerGridColumnas(dialogo);
        Assert.AreEqual(totalAntes, grid.Rows.Count);
        Assert.AreEqual(1, grid.SelectedRows.Count, "El diálogo selecciona la primera columna al cargar.");

        InvocarHandler(dialogo, "btnEliminar_Click");

        Assert.IsTrue(dialogo.CambiosRealizados);
        Assert.AreEqual(totalAntes - 1, grid.Rows.Count,
            "Tras una mutación confirmada la lista se recarga desde el contexto propio.");

        using var verificacion = new SOPROContext(_dbPath);
        Assert.AreEqual(totalAntes - 1, verificacion.ColumnasPersonalizadas.AsNoTracking().Count(c => c.ProyectoId == _proyecto.Id));
        Assert.AreEqual(0, _contexto!.ChangeTracker.Entries<ColumnaPersonalizada>().Count(),
            "El diálogo no debe rastrear columnas en el contexto compartido.");
    }

    [TestMethod]
    public void Dialogo_ResetConfirmadoPorElUsuario_MutaSoloDespuesDeConfirmar()
    {
        int confirmaciones = 0;
        bool respuesta = false;

        using var dialogo = new FormColumnasPersonalizadas(
            new ProjectDbContextFactory(), _dbPath, _proyecto.Id,
            ColumnasPresupuestoHelper.CrearDefinicionesPredeterminadas(), -1,
            _ => { confirmaciones++; return respuesta; });

        dialogo.Show();

        InvocarHandler(dialogo, "btnPredeterminadas_Click");

        Assert.AreEqual(1, confirmaciones, "La confirmación debe ocurrir antes de mutar.");
        Assert.IsFalse(dialogo.CambiosRealizados);

        using (var verificacion = new SOPROContext(_dbPath))
            Assert.AreEqual(23, verificacion.ColumnasPersonalizadas.AsNoTracking().Count(c => c.ProyectoId == _proyecto.Id),
                "Cancelar la confirmación no debe escribir nada.");
    }

    [TestMethod]
    public void ReordenarYCambiarSoloFormatoEnDialogo_ConservaOrdenPersistidoYFormato()
    {
        var grid = _form!.GridPrincipal;

        var columnaClave = grid.Columns
            .Cast<DataGridViewColumn>()
            .First(c => c.Tag is ColumnaPersonalizada { NombreInterno: "Clave" });
        int claveId = ((ColumnaPersonalizada)columnaClave.Tag!).Id;

        // Reordenamiento manual pendiente: solo lo persiste el paso previo al diálogo.
        int indiceDestino = columnaClave.DisplayIndex == 5 ? 6 : 5;
        columnaClave.DisplayIndex = indiceDestino;

        var ordenEsperado = OrdenesDelGrid(grid);

        Assert.IsTrue(_form.GuardarOrdenPrevioADialogoColumnas(),
            "El orden pendiente debe confirmarse antes de abrir el diálogo.");
        CollectionAssert.AreEquivalent(ordenEsperado, LeerOrdenes(_dbPath, _proyecto.Id),
            "El orden reordenado debe quedar persistido antes del diálogo.");

        int nuevoTamano;
        string nombreInternoSeleccionado;

        using (var dialogo = new FormColumnasPersonalizadas(
            new ProjectDbContextFactory(), _dbPath, _proyecto.Id,
            ColumnasPresupuestoHelper.CrearDefinicionesPredeterminadas(), -1, _ => true))
        {
            dialogo.Show();

            var gridDialogo = ObtenerGridColumnas(dialogo);
            Assert.AreEqual(1, gridDialogo.SelectedRows.Count);

            var seleccionada = (BudgetColumnSnapshot)gridDialogo.SelectedRows[0].Tag!;
            nombreInternoSeleccionado = seleccionada.NombreInterno;

            // Cambio SOLO de formato (tamaño de fuente): no toca altas/bajas/IDs.
            var tamano = ObtenerCampo<NumericUpDown>(dialogo, "nudTamaño");
            nuevoTamano = (int)(tamano.Value >= tamano.Maximum ? tamano.Value - 1 : tamano.Value + 1);
            tamano.Value = nuevoTamano;

            Assert.IsTrue(dialogo.CambiosRealizados,
                "El cambio de formato debe confirmarse por el servicio del diálogo.");

            dialogo.Close();
        }

        CollectionAssert.AreEquivalent(ordenEsperado, LeerOrdenes(_dbPath, _proyecto.Id),
            "El diálogo no debe reenviar un orden con IDs obsoletos ni alterar el orden ya guardado.");
        Assert.AreEqual(nuevoTamano,
            LeerColumna(_dbPath, _proyecto.Id, nombreInternoSeleccionado).TamanoFuente,
            "El formato individual del diálogo debe persistir.");

        _form.RecargarTrasDialogoColumnas();

        CollectionAssert.AreEquivalent(ordenEsperado, LeerOrdenes(_dbPath, _proyecto.Id),
            "Tras la recarga el orden persistido debe ser el reordenado antes del diálogo.");

        int ordenClave = LeerOrdenes(_dbPath, _proyecto.Id)
            .Single(e => e.Id == claveId)
            .Orden;

        Assert.AreEqual(indiceDestino, ordenClave,
            "La recarga conserva en la base el orden reordenado antes del diálogo.");
    }

    [TestMethod]
    public void AbrirDosVeces_PreservaColumnasLegacyYConfiguracionPersonalizada()
    {
        var proyecto = CrearProyecto(_contexto!, "Proyecto legacy UI");
        SembrarLegacy17(_contexto!, proyecto.Id, anchoTipo: 250);

        using (var primerForm = new FormPresupuesto(_contexto!, proyecto, CrearWorkspace()))
        {
            Assert.AreEqual(23, ContarColumnas(_dbPath, proyecto.Id),
                "El set legacy 17 se reemplaza por las 23 predeterminadas en la primera apertura.");
        }

        // Configuración personalizada posterior del usuario: debe sobrevivir a la
        // segunda apertura (bootstrap add-only/no-op).
        using (var verificacion = new SOPROContext(_dbPath))
        {
            var tipo = verificacion.ColumnasPersonalizadas.Single(c => c.ProyectoId == proyecto.Id && c.NombreInterno == "Tipo");
            tipo.AnchoColumna = 260;
            tipo.NombreFuente = "Arial";
            verificacion.SaveChanges();
        }

        var tipoPrimera = LeerColumna(_dbPath, proyecto.Id, "Tipo");
        Assert.AreEqual(260, tipoPrimera.AnchoColumna);

        using (var segundoForm = new FormPresupuesto(_contexto!, proyecto, CrearWorkspace()))
        {
            Assert.AreEqual(23, ContarColumnas(_dbPath, proyecto.Id),
                "La segunda apertura no debe duplicar columnas.");
        }

        var tipoSegunda = LeerColumna(_dbPath, proyecto.Id, "Tipo");
        Assert.AreEqual(260, tipoSegunda.AnchoColumna,
            "El aseguramiento no debe pisar el ancho configurado por el usuario.");
        Assert.AreEqual("Arial", tipoSegunda.NombreFuente);
        Assert.AreEqual(tipoPrimera.Id, tipoSegunda.Id);
    }

    [TestMethod]
    public void Dialogo_ResultadoInciertoReconciliado_CerrarCancelRecargaParentSinIdsObsoletos()
    {
        int totalAntes;

        using (var lectura = new SOPROContext(_dbPath))
            totalAntes = lectura.ColumnasPersonalizadas.AsNoTracking().Count(c => c.ProyectoId == _proyecto.Id);

        var factory = new FactoryConResultadoIncierto(_dbPath);
        var mensajes = new List<string>();

        using var dialogo = new FormColumnasPersonalizadas(
            factory, _dbPath, _proyecto.Id,
            ColumnasPresupuestoHelper.CrearDefinicionesPredeterminadas(), -1, _ => true);

        dialogo.MostrarMensajeParaPrueba = mensajes.Add;
        dialogo.Show();

        // La próxima operación confirma y luego falla al cerrar el contexto: el
        // resultado es incierto aunque la escritura ya ocurrió.
        factory.FallarAlCerrarProximoContexto = true;

        InvocarHandler(dialogo, "btnEliminar_Click");

        Assert.IsFalse(dialogo.CambiosRealizados,
            "Un resultado incierto no se anuncia como cambio confirmado.");
        Assert.IsTrue(dialogo.RequiereRecargaAlCerrar,
            "La lectura fresca posterior debe marcar que el padre recargue al cerrarse.");
        Assert.IsFalse(dialogo.RequiereBloqueoParent,
            "Si la lectura fresca se completó, el padre no necesita bloquearse.");
        Assert.IsTrue(mensajes.Any(m => m.Contains("resultado", StringComparison.OrdinalIgnoreCase)
                || m.Contains("no se pudo confirmar", StringComparison.OrdinalIgnoreCase)),
            "El usuario debe ver que el resultado es incierto.");

        var gridDialogo = ObtenerGridColumnas(dialogo);
        Assert.AreEqual(totalAntes - 1, gridDialogo.Rows.Count,
            "Tras reconciliar, la lista del diálogo refleja la base (sin IDs viejos).");

        using var verificacion = new SOPROContext(_dbPath);
        var idsBase = verificacion.ColumnasPersonalizadas.AsNoTracking()
            .Where(c => c.ProyectoId == _proyecto.Id)
            .Select(c => c.Id)
            .ToHashSet();
        var idsDialogo = gridDialogo.Rows.Cast<DataGridViewRow>()
            .Select(r => ((BudgetColumnSnapshot)r.Tag!).Id)
            .ToArray();
        Assert.IsTrue(idsDialogo.All(idsBase.Contains),
            "La lista reconciliada no debe contener IDs de columnas eliminadas.");

        // El usuario cierra con Cancelar: el padre debe recargar igualmente.
        _form!.MostrarMensajeColumnasParaPrueba = mensajes.Add;
        bool consistente = _form.AplicarResultadoDialogoColumnas(dialogo, DialogResult.Cancel);

        Assert.IsTrue(consistente, "La recarga del padre debe confirmarse.");
        Assert.IsFalse(_form.ColumnasBloqueadasPorResultadoIncierto,
            "Una vista recargada no queda bloqueada.");

        var idsGridPadre = _form.GridPrincipal.Columns.Cast<DataGridViewColumn>()
            .Where(c => c.Tag is ColumnaPersonalizada)
            .Select(c => ((ColumnaPersonalizada)c.Tag!).Id)
            .ToArray();

        Assert.AreEqual(totalAntes - 1, idsGridPadre.Length,
            "El grid del padre debe recargarse aunque el diálogo se cierre con Cancelar.");
        Assert.IsTrue(idsGridPadre.All(idsBase.Contains),
            "El grid del padre no debe conservar IDs de columnas eliminadas.");
        Assert.IsTrue(_form.GuardarOrdenPrevioADialogoColumnas(),
            "Con IDs vigentes el orden previo al diálogo vuelve a confirmarse.");
    }

    [TestMethod]
    public void Dialogo_ResultadoInciertoSinLecturaFresca_BloqueaParentHastaRecargaSegura()
    {
        var factory = new FactoryConResultadoIncierto(_dbPath);
        var mensajes = new List<string>();

        using var dialogo = new FormColumnasPersonalizadas(
            factory, _dbPath, _proyecto.Id,
            ColumnasPresupuestoHelper.CrearDefinicionesPredeterminadas(), -1, _ => true);

        dialogo.MostrarMensajeParaPrueba = mensajes.Add;
        dialogo.Show();

        factory.FallarAlCerrarProximoContexto = true;
        factory.FallarLecturasDespues = true;

        InvocarHandler(dialogo, "btnEliminar_Click");

        Assert.IsFalse(dialogo.CambiosRealizados);
        Assert.IsTrue(dialogo.RequiereBloqueoParent,
            "Sin lectura fresca no se puede seguir trabajando con IDs potencialmente viejos.");
        Assert.IsFalse(dialogo.RequiereRecargaAlCerrar);

        _form!.MostrarMensajeColumnasParaPrueba = mensajes.Add;

        // La vista del padre tampoco puede recargarse (contexto compartido inutilizable):
        // sin reconciliación posible, las mutaciones de columnas quedan bloqueadas.
        _contexto!.Dispose();

        Assert.IsFalse(_form.AplicarResultadoDialogoColumnas(dialogo, DialogResult.Cancel),
            "Sin reconciliación la vista del padre no puede darse por consistente.");
        Assert.IsTrue(_form.ColumnasBloqueadasPorResultadoIncierto,
            "El padre debe bloquear las mutaciones de columnas hasta una recarga segura.");

        // Bloqueo efectivo: ninguna mutación de columnas llega a la base.
        Assert.IsFalse(_form.GuardarOrdenPrevioADialogoColumnas(),
            "El guard debe rechazar el guardado de orden con IDs potencialmente viejos.");

        int totalEnBase;
        using (var lectura = new SOPROContext(_dbPath))
            totalEnBase = lectura.ColumnasPersonalizadas.AsNoTracking().Count(c => c.ProyectoId == _proyecto.Id);

        Assert.AreEqual(22, totalEnBase, "El borrado del diálogo sí se confirmó en la base.");

        // La vista del padre conserva la columna eliminada (ID obsoleto): el guard
        // impide persistir nada con ese ID.
        var columnaObsoleta = _form.GridPrincipal.Columns.Cast<DataGridViewColumn>()
            .First(c => c.Tag is ColumnaPersonalizada { NombreInterno: "Tipo" });
        columnaObsoleta.Width += 40;

        using (var verificacion = new SOPROContext(_dbPath))
        {
            Assert.AreEqual(22, verificacion.ColumnasPersonalizadas.AsNoTracking()
                    .Count(c => c.ProyectoId == _proyecto.Id),
                "Con el bloqueo activo el ancho no se persiste ni recrea columnas.");
        }

        _form.AplicarFormatoGlobal(new ColumnaPersonalizada
        {
            NombreFuente = "Verdana",
            TamanoFuente = 12,
            Alineacion = AlineacionColumna.Izquierda,
            ColorFuente = "#101010",
            ColorFondo = "#FFFFFF"
        });

        using (var verificacion = new SOPROContext(_dbPath))
        {
            Assert.IsTrue(verificacion.ColumnasPersonalizadas.AsNoTracking()
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .All(c => c.NombreFuente != "Verdana"),
                "Con el bloqueo activo el formato global no se envía.");
        }

        Assert.IsTrue(mensajes.Any(m => m.Contains("resultado incierto", StringComparison.OrdinalIgnoreCase)),
            "El usuario debe ver por qué se bloqueó la edición de columnas.");

        // Recarga segura: reabrir el presupuesto sobre la base confirma la vista y deja
        // las mutaciones de columnas disponibles otra vez.
        using var contextoNuevo = new SOPROContext(_dbPath);
        using var formNuevo = new FormPresupuesto(contextoNuevo, _proyecto, CrearWorkspace());

        Assert.IsFalse(formNuevo.ColumnasBloqueadasPorResultadoIncierto,
            "Una recarga segura (reapertura) no conserva el bloqueo.");
        Assert.IsTrue(formNuevo.GuardarOrdenPrevioADialogoColumnas(),
            "Tras la recarga segura las mutaciones de columnas vuelven a estar disponibles.");
    }

    [TestMethod]
    [Timeout(60000)]
    public void Dialogo_BloqueoPorResultadoIncierto_NoSeReactivaAlSeleccionarNiAlMutar()
    {
        var factory = new FactoryConResultadoIncierto(_dbPath);
        var mensajes = new List<string>();

        using var dialogo = new FormColumnasPersonalizadas(
            factory, _dbPath, _proyecto.Id,
            ColumnasPresupuestoHelper.CrearDefinicionesPredeterminadas(), -1, _ => true);

        dialogo.MostrarMensajeParaPrueba = mensajes.Add;
        dialogo.Show();

        factory.FallarAlCerrarProximoContexto = true;
        factory.FallarLecturasDespues = true;

        InvocarHandler(dialogo, "btnEliminar_Click");

        Assert.IsTrue(dialogo.RequiereBloqueoParent);
        Assert.IsTrue(dialogo.MutacionesBloqueadas,
            "Sin reconciliación el diálogo queda bloqueado de forma irreversible.");

        var grid = ObtenerGridColumnas(dialogo);
        var panelFormato = ObtenerCampo<Panel>(dialogo, "panelFormato");
        var btnAplicarATodas = ObtenerCampo<Button>(dialogo, "btnAplicarATodas");
        var btnRestaurarFormato = ObtenerCampo<Button>(dialogo, "btnRestaurarFormato");
        var btnEliminar = ObtenerCampo<Button>(dialogo, "btnEliminar");
        var btnPredeterminadas = ObtenerCampo<Button>(dialogo, "btnPredeterminadas");

        Assert.IsFalse(panelFormato.Enabled, "El panel de formato queda deshabilitado.");
        Assert.IsFalse(btnAplicarATodas.Enabled);
        Assert.IsFalse(btnRestaurarFormato.Enabled);
        Assert.IsFalse(btnEliminar.Enabled);
        Assert.IsFalse(btnPredeterminadas.Enabled);
        Assert.IsTrue(grid.ReadOnly, "El grid no admite edición con el bloqueo activo.");
        Assert.IsFalse(grid.Enabled);

        int intentosAntes = factory.IntentosDeCreacion;

        // Escape 1: seleccionar otra fila no puede reactivar el panel de formato.
        Assert.IsTrue(grid.Rows.Count > 1, "Debe haber más de una columna para cambiar la selección.");
        grid.ClearSelection();
        grid.Rows[1].Selected = true;

        Assert.IsFalse(panelFormato.Enabled,
            "SelectionChanged no puede reactivar la superficie de mutación bloqueada.");

        // Escape 2: invocaciones programáticas o eventos tardíos no llegan al servicio.
        InvocarHandler(dialogo, "btnAplicarATodas_Click");
        InvocarHandler(dialogo, "btnRestaurarFormato_Click");
        InvocarHandler(dialogo, "btnEliminar_Click");
        InvocarHandler(dialogo, "btnPredeterminadas_Click");
        InvocarHandler(dialogo, "btnNueva_Click");
        InvocarHandler(dialogo, "btnColorFondo_Click");
        InvocarHandler(dialogo, "btnColorTexto_Click");

        dialogo.GetType()
            .GetMethod("DgvColumnas_CellValueChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(dialogo, new object?[]
            {
                grid,
                new DataGridViewCellEventArgs(grid.Columns["colVisible"]!.Index, 1)
            });

        dialogo.GetType()
            .GetMethod("AplicarFormatoASeleccionada", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(dialogo, Array.Empty<object>());

        Assert.AreEqual(intentosAntes, factory.IntentosDeCreacion,
            "Con el bloqueo activo ninguna mutación debe crear un contexto de escritura.");

        Assert.IsTrue(dialogo.MutacionesBloqueadas,
            "El bloqueo permanece hasta cerrar el diálogo (el padre ejecuta su ruta segura).");
        Assert.IsFalse(panelFormato.Enabled);

        using (var verificacion = new SOPROContext(_dbPath))
        {
            Assert.AreEqual(22, verificacion.ColumnasPersonalizadas.AsNoTracking()
                    .Count(c => c.ProyectoId == _proyecto.Id),
                "Ningún intento bloqueado debe escribir en la base.");
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private ProjectWorkspaceService CrearWorkspace()
        => new(Path.Combine(_root, "sopro"), Path.Combine(_root, "local"));

    /// <summary>
    /// Fábrica de prueba por instancia (nunca estática): permite inducir un commit
    /// confirmado que falla al cerrar el contexto (OutcomeUnknown real) y, si se pide,
    /// que las lecturas posteriores fallen para probar la rama sin reconciliación.
    /// </summary>
    private sealed class FactoryConResultadoIncierto : IProjectDbContextFactory
    {
        private readonly string _dbPath;

        public FactoryConResultadoIncierto(string dbPath) => _dbPath = dbPath;

        /// <summary>El próximo contexto confirmará y lanzará al cerrarse.</summary>
        public bool FallarAlCerrarProximoContexto { get; set; }

        /// <summary>Después de ese cierre incierto, toda lectura nueva falla.</summary>
        public bool FallarLecturasDespues { get; set; }

        /// <summary>Intentos de crear contexto (incluidos los que fallan).</summary>
        public int IntentosDeCreacion { get; private set; }

        public SOPROContext Create(string databasePath)
        {
            IntentosDeCreacion++;

            if (FallarLecturasDespues && !FallarAlCerrarProximoContexto)
                throw new InvalidOperationException("lectura_simulada_fallida");

            return new ContextoConCierreIncierto(_dbPath, this);
        }

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));

        public bool ConsumirFalloDeCierre()
        {
            if (!FallarAlCerrarProximoContexto)
                return false;

            FallarAlCerrarProximoContexto = false;
            return true;
        }
    }

    private sealed class ContextoConCierreIncierto : SOPROContext
    {
        private readonly FactoryConResultadoIncierto _dueno;

        public ContextoConCierreIncierto(string dbPath, FactoryConResultadoIncierto dueno)
            : base(dbPath)
            => _dueno = dueno;

        public override void Dispose()
        {
            base.Dispose();

            if (_dueno.ConsumirFalloDeCierre())
                throw new InvalidOperationException("dispose_simulado_columnas");
        }
    }

    private static int ContarColumnas(string dbPath, int proyectoId)
    {
        using var contexto = new SOPROContext(dbPath);
        return contexto.ColumnasPersonalizadas.AsNoTracking().Count(c => c.ProyectoId == proyectoId);
    }

    private static ColumnaPersonalizada LeerColumna(string dbPath, int proyectoId, string nombreInterno)
    {
        using var contexto = new SOPROContext(dbPath);
        return contexto.ColumnasPersonalizadas.AsNoTracking()
            .Single(c => c.ProyectoId == proyectoId && c.NombreInterno == nombreInterno);
    }

    private static (int Id, int Orden)[] OrdenesDelGrid(DataGridView grid)
        => grid.Columns
            .Cast<DataGridViewColumn>()
            .Where(c => c.Tag is ColumnaPersonalizada cfg && cfg.Id > 0)
            .Select(c => (((ColumnaPersonalizada)c.Tag!).Id, c.DisplayIndex))
            .OrderBy(e => e.Id)
            .ToArray();

    private static (int Id, int Orden)[] LeerOrdenes(string dbPath, int proyectoId)
    {
        using var contexto = new SOPROContext(dbPath);
        return contexto.ColumnasPersonalizadas.AsNoTracking()
            .Where(c => c.ProyectoId == proyectoId)
            .Select(c => new { c.Id, c.Orden })
            .ToList()
            .Select(c => (c.Id, c.Orden))
            .OrderBy(e => e.Id)
            .ToArray();
    }

    private static T ObtenerCampo<T>(Form form, string nombre) where T : class
    {
        var campo = form.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No se encontró {nombre}.");
        return (T)(campo.GetValue(form)
            ?? throw new InvalidOperationException($"{nombre} no inicializado."));
    }

    private static DataGridView ObtenerGridColumnas(Form dialogo)
    {
        var campo = dialogo.GetType().GetField("dgvColumnas", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró dgvColumnas.");
        return (DataGridView)(campo.GetValue(dialogo)
            ?? throw new InvalidOperationException("dgvColumnas no inicializado."));
    }

    private static void InvocarHandler(Form dialogo, string nombreMetodo)
    {
        var metodo = dialogo.GetType().GetMethod(nombreMetodo, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No se encontró {nombreMetodo}.");
        metodo.Invoke(dialogo, new object?[] { dialogo, EventArgs.Empty });
    }

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

    private static ConceptoPresupuesto CrearConcepto(SOPROContext context, int proyectoId)
    {
        var concepto = new ConceptoPresupuesto
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
        };

        context.ConceptosPresupuesto.Add(concepto);
        context.SaveChanges();
        return concepto;
    }

    private static void SembrarLegacy17(SOPROContext context, int proyectoId, int anchoTipo)
    {
        var legacy = new (string Nombre, string Interno, int Orden)[]
        {
            ("Tipo", "Tipo", 1),
            ("Clave", "Clave", 2),
            ("Descripción", "Descripcion", 3),
            ("Unidad", "Unidad", 4),
            ("Cantidad", "Cantidad", 5),
            ("P.U.", "PrecioUnitario", 6),
            ("Importe", "Importe", 7),
            ("% Indirectos", "PorcentajeIndirectos", 8),
            ("Indirectos", "Indirectos", 9),
            ("% Financiamiento", "PorcentajeFinanciamiento", 10),
            ("Financiamiento", "Financiamiento", 11),
            ("% Utilidad", "PorcentajeUtilidad", 12),
            ("Utilidad", "Utilidad", 13),
            ("Cargos Adicionales", "CargosAdicionales", 14),
            ("P.U. Final", "PrecioUnitarioFinal", 15),
            ("% Cargos Adicionales", "PorcentajeCargosAdicionales", 16),
            ("Importe Final", "ImporteFinal", 17)
        };

        foreach (var (nombre, interno, orden) in legacy)
        {
            context.ColumnasPersonalizadas.Add(new ColumnaPersonalizada
            {
                ProyectoId = proyectoId,
                Nombre = nombre,
                NombreInterno = interno,
                TipoColumna = TipoColumnaPersonalizada.Personal,
                TipoDato = TipoDatoColumna.Texto,
                AnchoColumna = interno == "Tipo" ? anchoTipo : 100,
                Visible = true,
                Orden = orden,
                Formula = string.Empty,
                FormatoNumerico = string.Empty,
                FormatoFecha = string.Empty,
                CondicionTotalizacion = string.Empty
            });
        }

        context.SaveChanges();
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)CampoProyectoGlobal().GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => CampoProyectoGlobal().SetValue(null, previo);

    private static FieldInfo CampoProyectoGlobal()
        => typeof(SOPRO.WinForms.Helpers.FormatoHelper).GetField(
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
