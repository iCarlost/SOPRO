using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;

namespace SOPRO.Tests.WinForms.Programa;

/// <summary>
/// Compuertas de la revisión Oracle sobre el Programa de Obra:
/// <list type="bullet">
///   <item>Los handlers que PERSISTEN formato de columna y ancho quedan bloqueados
///   cuando <c>EstaBloqueadoParaOperar</c>: sin SaveChanges ni entidades modificadas
///   del contexto compartido. El formato manual ordinario sigue funcionando al liberar.</item>
///   <item>La publicación/reconciliación lee TODO con contexto fresco (incluida Curva S)
///   antes de tocar la UI: si falla, estado previo + bloqueo intactos y sin cachés
///   parciales; la lectura completa posterior libera.</item>
/// </list>
/// Ejercita el formulario REAL sobre bases temporales (nunca datos del usuario).
/// </summary>
[STATestClass]
[DoNotParallelize] // Construye el formulario real del módulo (controles WinForms).
public class ProgramaObraBloqueoFormatoYCurvaSTests
{
    private const string TipoProgramaObra = "ProgramaObra";
    private const string ColumnaDescripcion = "colDescripcion";

    // ═══════════ (1) Formato/width bloqueados durante la reconciliación ═══════════

    [TestMethod]
    public void ReconciliacionPendiente_BloqueaFormatoYAnchoSinSaveChangesNiPendientes()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            GenerarPrograma(dbPath, proyecto.Id);

            form = new FormProgramaObra(ctx, proyecto);
            Mostrar(form);
            form.Notificador.Mostrar = (_, _) => { };

            var columnaAntes = LeerColumna(dbPath, proyecto.Id, ColumnaDescripcion);

