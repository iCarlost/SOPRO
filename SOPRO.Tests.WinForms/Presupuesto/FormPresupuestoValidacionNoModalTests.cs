using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Seam no modal del diálogo de inconsistencias de <see cref="FormPresupuesto"/>:
/// la advertencia de apertura (cantidad/P.U. en cero) ya no espera interacción humana
/// en la suite STA. Se comprueba que la llamada se intercepta, que el formulario sigue
/// habilitado (sin diálogo modal como owner) y que el contrato de validación
/// (<c>ValidarPresupuestoAntesDeContinuar</c> devuelve <c>!bloquear</c>) no cambia.
/// </summary>
[STATestClass]
[DoNotParallelize] // Toca el seam estático y FormatoHelper; evita interacciones.
public class FormPresupuestoValidacionNoModalTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void Apertura_ConInconsistencias_SeInterceptaSinDialogoModal()
    {
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        var interceptorPrevio = FormPresupuesto.InterceptorDialogoInconsistencias;
        var llamadas = new List<(string Titulo, bool Bloquear, int Inconsistencias)>();

        using var root = new DirectorioTemporal("validacion_apertura_no_modal");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var proyecto = AgregarProyectoConConceptoEnCero(ctx);

            // Premisa: el presupuesto sí tiene inconsistencias, por lo que la apertura
            // intenta mostrar el diálogo (antes modal) de advertencia.
            Assert.IsTrue(PresupuestoValidationService.Validate(ctx, proyecto.Id).Count > 0,
                "El escenario debe producir al menos una inconsistencia de validación.");

            FormPresupuesto.InterceptorDialogoInconsistencias = (issues, titulo, bloquear) =>
                llamadas.Add((titulo, bloquear, issues.Count));

            form = new FormPresupuesto(ctx, proyecto, new ProjectWorkspaceService(
                Path.Combine(root.Ruta, "sopro"), Path.Combine(root.Ruta, "local")));

            // Show dispara FormPresupuesto_Shown -> MostrarAdvertenciaValidacionEnApertura.
            form.Show();
            form.ExpandirWorkspaceParaPrueba();
            System.Windows.Forms.Application.DoEvents();

            Assert.AreEqual(1, llamadas.Count,
                "La advertencia de apertura debe interceptarse una sola vez.");
            Assert.AreEqual("Advertencia de presupuesto", llamadas[0].Titulo);
            Assert.IsFalse(llamadas[0].Bloquear,
                "La advertencia de apertura no bloquea la operación.");
            Assert.IsTrue(llamadas[0].Inconsistencias > 0,
                "El interceptor debe recibir las inconsistencias detectadas.");
            Assert.IsTrue(form.Enabled,
                "Sin diálogo modal el formulario conserva el foco/habilitación (no queda esperando UI).");
            Assert.IsTrue(LeerValidacionAperturaMostrada(form),
                "El manejador Shown debe completarse: la validación de apertura ya se ejecutó.");
        }
        finally
        {
            FormPresupuesto.InterceptorDialogoInconsistencias = interceptorPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
        }
    }

    [TestMethod]
    public void ValidacionBloqueante_ConInterceptor_DevuelveFalsoSinDialogoModal()
    {
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        var interceptorPrevio = FormPresupuesto.InterceptorDialogoInconsistencias;
        var llamadas = new List<(string Titulo, bool Bloquear, int Inconsistencias)>();

        using var root = new DirectorioTemporal("validacion_bloqueante_no_modal");
        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(root.DbPath);
            ctx.Database.EnsureCreated();
            var proyecto = AgregarProyectoConConceptoEnCero(ctx);

            FormPresupuesto.InterceptorDialogoInconsistencias = (issues, titulo, bloquear) =>
                llamadas.Add((titulo, bloquear, issues.Count));

            form = new FormPresupuesto(ctx, proyecto, new ProjectWorkspaceService(
                Path.Combine(root.Ruta, "sopro"), Path.Combine(root.Ruta, "local")));
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            llamadas.Clear();

            var metodo = typeof(FormPresupuesto).GetMethod(
                "ValidarPresupuestoAntesDeContinuar", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(metodo, "Debe existir ValidarPresupuestoAntesDeContinuar.");
            var resultado = (bool)metodo!.Invoke(form, new object[] { true, "Validación de presupuesto" })!;

            Assert.IsFalse(resultado,
                "Con bloquear=true el contrato sigue devolviendo false aunque el diálogo se intercepte.");
            Assert.AreEqual(1, llamadas.Count);
            Assert.IsTrue(llamadas[0].Bloquear);
            Assert.IsTrue(form.Enabled, "La validación bloqueante no debe abrir un modal en pruebas.");
        }
        finally
        {
            FormPresupuesto.InterceptorDialogoInconsistencias = interceptorPrevio;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
        }
    }

    [TestMethod]
    public void Seam_ValorPorDefectoEsNullYSeRestauraExactamente()
    {
        // En producción nadie instala el interceptor: el valor por defecto es null y se
        // conserva el diálogo modal. Aquí solo se verifica el contrato del seam y que
        // restaurar el valor previo no deja estado global modificado.
        var previo = FormPresupuesto.InterceptorDialogoInconsistencias;
        try
        {
            FormPresupuesto.InterceptorDialogoInconsistencias = null;
            Assert.IsNull(FormPresupuesto.InterceptorDialogoInconsistencias,
                "Sin interceptor instalado la vista crea y muestra el diálogo modal normal.");
        }
        finally
        {
            FormPresupuesto.InterceptorDialogoInconsistencias = previo;
        }

        Assert.AreEqual(previo, FormPresupuesto.InterceptorDialogoInconsistencias,
            "El seam debe quedar exactamente como estaba antes de la prueba.");
    }

    private static Proyecto AgregarProyectoConConceptoEnCero(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto validación no modal",
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

        ctx.ConceptosPresupuesto.Add(new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "1.1",
            Descripcion = "Concepto sin cantidad ni precio",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 1,
            Unidad = "m2",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 0m,
            MatrizId = null,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
            FechaModificacion = new DateTime(2026, 1, 1),
        });
        ctx.SaveChanges();

        return proyecto;
    }

    private static bool LeerValidacionAperturaMostrada(FormPresupuesto form)
        => (bool)(typeof(FormPresupuesto)
               .GetField("_validacionAperturaMostrada", BindingFlags.Instance | BindingFlags.NonPublic)
               ?.GetValue(form)
           ?? false);

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)CampoProyectoGlobal().GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => CampoProyectoGlobal().SetValue(null, previo);

    private static FieldInfo CampoProyectoGlobal()
        => typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)
           ?? throw new InvalidOperationException(
               $"No se encontró el campo FormatoHelper.{CampoProyectoGlobalFormato}.");
}
