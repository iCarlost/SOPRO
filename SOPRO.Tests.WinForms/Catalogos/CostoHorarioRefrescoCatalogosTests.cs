using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.Models;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// Regresión del defecto de UI de la fase B de los vínculos del costo horario:
/// la calculadora guardaba y cerraba sin avisar al catálogo de maquinaria, de
/// modo que (especialmente vía editor→calculadora→Cancelar, que no recarga por
/// DialogResult) la maquinaria y las matrices con insumos vinculados quedaban
/// con valores viejos en pantalla.
///
/// El guardado atómico de <c>CatalogItemService.SaveMaquinariaAsync</c> ya es
/// correcto; aquí se demuestra que el aviso emitido al confirmar el commit:
///   1. Refresca ambos grids con el valor persistido (no con valores anteriores).
///   2. No vuelve a escribir: el persistido queda intacto y el tracker limpio.
///   3. Conserva la identidad rastreada: la referencia que el editor conserva
///      sigue siendo LA entrada del tracker y puede volver a guardarse.
///   4. No destruye borradores ajenos: con trabajo pendiente ambos catálogos
///      se abstienen de recargar y conservan las ediciones.
///   5. No deja fugas: cada instancia del catálogo de maquinaria percibe el
///      evento estático propio y lo libera de forma idempotente al descartarse.
/// </summary>
[STATestClass]
[DoNotParallelize] // Forms de WinForms y eventos estáticos compartidos.
public sealed class CostoHorarioRefrescoCatalogosTests
{
    private const string ClaveMaquinaria = "M-01";
    private const string ClaveMatriz = "APU-01";
    private const decimal CostoViejoManual = 40m;

    [TestMethod]
    public void NotificacionTrasGuardar_RefrescaGridsConElPersistido_YNoVuelveAEscribir()
    {
        ConEscenario((esc, catMaq, formMat) =>
        {
            // Punto de partida del defecto: ambas pantallas muestran el costo de
            // cálculo anterior (sembrado a mano) aunque la base cambie luego.
            Assert.AreEqual(CostoViejoManual, LeerCelda(catMaq, ClaveMaquinaria, "CostoHorario"));
            Assert.AreEqual(CostoViejoManual, LeerCelda(formMat, ClaveMatriz, "CostoDirecto"));

            // Ruta real de la calculadora (barra del catálogo y editor→calculadora
            // comparten este método), sin el MessageBox de resumen.
            using var calc = new FormCalculoCostoHorario(esc.Contexto, esc.Maquinaria);
            calc.Show();
            ProcesarMensajes();

            Task<CatalogSaveResult> guardado = calc.GuardarYNotificarAsync();
            BombearHasta(() => guardado.IsCompleted, "El guardado de la calculadora no concluyó.");
            CatalogSaveResult resultado = guardado.GetAwaiter().GetResult();

            Assert.AreEqual(esc.Maquinaria.Id, resultado.EntityId);
            Assert.IsFalse(resultado.IsNew, "La calculadora opera sobre un equipo ya persistido.");
            Assert.IsTrue(resultado.TriggeredRecalculation, "Al recalcular se propaga a las matrices.");
            Assert.IsFalse(esc.Contexto.ChangeTracker.HasChanges(), "El commit atómico deja el tracker limpio.");

            // Instantánea del persistido ANTES del aviso, leída por otro contexto.
            decimal persistMaq = LeerCostoMaquinariaPersistido(esc.DbPath);
            decimal persistMat = LeerCostoMatrizPersistido(esc.DbPath);
            Assert.AreNotEqual(CostoViejoManual, persistMaq, "El recálculo debe cambiar el costo horario.");
            Assert.AreNotEqual(CostoViejoManual, persistMat, "La propagación debe actualizar la matriz.");

            // El aviso sale del propio guardado (una sola notificación, la del
            // método de producción); los catálogos se recargan por evento y
            // pumping basta para que la recarga asíncrona pinte el persistido.
            BombearHasta(
                () => LeerCelda(catMaq, ClaveMaquinaria, "CostoHorario") == persistMaq,
                "La maquinaria no refrescó su grid con el persistido.");
            BombearHasta(
                () => LeerCelda(formMat, ClaveMatriz, "CostoDirecto") == persistMat,
                "Las matrices no refrescaron su grid con el persistido.");

            // El refresco no recalcula ni vuelve a guardar: persistido intacto y
            // tracker limpio tras la notificación.
            Assert.IsFalse(esc.Contexto.ChangeTracker.HasChanges());
            Assert.AreEqual(persistMaq, LeerCostoMaquinariaPersistido(esc.DbPath));
            Assert.AreEqual(persistMat, LeerCostoMatrizPersistido(esc.DbPath));

            // Y ambos pantallazos ya no muestran el valor viejo.
            Assert.AreNotEqual(CostoViejoManual, LeerCelda(catMaq, ClaveMaquinaria, "CostoHorario"));
            Assert.AreNotEqual(CostoViejoManual, LeerCelda(formMat, ClaveMatriz, "CostoDirecto"));
        });
    }

