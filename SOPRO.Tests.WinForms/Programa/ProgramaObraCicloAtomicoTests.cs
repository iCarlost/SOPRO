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
using SOPRO.WinForms.Undo;

namespace SOPRO.Tests.WinForms.Programa;

/// <summary>
/// Ciclo de vida atómico del Programa de Obra en la UI: los comandos de
/// generar/sincronizar/reconstruir/borrar/recalcular usan la ruta con fábrica y
/// programa activo esperado, publican una lectura FRESCA de sólo lectura y clasifican
/// el resultado. Un resultado incierto bloquea TODA mutación hasta que el botón real
/// «Recargar» reconcilie programa + formato en una sola lectura completa.
/// Siempre sobre formularios reales y bases temporales (nunca datos del usuario).
/// </summary>
[STATestClass]
[DoNotParallelize] // Construye el formulario real del módulo (controles WinForms).
public class ProgramaObraCicloAtomicoTests
{
    private const string TipoProgramaObra = "ProgramaObra";
    private const string TipoPresupuesto = "Presupuesto";

    // ══════════════════ (a) Borrado confirmado ══════════════════

    [TestMethod]
    public void BorrarPrograma_CommitEliminaFormatoPersistidoYDescartaAmbasFamiliasSinResetDeUi()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            var proyectoId = GenerarPrograma(dbPath, proyecto.Id);
            var direccion = SembrarFormato(dbPath, proyectoId);

