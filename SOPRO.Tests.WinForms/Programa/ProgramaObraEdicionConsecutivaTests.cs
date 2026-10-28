using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Programa;

/// <summary>
/// Regresión de la PR #49: dos ediciones consecutivas de días hábiles en actividades
/// distintas, con recálculo/regeneración de periodos y recarga entre ambas, deben
/// persistir sin <c>DbUpdateConcurrencyException</c>. El guardado usa un contexto
/// CORTO; la recarga publica una lectura fresca de sólo lectura (nunca el tracker
/// compartido, que conservaría distribuciones/periodos con IDs ya reemplazados).
/// También cubre undo → edición por el mismo camino. Formulario REAL sobre SQLite
/// temporal; nunca datos del usuario y sin diálogos modales.
/// </summary>
[STATestClass]
[DoNotParallelize] // Construye el formulario real del módulo (controles WinForms).
public class ProgramaObraEdicionConsecutivaTests
{
    [TestMethod]
    public void DosEdicionesConsecutivasDeDuracion5EnActividadesDistintas_ConRecargaEntreEllas_PersistenSinConcurrencia()
    {
        const int duracionDias = 5;
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            var programaId = GenerarPrograma(dbPath, proyecto.Id);

            form = new FormProgramaObra(ctx, proyecto);
            // Inyectado ANTES de mostrar/cargar: ninguna ruta (incluida la carga inicial)
            // puede abrir un diálogo modal en el hilo STA del test.
            var mensajes = new List<string>();
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);
            Mostrar(form);

            var programaInicial = form.ProgramaActual;
            Assert.IsNotNull(programaInicial, "Precondición: el programa base está cargado.");

            var actividades = programaInicial!.Actividades
                .Where(a => !a.EsResumen)
                .OrderBy(a => a.Orden)
                .ThenBy(a => a.Id)
                .ToList();
            Assert.IsTrue(actividades.Count >= 2, "Se necesitan al menos dos actividades hoja.");

            var primera = actividades[0];
            var segunda = actividades[1];
            Assert.AreNotEqual(duracionDias, primera.DuracionDiasHabiles,
                "Precondición: la primera actividad no tiene ya la duración a capturar.");
            Assert.AreNotEqual(duracionDias, segunda.DuracionDiasHabiles,
                "Precondición: la segunda actividad no tiene ya la duración a capturar.");

            // ── Edición 1: duración de la primera actividad ──
            Assert.IsTrue(
                form.EditarDuracionDiasActividad(BuscarFila(form, primera.Id), duracionDias),
                "La primera edición debe completar su operación asíncrona.");
            AfirmarEstadoVerificado(form);
            Assert.AreEqual(duracionDias, BuscarActividad(form, primera.Id).DuracionDiasHabiles,
                "La vista republicada refleja la primera edición.");

            // La recarga entre ediciones reemplaza el programa publicado (lectura fresca).
            Assert.AreNotSame(programaInicial, form.ProgramaActual,
                "Cada edición publica una lectura fresca, no reutiliza el DTO anterior.");

            // ── Edición 2: duración de la segunda actividad, sobre la vista ya recargada ──
            Assert.IsTrue(
                form.EditarDuracionDiasActividad(BuscarFila(form, segunda.Id), duracionDias),
                "La segunda edición debe completar su operación asíncrona.");
            AfirmarEstadoVerificado(form);
            Assert.AreEqual(duracionDias, BuscarActividad(form, segunda.Id).DuracionDiasHabiles,
                "La vista republicada refleja la segunda edición.");

            // ── Verificación contra la base (contexto independiente) ──
            using var verif = new SOPROContext(dbPath);

            var act1 = verif.ActividadesProgramadas.Single(a => a.Id == primera.Id);
            var act2 = verif.ActividadesProgramadas.Single(a => a.Id == segunda.Id);
            Assert.AreEqual(duracionDias, act1.DuracionDiasHabiles,
                "La primera edición sigue persistida después de la segunda.");
            Assert.AreEqual(duracionDias, act2.DuracionDiasHabiles,
                "La segunda edición se persistió (sin DbUpdateConcurrencyException).");