    [TestMethod]
    public void ReferenciaDelEditorSigueRastreada_TrasElRefrescoElEditorPuedeGuardar()
    {
        ConEscenario((esc, catMaq, formMat) =>
        {
            using FormCalculoCostoHorario calc = new FormCalculoCostoHorario(esc.Contexto, esc.Maquinaria);
            calc.Show();
            ProcesarMensajes();

            Task<CatalogSaveResult> guardado = calc.GuardarYNotificarAsync();
            BombearHasta(() => guardado.IsCompleted, "El guardado de la calculadora no concluyó.");
            guardado.GetAwaiter().GetResult();

            decimal persistMaq = LeerCostoMaquinariaPersistido(esc.DbPath);
            BombearHasta(
                () => LeerCelda(catMaq, ClaveMaquinaria, "CostoHorario") == persistMaq,
                "La maquinaria no refrescó su grid con el persistido.");

            // La instancia que FormEditarMaquinaria conserva (`_maquinaria`) sigue
            // siendo LA rastreada: el rebinde del catálogo no duplicó identidad.
            Assert.AreEqual(
                1,
                esc.Contexto.ChangeTracker.Entries<Maquinaria>().Count(e => e.Entity.Id == esc.Maquinaria.Id),
                "No debe haber dos instancias rastreadas con la misma PK.");

            // Guardar de nuevo con esa misma referencia (flujo de edición del
            // editor) debe completar sin InvalidOperationException.
            MaquinariaEditDto dtoEdicion = new MaquinariaEditDto
            {
                Clave = esc.Maquinaria.Clave,
                Descripcion = esc.Maquinaria.Descripcion,
                PotenciaNominal = esc.Maquinaria.PotenciaNominal,
                TipoCombustible = esc.Maquinaria.TipoCombustible,
                CostoHorario = esc.Maquinaria.CostoHorario,
                EsCostoCalculado = true,
                Notas = "edición posterior del editor sobre la referencia viva",
                GuardarEnMaestro = false,
                ProyectoId = esc.Maquinaria.ProyectoId,
                MaterialCombustibleId = esc.Maquinaria.MaterialCombustibleId,
                MaterialAceiteId = esc.Maquinaria.MaterialAceiteId,
                ManoDeObraOperadorId = esc.Maquinaria.ManoDeObraOperadorId
            };

            Task<CatalogSaveResult> edicion = CatalogItemService.SaveMaquinariaAsync(esc.Contexto, dtoEdicion, esc.Maquinaria);
            BombearHasta(() => edicion.IsCompleted, "El guardado del editor no concluyó.");
            CatalogSaveResult resultadoEdicion = edicion.GetAwaiter().GetResult();

            Assert.AreEqual(esc.Maquinaria.Id, resultadoEdicion.EntityId);
            Assert.IsFalse(resultadoEdicion.IsNew);

            using SOPROContext verificador = new SOPROContext(esc.DbPath);
            Maquinaria persistida = verificador.Maquinaria.Single(m => m.Id == esc.Maquinaria.Id);
            Assert.AreEqual("edición posterior del editor sobre la referencia viva", persistida.Notas);
            Assert.AreEqual(persistMaq, persistida.CostoHorario, "La edición posterior no debe alterar el costo calculado.");
        });
    }