            form = new FormProgramaObra(ctx, proyecto);
            // Mostrar el formulario ejecuta su carga inicial REAL (FormProgramaObra_Load).
            // Cualquier trigger/escenario destructivo se prepara DESPUÉS de esta carga.
            Mostrar(form);
            Assert.IsNotNull(form.ProgramaActual, "Precondición: hay programa cargado.");
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo);
            Assert.AreEqual(1, form.ObtenerEstilosFilaUsuario().Count);
            EmpujarHistorial(form);
            Assert.AreEqual(2, form.HistorialUndo.UndoCount);

            form.Notificador.Confirmar = _ => true;

            var (_, boton) = LocalizarBoton(form, "btnBorrarPrograma");
            Assert.IsTrue(boton.Visible && boton.Enabled);
            boton.PerformClick();

            Assert.IsNull(form.ProgramaActual, "Tras el borrado confirmado no queda programa en la vista.");
            Assert.AreEqual(0, form.HistorialUndo.UndoCount,
                "Un borrado confirmado descarta formato Y programa.");
            Assert.AreEqual(0, form.HistorialUndo.RedoCount);
            Assert.IsFalse(form.HistorialUndo.RequiresReconciliation);
            Assert.IsFalse(form.ProgramaOperacionIncierta);
            Assert.IsNull(form.ObtenerOverlayActual(direccion),
                "La caché de celdas queda sin overrides (el comando ya borró el formato).");
            Assert.AreEqual(0, form.ObtenerEstilosFilaUsuario().Count,
                "La caché de estilos queda sin overrides (el comando ya borró el formato).");

            using var verif = new SOPROContext(dbPath);
            Assert.IsFalse(verif.ProgramasObra.Any(p => p.ProyectoId == proyectoId),
                "El programa completo se eliminó en la misma transacción.");
            Assert.AreEqual(0, verif.FormatosCeldaReporte.Count(
                f => f.ProyectoId == proyectoId && f.TipoReporte == TipoProgramaObra));
            Assert.AreEqual(0, verif.ConfiguracionesEstiloFilaReporte.Count(
                f => f.ProyectoId == proyectoId && f.TipoReporte == TipoProgramaObra));
            Assert.AreEqual(1, verif.FormatosCeldaReporte.Count(f => f.TipoReporte == TipoPresupuesto),
                "El formato de otro tipo de reporte no se toca.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    // ══════════════════ (b) Failed / Conflict ══════════════════

    [TestMethod]
    public void ConflictoDeProgramaActivo_NoRecargaNiMutaHistorialNiCaches()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            var proyectoId = GenerarPrograma(dbPath, proyecto.Id);
            var direccion = SembrarFormato(dbPath, proyectoId);

            form = new FormProgramaObra(ctx, proyecto);
            Mostrar(form);
            EmpujarHistorial(form);
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo,
                "Caché cargada para observar que un conflicto no la muta.");

            var programaAntes = form.ProgramaActual;
            var filasAntes = form.GridPrincipal.Rows.Count;
            var mensajes = new List<string>();
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);

            // Otra ventana reemplaza el programa activo: la base esperada ya no coincide.
            ReemplazarProgramaActivo(dbPath, proyectoId);

            LocalizarBoton(form, "btnSincronizar").Boton.PerformClick();

            Assert.AreSame(programaAntes, form.ProgramaActual,
                "Un conflicto NO debe recargar (ni publicar una lectura distinta).");
            Assert.AreEqual(filasAntes, form.GridPrincipal.Rows.Count);
            Assert.AreEqual(2, form.HistorialUndo.UndoCount,
                "Ni el historial de programa ni el de formato se descartan.");
            Assert.IsFalse(form.HistorialUndo.RequiresReconciliation);
            Assert.IsFalse(form.ProgramaOperacionIncierta);
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo);
            Assert.AreEqual(1, form.ObtenerEstilosFilaUsuario().Count);
            Assert.IsTrue(mensajes.Any(m => m.Contains("otra ventana", StringComparison.OrdinalIgnoreCase)),
                "El conflicto se informa con el mensaje seguro del resultado.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    [TestMethod]
    public void SyncFallidoRevertido_ConservaHistorialCachesYNoRecarga()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            var proyectoId = GenerarPrograma(dbPath, proyecto.Id);
            var direccion = SembrarFormato(dbPath, proyectoId);

            form = new FormProgramaObra(ctx, proyecto);
            Mostrar(form);
            EmpujarHistorial(form);
            var programaAntes = form.ProgramaActual;
            var mensajes = new List<string>();
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);

            // Falla la escritura de actividades: el ejecutor revierte y clasifica Failed.
            // El trigger se crea DESPUÉS de la carga inicial del formulario para que la
            // lectura normal (que recalcula/guarda) no sea la que falla.
            ctx.Database.ExecuteSqlRaw(
                "CREATE TRIGGER trg_fail_actividades BEFORE UPDATE ON \"ActividadesProgramadas\" " +
                "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_actividades'); END;");

            LocalizarBoton(form, "btnSincronizar").Boton.PerformClick();

            Assert.AreSame(programaAntes, form.ProgramaActual, "Un fallo revertido no recarga.");
            Assert.AreEqual(2, form.HistorialUndo.UndoCount);
            Assert.IsFalse(form.HistorialUndo.RequiresReconciliation,
                "Un fallo con rollback confirmado no exige reconciliación.");
            Assert.IsFalse(form.ProgramaOperacionIncierta);
            Assert.AreEqual(1, form.ObtenerEstilosFilaUsuario().Count);
            Assert.IsTrue(mensajes.Count > 0, "El fallo se informa al usuario.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    // ══════════════════ (c) OutcomeUnknown ══════════════════

    [TestMethod]
    public void CommitConfirmadoConLecturaFrescaFallida_BloqueaHastaRecargaExitosa()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            var proyectoId = GenerarPrograma(dbPath, proyecto.Id);
            var direccion = SembrarFormato(dbPath, proyectoId);

            // 1ª creación: carga inicial del formulario. 2ª: comando de sincronización.
            // 3ª: lectura fresca (falla). 4ª: recarga manual de «Recargar» (éxito).
            var factory = new ReadFailureOnFreshReadFactory(dbPath);
            form = new FormProgramaObra(ctx, proyecto, factory);
            Mostrar(form);
            EmpujarHistorial(form);
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo);

            var mensajes = new List<string>();
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);

            LocalizarBoton(form, "btnSincronizar").Boton.PerformClick();

            // El commit SÍ se confirmó; lo que falló fue republicar la vista.
            Assert.IsFalse(form.ProgramaOperacionIncierta,
                "La persistencia se confirmó: no se afirma OutcomeUnknown.");
            Assert.IsTrue(form.RequiereLecturaFresca,
                "Sin lectura fresca la vista queda bloqueada aunque el commit sea conocido.");
            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation,
                "El historial no es confiable contra una vista obsoleta.");
            Assert.AreEqual(1, form.HistorialUndo.UndoCount,
                "No se descarta historial adicional antes de la lectura completa.");

            // Mismas compuertas que un resultado incierto: toda mutación bloqueada.
            Assert.IsTrue(form.GridPrincipal.ReadOnly);
            Assert.IsFalse(LocalizarBoton(form, "btnSincronizar").Boton.Enabled);
            Assert.IsFalse(LocalizarBoton(form, "btnRecalcular").Boton.Enabled);
            Assert.IsFalse(LocalizarBoton(form, "btnBorrarPrograma").Boton.Enabled);

            var creacionesAntes = factory.CreateCount;
            Assert.IsFalse(form.EjecutarRecalculoPrograma());
            Assert.IsFalse(form.EjecutarGenerarPrograma());
            Assert.AreEqual(creacionesAntes, factory.CreateCount,
                "Bloqueado: no se reintenta la lectura ni la operación.");
            Assert.IsTrue(form.RequiereLecturaFresca);
            Assert.IsTrue(mensajes.Count > 0);

            // La única salida es la recarga completa (visible): libera el bloqueo.
            var (_, recargar) = LocalizarBoton(form, "btnRecargar");
            Assert.IsTrue(recargar.Visible && recargar.Enabled);
            recargar.PerformClick();

            Assert.IsFalse(form.RequiereLecturaFresca);
            Assert.IsFalse(form.HistorialUndo.RequiresReconciliation);
            Assert.IsFalse(form.GridPrincipal.ReadOnly);
            Assert.IsNotNull(form.ProgramaActual);
            Assert.AreEqual(0, form.HistorialUndo.UndoCount);
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    [TestMethod]
    public void ResultadoIncierto_BloqueaTodaMutacionYSoloRecargarRealReconcilia()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            var proyectoId = GenerarPrograma(dbPath, proyecto.Id);
            var direccion = SembrarFormato(dbPath, proyectoId);

            var factory = new DisposeThrowingOnceFactory(dbPath);
            form = new FormProgramaObra(ctx, proyecto, factory);
            Mostrar(form);
            EmpujarHistorial(form);
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo);
            Assert.AreEqual(1, form.ObtenerEstilosFilaUsuario().Count);

            var mensajes = new List<string>();
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);

            LocalizarBoton(form, "btnSincronizar").Boton.PerformClick();

            // El commit ocurrió pero el cierre no se pudo verificar → incierto.
            Assert.IsTrue(form.ProgramaOperacionIncierta);
            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation);
            Assert.IsFalse(form.HistorialUndo.CanUndo);
            Assert.AreEqual(2, form.HistorialUndo.UndoCount, "El historial se conserva, no se reintenta.");
            Assert.IsTrue(form.GridPrincipal.ReadOnly, "La edición del grid queda bloqueada.");
            Assert.IsFalse(LocalizarBoton(form, "btnSincronizar").Boton.Enabled);
            Assert.IsFalse(LocalizarBoton(form, "btnRecalcular").Boton.Enabled);
            Assert.IsFalse(LocalizarBoton(form, "btnReconstruir").Boton.Enabled);
            Assert.IsFalse(LocalizarBoton(form, "btnBorrarPrograma").Boton.Enabled);

            // Reintentar una acción no abre contexto ni cambia el estado incierto.
            var creacionesAntes = factory.CreateCount;
            Assert.IsFalse(form.EjecutarRecalculoPrograma());
            Assert.IsFalse(form.EjecutarGenerarPrograma());
            Assert.IsFalse(form.AplicarTipoPeriodoPrograma(TipoPeriodoPrograma.Mes));
            Assert.AreEqual(creacionesAntes, factory.CreateCount,
                "Bloqueado: no se crea un contexto nuevo para reintentar.");
            Assert.IsTrue(form.ProgramaOperacionIncierta);

            // El botón Recargar es visible y es la ÚNICA salida: lectura fresca completa.
            var (_, recargar) = LocalizarBoton(form, "btnRecargar");
            Assert.IsTrue(recargar.Visible);
            Assert.IsTrue(recargar.Enabled, "Recargar permanece disponible durante el bloqueo.");
            recargar.PerformClick();

            Assert.IsFalse(form.ProgramaOperacionIncierta, "La lectura fresca libera el bloqueo.");
            Assert.IsFalse(form.HistorialUndo.RequiresReconciliation);
            Assert.AreEqual(0, form.HistorialUndo.UndoCount);
            Assert.IsNotNull(form.ProgramaActual, "El programa se republica desde la base.");
            Assert.AreEqual(1, form.ObtenerEstilosFilaUsuario().Count,
                "Los estilos de fila se releen en la misma lectura completa.");
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo);
            Assert.IsFalse(form.GridPrincipal.ReadOnly);
            Assert.IsTrue(LocalizarBoton(form, "btnSincronizar").Boton.Enabled);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    // ══════════════════ (d) Lectura fresca incompleta ══════════════════

    [TestMethod]
    public void RecargaDuranteBloqueo_EsSoloLecturaYNoEjecutaLoadProgramLegacy()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            var proyectoId = GenerarPrograma(dbPath, proyecto.Id);
            SembrarFormato(dbPath, proyectoId);

            var factory = new DisposeThrowingOnceFactory(dbPath);
            form = new FormProgramaObra(ctx, proyecto, factory);
            Mostrar(form);
            form.Notificador.Mostrar = (_, _) => { };
            LocalizarBoton(form, "btnSincronizar").Boton.PerformClick();
            Assert.IsTrue(form.ProgramaOperacionIncierta);

            // Cualquier ESCRITURA sobre actividades fallaría: si la recarga usara la
            // ruta legacy (LoadProgramById → RecalculateProgram → SaveChanges) el
            // trigger la abortaría. La reconciliación debe ser de SÓLO LECTURA.
            ctx.Database.ExecuteSqlRaw(
                "CREATE TRIGGER trg_no_write_recarga BEFORE UPDATE ON \"ActividadesProgramadas\" " +
                "BEGIN SELECT RAISE(ABORT, 'escritura_prohibida_en_recarga'); END;");

            LocalizarBoton(form, "btnRecargar").Boton.PerformClick();

            Assert.IsFalse(form.ProgramaOperacionIncierta,
                "La lectura fresca de sólo lectura debe reconciliar sin escribir.");
            Assert.IsFalse(form.HistorialUndo.RequiresReconciliation);
            Assert.IsNotNull(form.ProgramaActual);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    [TestMethod]
    public void RecargaFrescaConFalloDePrograma_ConservaCachesHistorialYBloqueo()
    {
        VerificarFalloDeLecturaFresca("DROP TABLE \"DependenciasActividad\"");
    }

    [TestMethod]
    public void RecargaFrescaConFalloDeEstilos_ConservaCachesHistorialYBloqueo()
    {
        VerificarFalloDeLecturaFresca("DROP TABLE \"ConfiguracionesEstiloFilaReporte\"");
    }

    private static void VerificarFalloDeLecturaFresca(string sentenciaFallo)
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            var proyectoId = GenerarPrograma(dbPath, proyecto.Id);
            var direccion = SembrarFormato(dbPath, proyectoId);

            var factory = new DisposeThrowingOnceFactory(dbPath);
            form = new FormProgramaObra(ctx, proyecto, factory);
            Mostrar(form);
            EmpujarHistorial(form);
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo);
            Assert.AreEqual(1, form.ObtenerEstilosFilaUsuario().Count);

            form.Notificador.Mostrar = (_, _) => { };
            LocalizarBoton(form, "btnSincronizar").Boton.PerformClick();
            Assert.IsTrue(form.ProgramaOperacionIncierta);

            var programaAntes = form.ProgramaActual;
            var overlayAntes = form.ObtenerOverlayActual(direccion);

            // La lectura conjunta falla (programa o estilos): no se publica nada PARCIAL.
            ctx.Database.ExecuteSqlRaw(sentenciaFallo);
            LocalizarBoton(form, "btnRecargar").Boton.PerformClick();

            Assert.IsTrue(form.ProgramaOperacionIncierta,
                "Sin lectura completa se conserva el bloqueo.");
            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation);
            Assert.AreEqual(2, form.HistorialUndo.UndoCount,
                "El historial se conserva: no se descarta nada a medias.");
            Assert.AreSame(programaAntes, form.ProgramaActual,
                "No se reemplaza el programa con una lectura incompleta.");
            Assert.IsTrue(form.TieneCachesFormatoCargadas,
                "Las cachés anteriores no se pisan con un estado parcial.");
            Assert.AreEqual(overlayAntes, form.ObtenerOverlayActual(direccion));
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    // ══════════════════ (e) Commits y no-op ══════════════════

    [TestMethod]
    public void SincronizarConfirmado_DescartaSoloProgramaYConservaFormato()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            var proyectoId = GenerarPrograma(dbPath, proyecto.Id);
            var direccion = SembrarFormato(dbPath, proyectoId);

            form = new FormProgramaObra(ctx, proyecto);
            Mostrar(form);
            EmpujarHistorial(form);
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo);

            form.Notificador.Mostrar = (_, _) => { };
            LocalizarBoton(form, "btnSincronizar").Boton.PerformClick();

            Assert.IsFalse(form.ProgramaOperacionIncierta);
            Assert.IsNotNull(form.ProgramaActual, "La publicación usa una lectura fresca.");
            Assert.AreEqual(1, form.HistorialUndo.UndoCount,
                "Sólo se descarta el historial de PROGRAMA; el de formato se conserva.");
            Assert.IsTrue(form.HistorialUndo.CanUndo);
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo,
                "Las cachés de formato no se invalidan en un commit de programa.");
            Assert.AreEqual(1, form.ObtenerEstilosFilaUsuario().Count);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    [TestMethod]
    public void ReconstruirConfirmado_DescartaSoloProgramaYRepublicaDesdeBase()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            var proyectoId = GenerarPrograma(dbPath, proyecto.Id);
            var direccion = SembrarFormato(dbPath, proyectoId);

            form = new FormProgramaObra(ctx, proyecto);
            Mostrar(form);
            EmpujarHistorial(form);
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo,
                "Caché cargada: reconstruir no debe invalidarla.");
            form.Notificador.Mostrar = (_, _) => { };
            form.Notificador.Confirmar = _ => true;

            LocalizarBoton(form, "btnReconstruir").Boton.PerformClick();

            Assert.IsFalse(form.ProgramaOperacionIncierta);
            Assert.IsNotNull(form.ProgramaActual);
            Assert.AreEqual(1, form.HistorialUndo.UndoCount);
            Assert.IsTrue(form.TieneCachesFormatoCargadas,
                "El formato no se invalida al reconstruir el programa.");
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo);

            using var verif = new SOPROContext(dbPath);
            Assert.AreEqual(1, verif.ProgramasObra.Count(p => p.ProyectoId == proyectoId && p.Activo),
                "Reconstruir deja exactamente un programa activo.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    [TestMethod]
    public void GenerarSinCambios_NoInvalidaCachesNiHistorial()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            var proyectoId = GenerarPrograma(dbPath, proyecto.Id);
            var direccion = SembrarFormato(dbPath, proyectoId);

            form = new FormProgramaObra(ctx, proyecto);
            form.CargarPrograma();
            EmpujarHistorial(form);
            var programaAntes = form.ProgramaActual;
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo);

            form.Notificador.Mostrar = (_, _) => { };

            // Ya existe el programa esperado: el comando confirma SIN CAMBIOS.
            Assert.IsFalse(form.EjecutarGenerarPrograma(), "Un no-op confirmado no publica recarga.");
            Assert.AreSame(programaAntes, form.ProgramaActual);
            Assert.AreEqual(2, form.HistorialUndo.UndoCount,
                "Sin cambios no se descarta ninguna familia.");
            Assert.IsFalse(form.ProgramaOperacionIncierta);
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo);
            Assert.AreEqual(1, form.ObtenerEstilosFilaUsuario().Count);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    // ══════════════════ (f) Recálculo y tipo de período ══════════════════

    [TestMethod]
    public void RecalcularPrograma_UsaRutaAtomicaYUnConflictoNoRecarga()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            var proyectoId = GenerarPrograma(dbPath, proyecto.Id);
            var direccion = SembrarFormato(dbPath, proyectoId);

            form = new FormProgramaObra(ctx, proyecto);
            form.CargarPrograma();
            EmpujarHistorial(form);
            form.Notificador.Mostrar = (_, _) => { };

            Assert.IsTrue(form.EjecutarRecalculoPrograma(), "El recálculo confirmado publica lectura fresca.");
            Assert.IsNotNull(form.ProgramaActual);
            Assert.AreEqual(1, form.HistorialUndo.UndoCount,
                "El recálculo descarta sólo el historial de programa.");
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo);

            var programaAntes = form.ProgramaActual;
            ReemplazarProgramaActivo(dbPath, proyectoId);

            Assert.IsFalse(form.EjecutarRecalculoPrograma(), "El conflicto no recarga.");
            Assert.AreSame(programaAntes, form.ProgramaActual);
            Assert.AreEqual(1, form.HistorialUndo.UndoCount);
            Assert.IsFalse(form.HistorialUndo.RequiresReconciliation);
            Assert.AreEqual(1, form.ObtenerEstilosFilaUsuario().Count);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    [TestMethod]
    public void TipoPeriodo_UsaRutaAtomicaYUnConflictoRestauraElCombo()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            var proyectoId = GenerarPrograma(dbPath, proyecto.Id);
            SembrarFormato(dbPath, proyectoId);

            form = new FormProgramaObra(ctx, proyecto);
            form.CargarPrograma();
            EmpujarHistorial(form);
            form.Notificador.Mostrar = (_, _) => { };

            Assert.IsTrue(form.AplicarTipoPeriodoPrograma(TipoPeriodoPrograma.Mes));
            Assert.AreEqual(TipoPeriodoPrograma.Mes, form.ProgramaActual!.TipoPeriodo);
            Assert.AreEqual(1, form.HistorialUndo.UndoCount,
                "El cambio de período descarta sólo el historial de programa.");

            using (var verif = new SOPROContext(dbPath))
            {
                var programa = verif.ProgramasObra.Single(p => p.Activo);
                Assert.AreEqual(TipoPeriodoPrograma.Mes, programa.TipoPeriodo);
                Assert.AreEqual(30, programa.DuracionPeriodoDias,
                    "La ruta atómica actualiza tipo y duración en el mismo commit.");
            }

            // Otra ventana reemplaza el activo: el cambio de tipo debe conflictuar.
            ReemplazarProgramaActivo(dbPath, proyectoId);
            var programaAntes = form.ProgramaActual;

            Assert.IsFalse(form.AplicarTipoPeriodoPrograma(TipoPeriodoPrograma.Dia));
            Assert.AreSame(programaAntes, form.ProgramaActual);
            Assert.IsFalse(form.ProgramaOperacionIncierta);
            Assert.AreEqual(1, form.HistorialUndo.UndoCount);
            Assert.AreEqual(TipoPeriodoPrograma.Mes, form.ProgramaActual!.TipoPeriodo,
                "El combo/DTO vuelve al tipo publicado.");

            using var verifFinal = new SOPROContext(dbPath);
            var activoNuevo = verifFinal.ProgramasObra.Single(p => p.Activo);
            Assert.AreEqual(TipoPeriodoPrograma.Semana, activoNuevo.TipoPeriodo,
                "El conflicto no escribió el tipo sobre el programa de otra ventana.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    // ═══════════════════════════ Escenario ═══════════════════════════

    private static string CrearRuta()
        => Path.Combine(Path.GetTempPath(), $"sopro_po_ciclo_{Guid.NewGuid():N}.db");

    /// <summary>
    /// Muestra el formulario y procesa el evento Load (que ejecuta la carga inicial
    /// REAL del módulo). Se llama ANTES de preparar triggers/tablas rotas para que
    /// ningún escenario destructivo afecte a esa carga normal.
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
            Nombre = "Proyecto ciclo programa",
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

    private static ReportCellAddress SembrarFormato(string dbPath, int proyectoId)
    {
        using var ctx = new SOPROContext(dbPath);

        ctx.FormatosCeldaReporte.Add(new FormatoCeldaReporte
        {
            ProyectoId = proyectoId,
            TipoReporte = TipoProgramaObra,
            ClaveVista = string.Empty,
            ClaveFila = "ACT:1",
            NombreColumna = "colImporte",
            ColorFondo = "#111111",
        });

        ctx.ConfiguracionesEstiloFilaReporte.Add(new ConfiguracionEstiloFilaReporte
        {
            ProyectoId = proyectoId,
            TipoReporte = TipoProgramaObra,
            RolFila = "Total",
            Negrita = true,
        });

        ctx.FormatosCeldaReporte.Add(new FormatoCeldaReporte
        {
            ProyectoId = proyectoId,
            TipoReporte = TipoPresupuesto,
            ClaveVista = string.Empty,
            ClaveFila = "1",
            NombreColumna = "colImporte",
            ColorFondo = "#ABCDEF",
        });

        ctx.SaveChanges();
        return new ReportCellAddress(string.Empty, "ACT:1", "colImporte");
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

    /// <summary>Otra ventana publica un programa activo distinto del esperado.</summary>
    private static void ReemplazarProgramaActivo(string dbPath, int proyectoId)
    {
        using var ctx = new SOPROContext(dbPath);
        foreach (var activo in ctx.ProgramasObra.Where(p => p.ProyectoId == proyectoId && p.Activo))
            activo.Activo = false;

        ctx.ProgramasObra.Add(new ProgramaObra
        {
            ProyectoId = proyectoId,
            Nombre = "Programa de otra ventana",
            FechaInicioPrograma = new DateTime(2026, 1, 5),
            FechaFinPrograma = new DateTime(2026, 1, 16),
            TipoPeriodo = TipoPeriodoPrograma.Semana,
            DuracionPeriodoDias = 7,
            Activo = true,
        });
        ctx.SaveChanges();
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

    // ═══════════════════════ Seam: resultado incierto ═══════════════════════

    /// <summary>
    /// Simula el commit ya realizado seguido de un fallo al cerrar el contexto (sólo en
    /// el SEGUNDO cierre: el primero es la carga inicial del formulario, que ahora es
    /// una lectura fresca de sólo lectura). El ejecutor lo clasifica como
    /// OutcomeUnknown; las lecturas frescas posteriores cierran normalmente.
    /// </summary>
    private sealed class DisposeThrowingOnceFactory : IProjectDbContextFactory
    {
        private readonly string _dbPath;
        private int _createCount;
        private int _disposeCount;

        public DisposeThrowingOnceFactory(string dbPath) => _dbPath = dbPath;

        public int CreateCount => Volatile.Read(ref _createCount);

        public SOPROContext Create(string databasePath)
        {
            Interlocked.Increment(ref _createCount);
            return new DisposeThrowingContext(_dbPath, () => Interlocked.Increment(ref _disposeCount) == 2);
        }

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
    /// Fábrica que falla al crear el contexto de la TERCERA operación: la primera es la
    /// carga inicial del formulario, la segunda el comando y la tercera la lectura
    /// fresca posterior al commit. El comando y la recarga manual usan contextos
    /// reales, de modo que se distingue «commit conocido + vista obsoleta» de
    /// «persistencia incierta».
    /// </summary>
    private sealed class ReadFailureOnFreshReadFactory : IProjectDbContextFactory
    {
        private readonly string _dbPath;
        private int _createCount;

        public ReadFailureOnFreshReadFactory(string dbPath) => _dbPath = dbPath;

        public int CreateCount => Volatile.Read(ref _createCount);

        public SOPROContext Create(string databasePath)
        {
            var numero = Interlocked.Increment(ref _createCount);
            if (numero == 3)
                throw new InvalidOperationException("lectura_fresca_simulada_fallida");

            return new SOPROContext(_dbPath);
        }

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }
}