            // Estado de reconciliación REAL: el commit de formato se hizo pero no se pudo
            // confirmar (OutcomeUnknown) → la vista queda bloqueada hasta relevar todo.
            var comandos = new[]
            {
                new CambioFormatoCeldaCommand(
                    string.Empty,
                    "ACT:1",
                    "colImporte",
                    ExpectedOverlay: null,
                    ProposedOverlay: new ReportStyleOverlay(ColorFondo: "#999999")),
            };

            Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() =>
                FormatoCeldasPersistence.Aplicar(
                    dbPath, proyecto.Id, TipoProgramaObra, comandos, form.HistorialUndo, new DisposeThrowingFactory(dbPath)));
            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation,
                "Precondición: la vista queda bloqueada tras el resultado incierto de formato.");

            var formato = CrearFormatoDestacado();
            var indice = form.GridPrincipal.Columns[ColumnaDescripcion].Index;
            form.NotificarColumnaSeleccionada(indice);

            // Ninguno de los tres handlers de persistencia puede escribir mientras dure
            // el bloqueo: ni el formato de columna, ni el global, ni el ancho.
            form.AplicarFormato(formato);
            form.AplicarFormatoGlobal(formato);

            var columna = form.GridPrincipal.Columns[ColumnaDescripcion];
            var anchoPrevio = columna.Width;
            columna.Width = anchoPrevio + 40;
            System.Windows.Forms.Application.DoEvents();

            Assert.IsFalse(form.HayCambiosPendientesEnContexto,
                "Un handler bloqueado no deja entidades modificadas (ni SaveChanges) en el contexto compartido.");
            Assert.IsTrue(form.EstaBloqueadoParaOperar, "La vista sigue bloqueada tras los intentos.");
            Assert.IsTrue(form.BloqueaEdicionPrograma(), "La edición del grid sigue bloqueada tras los intentos.");

            var columnaBloqueada = LeerColumna(dbPath, proyecto.Id, ColumnaDescripcion);
            Assert.AreEqual(columnaAntes.AnchoColumna, columnaBloqueada.AnchoColumna,
                "El ancho no se persiste mientras la vista está bloqueada.");
            Assert.AreEqual(columnaAntes.NombreFuente, columnaBloqueada.NombreFuente,
                "El formato de columna no se persiste mientras la vista está bloqueada.");
            Assert.AreEqual(columnaAntes.TamanoFuente, columnaBloqueada.TamanoFuente);
            Assert.IsFalse(columnaBloqueada.Negrita);

            // La recarga completa es la salida: libera el bloqueo…
            form.CargarPrograma();
            Assert.IsFalse(form.HistorialUndo.RequiresReconciliation, "La lectura completa libera el bloqueo.");
            Assert.IsFalse(form.EstaBloqueadoParaOperar);

            // …y el formato manual ordinario sigue funcionando exactamente igual.
            form.NotificarColumnaSeleccionada(form.GridPrincipal.Columns[ColumnaDescripcion].Index);
            form.AplicarFormato(formato);

            var columnaDesbloqueada = LeerColumna(dbPath, proyecto.Id, ColumnaDescripcion);
            Assert.AreEqual("Arial", columnaDesbloqueada.NombreFuente,
                "Sin bloqueo, el formato manual de columna se conserva (se persiste).");
            Assert.AreEqual(14, columnaDesbloqueada.TamanoFuente);
            Assert.IsTrue(columnaDesbloqueada.Negrita);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    // ═══════════ (2) Falla de lectura/publicación de Curva S ═══════════

    [TestMethod]
    public void CurvaSFallidaEnLaPublicacion_ConservaEstadoYBloqueoYLaRecargaCompletaLibera()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            GenerarPrograma(dbPath, proyecto.Id);

            var factory = new DistribucionesRotaEnLecturaFrescaFactory(dbPath);
            form = new FormProgramaObra(ctx, proyecto, factory);
            Mostrar(form);
            EmpujarHistorial(form);

            var programaAntes = form.ProgramaActual;
            var curvaAntes = form.CurvaSPublicada;
            var mensajes = new List<string>();
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);

            // El comando se confirma; la lectura fresca posterior puede leer el programa
            // pero NO la Curva S (tabla de distribuciones fuera de servicio).
            LocalizarBoton(form, "btnSincronizar").Boton.PerformClick();

            Assert.IsFalse(form.ProgramaOperacionIncierta,
                "La persistencia se confirmó: no se afirma un resultado incierto del comando.");
            Assert.IsTrue(form.RequiereLecturaFresca,
                "Sin Curva S no hay publicación completa: la vista queda bloqueada.");
            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation);
            Assert.AreSame(programaAntes, form.ProgramaActual,
                "No se publica un programa nuevo si el snapshot de publicación falló.");
            Assert.AreSame(curvaAntes, form.CurvaSPublicada,
                "No se publica una Curva S parcial: la caché anterior queda intacta.");
            Assert.IsTrue(form.GridPrincipal.ReadOnly);
            Assert.IsFalse(LocalizarBoton(form, "btnRecalcular").Boton.Enabled);
            Assert.IsFalse(LocalizarBoton(form, "btnSincronizar").Boton.Enabled);
            Assert.IsTrue(mensajes.Count > 0, "El fallo de publicación se informa al usuario.");

            // Repuesta la base, la ÚNICA salida (Recargar) obtiene la lectura completa y
            // libera el bloqueo con datos publicados de verdad.
            factory.RestaurarDistribuciones();
            LocalizarBoton(form, "btnRecargar").Boton.PerformClick();

            Assert.IsFalse(form.RequiereLecturaFresca, "La lectura completa posterior libera el bloqueo.");
            Assert.IsFalse(form.HistorialUndo.RequiresReconciliation);
            Assert.IsNotNull(form.ProgramaActual);
            Assert.IsFalse(form.GridPrincipal.ReadOnly);
            Assert.IsTrue(form.CurvaSPublicada.Count > 0, "La Curva S se publica con la lectura completa.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    // ═══════════════════════════════ Escenario ═══════════════════════════════

    private static ColumnaPersonalizada CrearFormatoDestacado() => new()
    {
        NombreFuente = "Arial",
        TamanoFuente = 14,
        Negrita = true,
        Cursiva = true,
        Alineacion = AlineacionColumna.Derecha,
        ColorFondo = "#FF0000",
        ColorFuente = "#00FF00",
        WrapTexto = true,
        AlineacionVertical = 2,
    };

    private static (int AnchoColumna, string NombreFuente, int TamanoFuente, bool Negrita) LeerColumna(
        string dbPath, int proyectoId, string nombreInterno)
    {
        using var ctx = new SOPROContext(dbPath);
        var columna = ctx.ColumnasProgramaObra.Single(c => c.ProyectoId == proyectoId && c.NombreInterno == nombreInterno);
        return (columna.AnchoColumna, columna.NombreFuente, columna.TamanoFuente, columna.Negrita);
    }

    private static string CrearRuta()
        => Path.Combine(Path.GetTempPath(), $"sopro_po_bloqueo_{Guid.NewGuid():N}.db");

    /// <summary>
    /// Muestra el formulario y procesa el evento Load (carga inicial REAL del módulo)
    /// ANTES de cualquier escenario destructivo.
    /// </summary>
    private static void Mostrar(FormProgramaObra form)
    {
        form.Show();
        System.Windows.Forms.Application.DoEvents();
    }

    private static SOPROContext CrearBase(string dbPath, out Proyecto proyecto)
    {
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();

        proyecto = new Proyecto
        {
            Nombre = "Proyecto bloqueo formato y Curva S",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 5),
            FechaTermino = new DateTime(2026, 2, 27),
            PlazoEjecucion = 40,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();

        ctx.ConceptosPresupuesto.AddRange(
            CrearConcepto(proyecto.Id, "C-01", 10m, 100m, 1),
            CrearConcepto(proyecto.Id, "C-02", 5m, 200m, 2));
        ctx.SaveChanges();

        return ctx;
    }

    private static ConceptoPresupuesto CrearConcepto(
        int proyectoId, string clave, decimal cantidad, decimal precio, int orden)
        => new()
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = $"Concepto {clave}",
            Unidad = "m2",
            EsAgrupador = false,
            Nivel = 1,
            Orden = orden,
            Cantidad = cantidad,
            CostoDirectoUnitario = precio,
            CostoDirectoTotal = cantidad * precio,
            PrecioUnitario = precio,
            ImporteTotal = cantidad * precio,
        };

    private static int GenerarPrograma(string dbPath, int proyectoId)
    {
        var result = new ProgramacionSynchronizationService().GenerateProgram(
            new ProjectDbContextFactory(),
            dbPath,
            proyectoId,
            expectedActiveProgramaId: null,
            new DateTime(2026, 1, 5),
            TipoPeriodoPrograma.Semana);

        Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Diagnostic);
        Assert.IsNotNull(result.ProgramaObraId);
        return result.ProgramaObraId!.Value;
    }

    /// <summary>Una acción de cada familia: programa (Delegate) y formato (Composite).</summary>
    private static void EmpujarHistorial(FormProgramaObra form)
    {
        Assert.IsTrue(form.HistorialUndo.Execute(new DelegateUndoableAction(
            "Editar fecha en programa", () => { }, () => { })));
        Assert.IsTrue(form.HistorialUndo.Execute(new CompositeUndoableAction(
            "Formato de una celda",
            new List<UndoChange> { new("antes", "despues") },
            _ => { },
            _ => { })));
    }

    private static (ToolStrip Barra, ToolStripButton Boton) LocalizarBoton(FormProgramaObra form, string nombre)
    {
        var barra = form.Controls.Find("toolPrograma", searchAllChildren: true)
            .OfType<ToolStrip>()
            .SingleOrDefault();
        Assert.IsNotNull(barra, "Debe existir la barra 'toolPrograma' en el formulario.");

        var boton = barra!.Items.Find(nombre, searchAllChildren: false)
            .OfType<ToolStripButton>()
            .SingleOrDefault();
        Assert.IsNotNull(boton, $"La barra 'toolPrograma' debe contener el botón '{nombre}'.");

        return (barra, boton!);
    }

    private static void Limpiar(string dbPath)
    {
        foreach (var sufijo in new[] { string.Empty, "-wal", "-shm" })
        {
            var archivo = dbPath + sufijo;
            if (!File.Exists(archivo)) continue;
            try { File.Delete(archivo); }
            catch (IOException) { }
        }
    }

    // ═══════════════════════════ Seams de prueba ═══════════════════════════

    /// <summary>
    /// Simula el commit ya realizado seguido de un fallo al cerrar el contexto (sólo en
    /// el PRIMER cierre): el ejecutor lo clasifica como OutcomeUnknown y el historial
    /// queda suspendido hasta una lectura fresca completa.
    /// </summary>
    private sealed class DisposeThrowingFactory : IProjectDbContextFactory
    {
        private readonly string _dbPath;
        private bool _failed;

        public DisposeThrowingFactory(string dbPath) => _dbPath = dbPath;

        public SOPROContext Create(string databasePath)
            => new DisposeThrowingContext(_dbPath, () =>
            {
                if (_failed) return false;
                _failed = true;
                return true;
            });

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }

    private sealed class DisposeThrowingContext : SOPROContext
    {
        private readonly Func<bool> _debeFallar;

        public DisposeThrowingContext(string dbPath, Func<bool> debeFallar)
            : base(dbPath)
        {
            _debeFallar = debeFallar;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_debeFallar()) throw new InvalidOperationException("dispose_simulado");
        }
    }

    /// <summary>
    /// Deja fuera de servicio la tabla de distribuciones al crear el contexto de la
    /// LECTURA FRESCA posterior al comando (tercera creación: la primera es la carga
    /// inicial del formulario y la segunda el comando): el programa se puede leer, pero
    /// la Curva S y el Gantt financiero fallan. <see cref="RestaurarDistribuciones"/>
    /// permite que la recarga posterior sea completa.
    /// </summary>
    private sealed class DistribucionesRotaEnLecturaFrescaFactory : IProjectDbContextFactory
    {
        private const string Tabla = "DistribucionesPeriodo";
        private const string TablaRota = "DistribucionesPeriodo_bloqueada";

        private readonly string _dbPath;
        private int _createCount;

        public DistribucionesRotaEnLecturaFrescaFactory(string dbPath) => _dbPath = dbPath;

        public SOPROContext Create(string databasePath)
        {
            var numero = Interlocked.Increment(ref _createCount);
            if (numero == 3)
                Renombrar(desde: Tabla, hacia: TablaRota);

            return new SOPROContext(_dbPath);
        }

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));

        public void RestaurarDistribuciones()
            => Renombrar(desde: TablaRota, hacia: Tabla);

        private void Renombrar(string desde, string hacia)
        {
            using var ctx = new SOPROContext(_dbPath);
            ctx.Database.ExecuteSqlRaw($"ALTER TABLE \"{desde}\" RENAME TO \"{hacia}\";");
        }
    }
}