    [TestMethod]
    public void BorradorAjenoPendiente_CatalogosRechazanRecarga_NadaSePierde()
    {
        ConEscenario((esc, catMaq, formMat) =>
        {
            esc.Material.Notas = "borrador ajeno pendiente de un editor abierto";
            Assert.IsTrue(esc.Contexto.ChangeTracker.HasChanges(), "La edición ajena debe estar pendiente en el contexto compartido.");

            FormCatalogoMaquinaria.NotificarInsumosModificados();
            ProcesarMensajes(40);

            Assert.AreEqual(
                "Hay cambios sin guardar; no se recargó el catálogo de maquinaria.",
                LeerEtiqueta(catMaq, "lblStatus"));
            Assert.AreEqual(
                "Hay cambios sin guardar; no se recargó el catálogo de matrices.",
                LeerEtiqueta(formMat, "lblStatus"));

            // Los grids conservan sus valores anteriores (no se recargaron).
            Assert.AreEqual(CostoViejoManual, LeerCelda(catMaq, ClaveMaquinaria, "CostoHorario"));
            Assert.AreEqual(CostoViejoManual, LeerCelda(formMat, ClaveMatriz, "CostoDirecto"));

            // Sin pérdida de datos: el borrador sigue vivo y es guardable.
            var entrada = esc.Contexto.ChangeTracker.Entries<Material>()
                .Single(e => e.Entity.Id == esc.Material.Id);
            Assert.AreEqual(EntityState.Modified, entrada.State);
            Assert.AreEqual("borrador ajeno pendiente de un editor abierto", entrada.Entity.Notas);

            esc.Contexto.SaveChanges();
            using var verificador = new SOPROContext(esc.DbPath);
            Assert.AreEqual(
                "borrador ajeno pendiente de un editor abierto",
                verificador.Materiales.First(m => m.Id == esc.Material.Id).Notas);
        });
    }

    [TestMethod]
    public void SuscripcionPropia_SeLiberaAlDispose_CierraYNoAfectaOtrasInstancias()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sopro_mq_bus_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string dbPath = Path.Combine(root, "maquinaria-temporal.soproproj");