            var periodosVigentes = verif.PeriodosPrograma
                .Where(p => p.ProgramaObraId == programaId)
                .Select(p => p.Id)
                .ToHashSet();
            Assert.IsTrue(periodosVigentes.Count > 0, "La regeneración de periodos dejó periodos vigentes.");

            VerificarDistribuciones(verif, act1, periodosVigentes);
            VerificarDistribuciones(verif, act2, periodosVigentes);

            // El rango del programa queda consistente con las fechas persistidas.
            var hojasConFechas = verif.ActividadesProgramadas
                .Where(a => a.ProgramaObraId == programaId && !a.EsResumen)
                .ToList();
            var programa = verif.ProgramasObra.Single(p => p.Id == programaId);
            Assert.AreEqual(
                hojasConFechas.Where(a => a.FechaInicioProgramada.HasValue)
                    .Min(a => a.FechaInicioProgramada!.Value.Date),
                programa.FechaInicioPrograma.Date,
                "El rango del programa se regenera con la fecha mínima de las actividades.");
            Assert.AreEqual(
                hojasConFechas.Where(a => a.FechaFinProgramada.HasValue)
                    .Max(a => a.FechaFinProgramada!.Value.Date),
                programa.FechaFinPrograma!.Value.Date,
                "El rango del programa se regenera con la fecha máxima de las actividades.");
            Assert.AreEqual(0, mensajes.Count,
                "Una escritura correcta no debe producir mensajes (ni modales ni capturados).");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    [TestMethod]
    public void UndoDeUnaEdicionYLuegoEdicionDeOtraActividad_UsanElMismoCaminoFrescoSinConcurrencia()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            var programaId = GenerarPrograma(dbPath, proyecto.Id);