        SOPROContext? contexto = null;
        FormCatalogoMaquinaria? formA = null;
        FormCatalogoMaquinaria? formB = null;
        FormCatalogoMaquinaria? formC = null;
        try
        {
            contexto = new SOPROContext(dbPath);
            contexto.Database.EnsureCreated();
            AgregarProyecto(contexto, "Proyecto bus maquinaria");

            formA = new FormCatalogoMaquinaria(contexto, contexto.Proyectos.Single().Id);
            formB = new FormCatalogoMaquinaria(contexto, contexto.Proyectos.Single().Id);
            Assert.AreEqual(1, ContarSuscripciones(formA));
            Assert.AreEqual(1, ContarSuscripciones(formB));

            formA.Dispose();
            formA.Dispose(); // idempotente: repetir no debe fallar.
            Assert.AreEqual(0, ContarSuscripciones(formA));
            Assert.AreEqual(1, ContarSuscripciones(formB), "Otra instancia viva conserva su suscripción.");

            // Disparar el bus con una instancia ya descartada no debe lanzar
            // ObjectDisposedException y no debe revivir manejadores obsoletos.
            FormCatalogoMaquinaria.NotificarInsumosModificados();
            Assert.AreEqual(0, ContarSuscripciones(formA));

            // La vía normal de cierre (Ventana cerrada → OnFormClosed) también libera.
            formC = new FormCatalogoMaquinaria(contexto, contexto.Proyectos.Single().Id);
            formC.Show();
            ProcesarMensajes();
            Assert.AreEqual(1, ContarSuscripciones(formC));
            formC.Close();
            Assert.AreEqual(0, ContarSuscripciones(formC));

            formB?.Dispose();
        }
        finally
        {
            formA?.Dispose();
            formB?.Dispose();
            formC?.Dispose();
            contexto?.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private sealed record Escenario(
        SOPROContext Contexto,
        string DbPath,
        Proyecto Proyecto,
        Material Material,
        Maquinaria Maquinaria,
        Matriz Matriz);

    /// <summary>
    /// Escenario completo compartido: proyecto, material vinculado, maquinaria
    /// con costo de cálculo viejo, matriz APU que la consume, y las dos ventanas
    /// (catálogo de maquinaria + catálogo de matrices) con sus grids ya cargados.
    /// </summary>
    private static void ConEscenario(Action<Escenario, FormCatalogoMaquinaria, FormMatrices> prueba)
    {
        string root = Path.Combine(Path.GetTempPath(), $"sopro_costo_ui_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string dbPath = Path.Combine(root, "proyecto-costos.soproproj");

        SOPROContext? contexto = null;
        FormCatalogoMaquinaria? catMaq = null;
        FormMatrices? formMat = null;
        try
        {
            contexto = new SOPROContext(dbPath);
            contexto.Database.EnsureCreated();
            Proyecto proyecto = AgregarProyecto(contexto, "Proyecto vínculos costo horario");

            var material = new Material { Clave = "DIE-01", Descripcion = "Diésel", Unidad = "lt", PrecioUnitario = 23m, ProyectoId = proyecto.Id, Notas = "" };
            contexto.Materiales.Add(material);
            contexto.SaveChanges();

            var maquinaria = new Maquinaria
            {
                Clave = ClaveMaquinaria,
                Descripcion = "Excavadora de prueba",
                ProyectoId = proyecto.Id,
                EsCostoCalculado = true, // el costo viejo ya era de cálculo y quedó vencido.
                CostoHorario = CostoViejoManual,
                ValorAdquisicion = 1000m,
                VidaEconomica = 10000m,
                CantidadCombustible = 2m,
                PrecioCombustible = 10m, // precio manual vencido; el aviso toma el del material.
                MaterialCombustibleId = material.Id,
                Notas = string.Empty,
            };
            contexto.Maquinaria.Add(maquinaria);
            contexto.SaveChanges();

            var matriz = new Matriz { Clave = ClaveMatriz, Descripcion = "Aranque de ranura", Unidad = "m3", ProyectoId = proyecto.Id, CostoDirecto = CostoViejoManual };
            matriz.Componentes.Add(new ComponenteMatriz
            {
                TipoComponente = TipoComponenteMatriz.Maquinaria,
                MaquinariaId = maquinaria.Id,
                Cantidad = 1m,
                Rendimiento = Maquinaria.CalcularRendimiento(1m),
                Importe = CostoViejoManual,
                Orden = 1,
            });
            contexto.Matrices.Add(matriz);
            contexto.SaveChanges();

            catMaq = new FormCatalogoMaquinaria(contexto, proyecto.Id);
            formMat = new FormMatrices(contexto, proyecto.Id);
            catMaq.Show();
            formMat.Show();

            BombearHasta(
                () => LeerCelda(catMaq, ClaveMaquinaria, "CostoHorario") == CostoViejoManual,
                "El catálogo de maquinaria no cargó su grid.");
            BombearHasta(
                () => LeerCelda(formMat, ClaveMatriz, "CostoDirecto") == CostoViejoManual,
                "El catálogo de matrices no cargó su grid.");

            prueba(new Escenario(contexto, dbPath, proyecto, material, maquinaria, matriz), catMaq!, formMat!);
        }
        finally
        {
            catMaq?.Dispose();
            formMat?.Dispose();
            contexto?.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            for (int intento = 0; intento < 5; intento++)
            {
                try
                {
                    if (Directory.Exists(root))
                        Directory.Delete(root, recursive: true);
                    break;
                }
                catch (IOException) { try { System.Threading.Thread.Sleep(200); } catch { } }
                catch (UnauthorizedAccessException) { try { System.Threading.Thread.Sleep(200); } catch { } }
            }
        }
    }

    private static Proyecto AgregarProyecto(SOPROContext contexto, string nombre)
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
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        contexto.Proyectos.Add(proyecto);
        contexto.SaveChanges();
        return proyecto;
    }

    /// <summary>Costo de la celda cuyo DataBoundItem tiene la clave pedida y la propiedad enlazada pedida.</summary>
    private static decimal LeerCelda(Form form, string clave, string propiedadEnlazada)
    {
        var grid = (DataGridView)form.GetType()
            .GetProperty("GridPrincipal", BindingFlags.Instance | BindingFlags.Public)!
            .GetValue(form)!;

        var columna = grid.Columns
            .Cast<DataGridViewColumn>()
            .Single(c => c.DataPropertyName == propiedadEnlazada);

        var fila = grid.Rows
            .Cast<DataGridViewRow>()
            .Single(f => !f.IsNewRow
                && Convert.ToString(LeerDatoItem(f, "Clave")) == clave);

        object? valor = fila.Cells[columna.Index].Value
            ?? throw new AssertFailedException("La celda aún no tiene valor.");
        return Convert.ToDecimal(valor);
    }

    private static object? LeerDatoItem(DataGridViewRow fila, string propiedad)
    {
        var item = fila.DataBoundItem
            ?? throw new AssertFailedException("La fila carece de DataBoundItem.");
        return item.GetType().GetProperty(propiedad)!.GetValue(item);
    }

    private static string LeerEtiqueta(Form form, string campo)
    {
        var valor = form.GetType().GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(form)!;
        return valor switch
        {
            Label etiqueta => etiqueta.Text,
            ToolStripStatusLabel estado => estado.Text,
            _ => throw new InvalidOperationException($"'{campo}' no es una etiqueta de estado conocida."),
        };
    }

    private static decimal LeerCostoMaquinariaPersistido(string dbPath)
    {
        using var ctx = new SOPROContext(dbPath);
        return ctx.Maquinaria.Single(m => m.Clave == ClaveMaquinaria && m.ProyectoId != null).CostoHorario;
    }

    private static decimal LeerCostoMatrizPersistido(string dbPath)
    {
        using var ctx = new SOPROContext(dbPath);
        var matriz = ctx.Matrices
            .Include(m => m.Componentes)
            .Single(m => m.Clave == ClaveMatriz && m.ProyectoId != null);
        return matriz.CostoDirecto;
    }

    private static void BombearHasta(Func<bool> condicion, string mensaje)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (!condicion() && DateTime.UtcNow < limite)
        {
            System.Windows.Forms.Application.DoEvents();
            System.Threading.Thread.Sleep(10);
        }
        Assert.IsTrue(condicion(), mensaje);
    }

    private static void ProcesarMensajes(int vueltas = 15)
    {
        for (int i = 0; i < vueltas; i++)
            System.Windows.Forms.Application.DoEvents();
    }

    // ── Conteo de suscripciones al bus propio (desfuga) ───────────────────────

    private static int ContarSuscripciones(FormCatalogoMaquinaria form)
    {
        var campo = typeof(FormCatalogoMaquinaria).GetField(
            nameof(FormCatalogoMaquinaria.InsumosModificados),
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(campo, "No se encontró el respaldo del evento estático InsumosModificados.");

        if (campo.GetValue(null) is not Delegate manejadores)
            return 0;

        int total = 0;
        foreach (var manejador in manejadores.GetInvocationList())
            if (ReferenciaA(manejador, form))
                total++;
        return total;
    }

    /// <summary>Un delegado apunta al form directo o a un closure que lo captura.</summary>
    private static bool ReferenciaA(Delegate manejador, object objetivo)
    {
        if (manejador.Target is null)
            return false;
        return ReferenceEquals(manejador.Target, objetivo) || EsClosureSobre(manejador.Target, objetivo);
    }

    private static bool EsClosureSobre(object closure, object objetivo)
    {
        if (!closure.GetType().IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            return false;

        foreach (var campo in closure.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var valor = campo.GetValue(closure);
            if (valor is null) continue;
            if (ReferenceEquals(valor, objetivo)) return true;
            if (EsClosureSobre(valor, objetivo)) return true;
        }

        return false;
    }
}