            form = new FormProgramaObra(ctx, proyecto);
            // Inyectado ANTES de mostrar/cargar: ninguna ruta (incluida la carga inicial)
            // puede abrir un diálogo modal en el hilo STA del test.
            var mensajes = new List<string>();
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);
            Mostrar(form);

            var actividades = form.ProgramaActual!.Actividades
                .Where(a => !a.EsResumen)
                .OrderBy(a => a.Orden)
                .ThenBy(a => a.Id)
                .ToList();
            var primera = actividades[0];
            var segunda = actividades[1];
            var inicio1 = primera.FechaInicioProgramada!.Value;
            var nueva1 = SiguienteDiaHabil(ctx, programaId, inicio1);
            var nueva2 = SiguienteDiaHabil(ctx, programaId, segunda.FechaInicioProgramada!.Value);

            Assert.IsTrue(form.EditarFechaActividad(BuscarFila(form, primera.Id), "colFechaInicio", nueva1));
            AfirmarEstadoVerificado(form);
            Assert.IsTrue(form.HistorialUndo.CanUndo, "La edición confirmada deja una acción en el historial.");

            // Undo real (misma ruta que Ctrl+Z): reaprovecha el guardado con contexto corto.
            Assert.IsTrue(form.EjecutarUndoPrograma(), "El undo debe ejecutarse.");
            AfirmarEstadoVerificado(form);

            using (var verifUndo = new SOPROContext(dbPath))
            {
                var act1 = verifUndo.ActividadesProgramadas.Single(a => a.Id == primera.Id);
                Assert.AreEqual(inicio1.Date, act1.FechaInicioProgramada!.Value.Date,
                    "El undo restaura el valor persistido por el mismo camino fresco.");
            }

            // Una edición posterior a un undo no debe encontrar el tracker envenenado.
            Assert.IsTrue(form.EditarFechaActividad(BuscarFila(form, segunda.Id), "colFechaInicio", nueva2));
            AfirmarEstadoVerificado(form);

            using var verif = new SOPROContext(dbPath);
            var act2 = verif.ActividadesProgramadas.Single(a => a.Id == segunda.Id);
            Assert.AreEqual(nueva2.Date, act2.FechaInicioProgramada!.Value.Date);

            var periodosVigentes = verif.PeriodosPrograma
                .Where(p => p.ProgramaObraId == programaId)
                .Select(p => p.Id)
                .ToHashSet();
            VerificarDistribuciones(verif, act2, periodosVigentes);
            Assert.AreEqual(0, mensajes.Count,
                "Una edición y un undo correctos no deben producir mensajes.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    // ═══════════════════════════ Verificaciones ═══════════════════════════

    /// <summary>
    /// Mientras una edición está en su fase asíncrona, el BeginEdit real de actividades
    /// Y de dependencias debe cancelarse: antes se permitía iniciar la edición y el
    /// <c>CellEndEdit</c> posterior la descartaba en silencio. La operación en curso
    /// termina y persiste; el intento concurrente no altera el dato.
    /// </summary>
    [TestMethod]
    public void EdicionConcurrenteDuranteOperacionEnCurso_SeCancelaYNoSePierdeLaEdicionEnCurso()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            GenerarPrograma(dbPath, proyecto.Id);

            form = new FormProgramaObra(ctx, proyecto);
            var mensajes = new List<string>();
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);
            Mostrar(form);

            var actividades = form.ProgramaActual!.Actividades
                .Where(a => !a.EsResumen)
                .OrderBy(a => a.Orden)
                .ThenBy(a => a.Id)
                .ToList();
            Assert.IsTrue(actividades.Count >= 2, "Se necesitan dos actividades hoja.");

            var primera = actividades[0];
            var segunda = actividades[1];
            var duracionOriginalSegunda = segunda.DuracionDiasHabiles;

            // Inicia la edición de la PRIMERA sin esperar: el worker queda activo.
            Assert.IsTrue(
                form.IniciarEdicionDuracionDiasActividad(BuscarFila(form, primera.Id), 5),
                "La edición debe arrancar.");
            Assert.IsTrue(form.OperacionProgramaEnCurso,
                "Precondición: hay una operación de edición en curso.");

            // Intento concurrente sobre OTRA celda del grid de actividades.
            var colDuracion = form.GridPrincipal.Columns["colDuracionDias"].Index;
            Assert.IsFalse(
                form.IntentarEditarCeldaActividad(BuscarFila(form, segunda.Id), colDuracion),
                "El BeginEdit de actividades debe cancelarse mientras hay una operación en curso.");
            Assert.IsFalse(
                form.IntentarEditarCeldaActividad(
                    BuscarFila(form, segunda.Id),
                    form.GridPrincipal.Columns["colFechaInicio"].Index),
                "Cualquier celda de actividades debe rechazarse mientras hay una operación en curso.");
            Assert.IsFalse(
                form.IntentarEditarCeldaDependencia(0, 0),
                "El BeginEdit de dependencias debe cancelarse mientras hay una operación en curso.");

            // El rechazo es visible, no un descarte silencioso, y no alteró el dato.
            StringAssert.Contains(TextoEstado(form), "Espere",
                "La cancelación debe informar que hay una operación en curso.");
            Assert.AreEqual(duracionOriginalSegunda, BuscarActividad(form, segunda.Id).DuracionDiasHabiles,
                "El intento concurrente no debe modificar el dato enlazado.");

            // La edición en curso termina y persiste; la segunda actividad queda intacta.
            Assert.IsTrue(form.EsperarOperacionPrograma(), "La operación en curso debe terminar.");
            AfirmarEstadoVerificado(form);
            Assert.AreEqual(5, BuscarActividad(form, primera.Id).DuracionDiasHabiles);

            using var verif = new SOPROContext(dbPath);
            Assert.AreEqual(5, verif.ActividadesProgramadas.Single(a => a.Id == primera.Id).DuracionDiasHabiles,
                "La edición en curso se persiste.");
            Assert.AreEqual(duracionOriginalSegunda,
                verif.ActividadesProgramadas.Single(a => a.Id == segunda.Id).DuracionDiasHabiles,
                "La edición concurrente no se aplicó ni se perdió en silencio.");
            Assert.AreEqual(0, mensajes.Count,
                "Una edición correcta no debe producir mensajes (ni modales ni capturados).");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    /// <summary>
    /// Mientras hay una edición en curso, Undo/Redo deben rechazarse ANTES de llamar a
    /// <c>UndoManager</c>: no mueven stacks ni tocan DTO/DB. La edición en curso
    /// termina y persiste.
    /// </summary>
    [TestMethod]
    public void UndoYRedoDuranteOperacionEnCurso_SeRechazanSinMoverStacksNiDatos()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            GenerarPrograma(dbPath, proyecto.Id);

            form = new FormProgramaObra(ctx, proyecto);
            var mensajes = new List<string>();
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);
            Mostrar(form);

            var actividades = form.ProgramaActual!.Actividades
                .Where(a => !a.EsResumen)
                .OrderBy(a => a.Orden)
                .ThenBy(a => a.Id)
                .ToList();
            Assert.IsTrue(actividades.Count >= 2, "Se necesitan dos actividades hoja.");
            var primera = actividades[0];
            var segunda = actividades[1];
            var duracionOriginalSegunda = segunda.DuracionDiasHabiles;

            Assert.IsTrue(form.IniciarEdicionDuracionDiasActividad(BuscarFila(form, primera.Id), 5));
            Assert.IsTrue(form.OperacionProgramaEnCurso,
                "Precondición: la edición está en su fase asíncrona.");

            var undoAntes = form.HistorialUndo.UndoCount;
            var redoAntes = form.HistorialUndo.RedoCount;
            Assert.IsTrue(undoAntes >= 1, "La edición en curso ya registró su acción en el historial.");

            Assert.IsFalse(form.IntentarUndoPrograma(),
                "Undo debe rechazarse mientras hay una operación en curso.");
            Assert.IsFalse(form.IntentarRedoPrograma(),
                "Redo debe rechazarse mientras hay una operación en curso.");
            StringAssert.Contains(TextoEstado(form), "Espere",
                "El rechazo del historial debe ser visible mientras hay una operación en curso.");

            Assert.AreEqual(undoAntes, form.HistorialUndo.UndoCount,
                "El Undo rechazado no debe mover el stack.");
            Assert.AreEqual(redoAntes, form.HistorialUndo.RedoCount,
                "El Redo rechazado no debe mover el stack.");
            Assert.AreEqual(duracionOriginalSegunda, BuscarActividad(form, segunda.Id).DuracionDiasHabiles,
                "El historial rechazado no debe modificar DTOs ajenos a la edición en curso.");

            Assert.IsTrue(form.EsperarOperacionPrograma(), "La edición en curso debe terminar.");
            AfirmarEstadoVerificado(form);
            Assert.AreEqual(5, BuscarActividad(form, primera.Id).DuracionDiasHabiles);

            using var verif = new SOPROContext(dbPath);
            Assert.AreEqual(5, verif.ActividadesProgramadas.Single(a => a.Id == primera.Id).DuracionDiasHabiles,
                "La edición en curso se persiste tras el rechazo de Undo/Redo.");
            Assert.AreEqual(duracionOriginalSegunda,
                verif.ActividadesProgramadas.Single(a => a.Id == segunda.Id).DuracionDiasHabiles);
            Assert.AreEqual(0, mensajes.Count,
                "El rechazo de historial no debe producir mensajes (ni modales ni capturados).");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    /// <summary>
    /// Write rechazado por validación (<c>Ok=false</c>: nada se escribió) seguido de una
    /// lectura fresca fallida: el mensaje NO puede afirmar «se guardó», la vista queda
    /// bloqueada y la base intacta; «Recargar» reconcilia en sólo lectura.
    /// </summary>
    [TestMethod]
    public void EscrituraRechazadaConLecturaFrescaFallida_NoAfirmaQueSeGuardoYMantieneLockSinEscribir()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            GenerarPrograma(dbPath, proyecto.Id);

            // Creación 1 = carga inicial; 2 = contexto del guardado (rechazado, no escribe);
            // 3 = lectura fresca posterior (falla). La reconciliación manual usa la 4ª.
            var factory = new FalloEnLecturaFrescaTrasEscrituraRechazadaFactory(dbPath);
            form = new FormProgramaObra(ctx, proyecto, factory);
            var mensajes = new List<string>();
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);
            Mostrar(form);

            var primera = form.ProgramaActual!.Actividades
                .Where(a => !a.EsResumen)
                .OrderBy(a => a.Orden)
                .ThenBy(a => a.Id)
                .First();
            var inicioOriginal = primera.FechaInicioProgramada!.Value.Date;
            var fechaInhabil = SiguienteDiaInhabil(inicioOriginal);

            // Fecha inhábil: SaveActivity la rechaza en validación (Ok=false) antes de escribir.
            Assert.IsTrue(
                form.EditarFechaActividad(BuscarFila(form, primera.Id), "colFechaInicio", fechaInhabil),
                "La edición debe completar su ciclo (guardado rechazado + recarga fallida).");

            Assert.IsTrue(mensajes.Any(m => m.Contains("inhábil", StringComparison.OrdinalIgnoreCase)),
                "El rechazo de validación se informa. Mensajes: " + string.Join(" | ", mensajes));
            Assert.IsTrue(
                mensajes.Any(m => m.Contains("fue rechazada")
                    && m.Contains("no se pudo refrescar la vista")),
                "El fallo de lectura posterior debe clasificarse como operación rechazada. Mensajes: "
                    + string.Join(" | ", mensajes));
            Assert.IsFalse(
                mensajes.Any(m => m.Contains("se guardó")),
                "Con write rechazado (Ok=false) no puede afirmarse que se guardó.");
            Assert.IsFalse(
                mensajes.Any(m => m.Contains("actualizada.", StringComparison.OrdinalIgnoreCase)),
                "Un rechazo nunca debe informar éxito.");

            Assert.IsFalse(form.ProgramaOperacionIncierta,
                "Un rechazo de validación no es un resultado incierto de persistencia.");
            Assert.IsTrue(form.RequiereLecturaFresca,
                "La lectura fresca fallida mantiene el bloqueo.");
            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation);
            Assert.IsTrue(form.GridPrincipal.ReadOnly);
            Assert.IsNotNull(form.UltimoErrorLecturaFresca);
            StringAssert.Contains(form.UltimoErrorLecturaFresca!, "lectura_fresca_simulada_fallida");

            // Nada se escribió: la actividad conserva su fecha original.
            using (var verif = new SOPROContext(dbPath))
            {
                Assert.AreEqual(inicioOriginal,
                    verif.ActividadesProgramadas.Single(a => a.Id == primera.Id).FechaInicioProgramada!.Value.Date,
                    "Un write rechazado no modifica la base.");
            }

            // La recuperación es de SÓLO LECTURA: cualquier DML del programa aborta.
            ctx.Database.ExecuteSqlRaw(SqlBloqueoEscrituraPrograma);

            var recargar = LocalizarBoton(form, "btnRecargar").Boton;
            Assert.IsTrue(recargar.Enabled, "«Recargar» permanece disponible con la vista bloqueada.");
            recargar.PerformClick();

            Assert.IsFalse(form.RequiereLecturaFresca, "La lectura completa libera el bloqueo.");
            Assert.IsFalse(form.HistorialUndo.RequiresReconciliation);
            Assert.IsFalse(form.GridPrincipal.ReadOnly);
            Assert.IsNotNull(form.ProgramaActual);

            using var verifFinal = new SOPROContext(dbPath);
            Assert.AreEqual(inicioOriginal,
                verifFinal.ActividadesProgramadas.Single(a => a.Id == primera.Id).FechaInicioProgramada!.Value.Date,
                "La recuperación no escribe ni aplica el valor rechazado.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    /// <summary>
    /// Lectura fresca fallida DESPUÉS de un commit confirmado (regresión del diálogo
    /// «La operación se guardó, pero no se pudo releer el programa…»): la vista debe
    /// quedar bloqueada, sin reintentar la lectura por su cuenta y sin escribir nada;
    /// el valor ya confirmado permanece en la base y «Recargar» reconcilia en sólo
    /// lectura. Sólo SQLite temporal.
    /// </summary>
    [TestMethod]
    public void LecturaFrescaFallidaTrasEdicionConfirmada_ConservaBloqueoSinReintentarNiEscribir()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            var programaId = GenerarPrograma(dbPath, proyecto.Id);

            // Creación 1 = carga inicial, 2 = guardado con contexto corto, 3 = regeneración.
            // La 4ª (lectura fresca posterior al commit) falla.
            var factory = new FalloEnLecturaFrescaTrasEdicionFactory(dbPath);
            form = new FormProgramaObra(ctx, proyecto, factory);
            // Inyectado ANTES de cargar: ni la carga inicial ni el fallo inducido pueden
            // abrir un MessageBox modal en el hilo STA.
            var mensajes = new List<string>();
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);
            var fallbackAntes = NotificadorPrograma.VecesUsadoFallbackNoModal;
            Mostrar(form);

            var programaAntes = form.ProgramaActual;
            Assert.IsNotNull(programaAntes, "Precondición: programa cargado.");

            var primera = programaAntes!.Actividades
                .Where(a => !a.EsResumen)
                .OrderBy(a => a.Orden)
                .ThenBy(a => a.Id)
                .First();
            var nueva = SiguienteDiaHabil(ctx, programaId, primera.FechaInicioProgramada!.Value);

            Assert.IsTrue(
                form.EditarFechaActividad(BuscarFila(form, primera.Id), "colFechaInicio", nueva),
                "La edición debe completar su operación (aun cuando la lectura fresca falle).");

            // El commit se confirmó; lo que falló fue republicar la vista.
            Assert.IsFalse(form.ProgramaOperacionIncierta,
                "La persistencia se confirmó: no es un resultado incierto del comando.");
            Assert.IsTrue(form.RequiereLecturaFresca,
                "Sin lectura fresca la vista no es operable con estado verificado.");
            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation,
                "El historial no es confiable contra una vista obsoleta.");
            Assert.IsTrue(form.GridPrincipal.ReadOnly,
                "La edición de celdas queda bloqueada hasta reconciliar.");
            Assert.AreSame(programaAntes, form.ProgramaActual,
                "No se publica un programa sin verificar.");

            // La causa ya NO se descarta en Debug: queda expuesta (diagnóstico y pruebas).
            Assert.IsNotNull(form.UltimoErrorLecturaFresca,
                "Un fallo de lectura fresca debe registrar su causa.");
            StringAssert.Contains(form.UltimoErrorLecturaFresca!, "apertura del contexto de lectura");
            StringAssert.Contains(form.UltimoErrorLecturaFresca!, "lectura_fresca_simulada_fallida");
            Assert.IsTrue(
                mensajes.Any(m => m.Contains("no se pudo releer el programa")
                    && m.Contains("lectura_fresca_simulada_fallida")),
                "El usuario debe ver el motivo del fallo de lectura.");
            Assert.IsFalse(
                mensajes.Any(m => m.Contains("actualizada.", StringComparison.OrdinalIgnoreCase)),
                "Un fallo de recarga nunca debe informar éxito.");
            Assert.AreEqual(fallbackAntes, NotificadorPrograma.VecesUsadoFallbackNoModal,
                "El mensaje debe capturarse con el notificador inyectado: cero ruta modal en STA.");

            // El valor editado SÍ quedó persistido por el commit previo.
            using (var verif = new SOPROContext(dbPath))
            {
                var persistida = verif.ActividadesProgramadas.Single(a => a.Id == primera.Id);
                Assert.AreEqual(nueva.Date, persistida.FechaInicioProgramada!.Value.Date,
                    "El commit se confirmó antes del fallo de lectura.");
            }

            // Sin reintentos automáticos: no se abre otro contexto por su cuenta.
            var creaciones = factory.CreateCount;
            Assert.AreEqual(4, creaciones,
                "Carga inicial + guardado + regeneración + único intento de lectura fresca.");
            System.Windows.Forms.Application.DoEvents();
            Assert.AreEqual(creaciones, factory.CreateCount,
                "Un fallo de lectura fresca no se reintenta automáticamente.");

            // La recuperación es de SÓLO LECTURA: cualquier DML del programa aborta.
            ctx.Database.ExecuteSqlRaw(SqlBloqueoEscrituraPrograma);

            var recargar = LocalizarBoton(form, "btnRecargar").Boton;
            Assert.IsTrue(recargar.Enabled, "«Recargar» es la salida y permanece disponible.");
            recargar.PerformClick();

            Assert.IsFalse(form.RequiereLecturaFresca, "La lectura completa libera el bloqueo.");
            Assert.IsFalse(form.HistorialUndo.RequiresReconciliation);
            Assert.IsFalse(form.GridPrincipal.ReadOnly);
            Assert.IsNotNull(form.ProgramaActual);
            Assert.AreEqual(nueva.Date, BuscarActividad(form, primera.Id).FechaInicioProgramada!.Value.Date,
                "La vista republica el valor ya confirmado.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    private const string SqlBloqueoEscrituraPrograma = @"
CREATE TRIGGER trg_bloqueo_act_upd BEFORE UPDATE ON ""ActividadesProgramadas""
BEGIN SELECT RAISE(ABORT, 'escritura_prohibida_en_recarga'); END;
CREATE TRIGGER trg_bloqueo_prog_upd BEFORE UPDATE ON ""ProgramasObra""
BEGIN SELECT RAISE(ABORT, 'escritura_prohibida_en_recarga'); END;
CREATE TRIGGER trg_bloqueo_per_ins BEFORE INSERT ON ""PeriodosPrograma""
BEGIN SELECT RAISE(ABORT, 'escritura_prohibida_en_recarga'); END;
CREATE TRIGGER trg_bloqueo_per_del BEFORE DELETE ON ""PeriodosPrograma""
BEGIN SELECT RAISE(ABORT, 'escritura_prohibida_en_recarga'); END;
CREATE TRIGGER trg_bloqueo_dist_ins BEFORE INSERT ON ""DistribucionesPeriodo""
BEGIN SELECT RAISE(ABORT, 'escritura_prohibida_en_recarga'); END;
CREATE TRIGGER trg_bloqueo_dist_del BEFORE DELETE ON ""DistribucionesPeriodo""
BEGIN SELECT RAISE(ABORT, 'escritura_prohibida_en_recarga'); END;";

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

    private static void AfirmarEstadoVerificado(FormProgramaObra form)
    {
        Assert.IsFalse(form.ProgramaOperacionIncierta,
            "La edición se confirmó: no debe quedar como resultado incierto.");
        Assert.IsFalse(form.RequiereLecturaFresca,
            "La recarga fresca debe publicar la vista sin exigir otra lectura.");
        Assert.IsFalse(form.HistorialUndo.RequiresReconciliation,
            "El historial no debe quedar suspendido.");
        Assert.IsFalse(form.GridPrincipal.ReadOnly,
            "Al terminar la operación la edición vuelve a estar disponible.");
        Assert.IsNotNull(form.ProgramaActual, "La vista publicada tiene programa.");
    }

    /// <summary>
    /// Distribuciones íntegras: apuntan a periodos VIGENTES del programa y suman la
    /// cantidad/importe de la actividad (ajuste de residuo del último periodo).
    /// </summary>
    private static void VerificarDistribuciones(
        SOPROContext ctx,
        ActividadProgramada actividad,
        HashSet<int> periodosVigentes)
    {
        var distribuciones = ctx.DistribucionesPeriodo
            .Where(d => d.ActividadProgramadaId == actividad.Id)
            .ToList();

        Assert.IsTrue(distribuciones.Count > 0,
            $"La actividad '{actividad.Clave}' debe tener distribuciones tras la edición.");
        Assert.IsTrue(
            distribuciones.All(d => periodosVigentes.Contains(d.PeriodoProgramaId)),
            $"Las distribuciones de '{actividad.Clave}' no deben apuntar a periodos eliminados.");

        Assert.AreEqual(actividad.CantidadTotal, distribuciones.Sum(d => d.CantidadProgramada),
            $"La cantidad distribuida de '{actividad.Clave}' debe sumar la cantidad total.");
        Assert.AreEqual(actividad.ImporteTotal, distribuciones.Sum(d => d.ImporteProgramado),
            $"El importe distribuido de '{actividad.Clave}' debe sumar el importe total.");
    }

    private static ActivityGridRowDto BuscarActividad(FormProgramaObra form, int actividadId)
        => form.ProgramaActual!.Actividades.Single(a => a.Id == actividadId);

    private static string TextoEstado(FormProgramaObra form)
        => form.Controls.Find("lblEstado", searchAllChildren: true)
            .OfType<Label>()
            .FirstOrDefault()?.Text ?? string.Empty;

    private static int BuscarFila(FormProgramaObra form, int actividadId)
    {
        for (var fila = 0; fila < form.GridPrincipal.Rows.Count; fila++)
        {
            if (form.GridPrincipal.Rows[fila].DataBoundItem is ActivityGridRowDto dto && dto.Id == actividadId)
                return fila;
        }

        return -1;
    }

    /// <summary>Primer día hábil posterior a <paramref name="desde"/> según el programa.</summary>
    private static DateTime SiguienteDiaHabil(SOPROContext ctx, int programaId, DateTime desde)
    {
        var calculo = new ProgramacionCalculationService();
        for (var dias = 1; dias <= 30; dias++)
        {
            var candidata = desde.Date.AddDays(dias);
            if (calculo.IsWorkingDay(ctx, programaId, candidata))
                return candidata;
        }

        throw new InvalidOperationException("No se encontró un día hábil posterior.");
    }

    /// <summary>Primer día inhábil (fin de semana) posterior a <paramref name="desde"/>.</summary>
    private static DateTime SiguienteDiaInhabil(DateTime desde)
    {
        for (var dias = 1; dias <= 7; dias++)
        {
            var candidata = desde.Date.AddDays(dias);
            if (candidata.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                return candidata;
        }

        throw new InvalidOperationException("No se encontró un día inhábil posterior.");
    }

    // ═══════════════════════════ Escenario ═══════════════════════════

    private static string CrearRuta()
        => Path.Combine(Path.GetTempPath(), $"sopro_po_edicion_{Guid.NewGuid():N}.db");

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
            Nombre = "Proyecto edición consecutiva",
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

    /// <summary>
    /// Falla al crear el contexto de la LECTURA FRESCA de una edición: la 1ª creación es
    /// la carga inicial del formulario, la 2ª el guardado con contexto corto, la 3ª la
    /// regeneración y la 4ª la lectura fresca posterior al commit. Las lecturas de la
    /// reconciliación manual («Recargar») usan contextos reales.
    /// </summary>
    private sealed class FalloEnLecturaFrescaTrasEdicionFactory : IProjectDbContextFactory
    {
        private readonly string _dbPath;
        private int _createCount;

        public FalloEnLecturaFrescaTrasEdicionFactory(string dbPath) => _dbPath = dbPath;

        public int CreateCount => Volatile.Read(ref _createCount);

        public SOPROContext Create(string databasePath)
        {
            var numero = Interlocked.Increment(ref _createCount);
            if (numero == 4)
                throw new InvalidOperationException("lectura_fresca_simulada_fallida");

            return new SOPROContext(_dbPath);
        }

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }

    /// <summary>
    /// Falla en la lectura fresca posterior a un guardado RECHAZADO (Ok=false): la 1ª
    /// creación es la carga inicial, la 2ª el contexto del guardado (que no escribe) y
    /// la 3ª la lectura fresca. La reconciliación manual («Recargar») usa la 4ª.
    /// </summary>
    private sealed class FalloEnLecturaFrescaTrasEscrituraRechazadaFactory : IProjectDbContextFactory
    {
        private readonly string _dbPath;
        private int _createCount;

        public FalloEnLecturaFrescaTrasEscrituraRechazadaFactory(string dbPath) => _dbPath = dbPath;

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
