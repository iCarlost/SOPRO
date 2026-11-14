using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Recalculo;

[TestClass]
public class RecalculoGlobalUiFeedbackTests
{
    [TestMethod]
    public void FalloCalculo_IncluyeElDetalleYNoAfirmaReversionNiAusenciaDeCambios()
    {
        string mensaje = RecalculoGlobalUiFeedback.MensajeFalloCalculo("referencia de matriz inválida");

        Assert.IsTrue(mensaje.Contains("referencia de matriz inválida", StringComparison.Ordinal),
            "El mensaje debe conservar el detalle del fallo.");
        Assert.IsFalse(mensaje.Contains("revirt", StringComparison.OrdinalIgnoreCase),
            "Un fallo de cálculo no debe describirse como una reversión.");
        Assert.IsFalse(mensaje.Contains("no se modificaron", StringComparison.OrdinalIgnoreCase),
            "El servicio guarda por fases; no se puede afirmar que no hubo cambios.");
        Assert.IsFalse(mensaje.Contains("se guardaron", StringComparison.OrdinalIgnoreCase),
            "Un fallo de cálculo no debe afirmar que los datos se guardaron.");
    }

    [TestMethod]
    public void FalloCalculo_SinDetalle_UsaMensajeGenerico()
    {
        string mensaje = RecalculoGlobalUiFeedback.MensajeFalloCalculo("   ");

        Assert.IsTrue(mensaje.Contains("No se pudo completar el recálculo.", StringComparison.Ordinal),
            "Debe existir un mensaje genérico cuando no hay detalle.");
    }

    [TestMethod]
    public void FalloRefresco_AfirmaQueElCalculoSeGuardoYReportaPestanasYDetalle()
    {
        var error = new InvalidOperationException("control ya liberado");

        string mensaje = RecalculoGlobalUiFeedback.MensajeFalloRefresco(2, error);

        Assert.IsTrue(mensaje.Contains("se completó y los datos se guardaron", StringComparison.Ordinal),
            "El refresco falla después de guardar; el mensaje debe decirlo.");
        Assert.IsTrue(mensaje.Contains("Pestañas que no se pudieron refrescar: 2.", StringComparison.Ordinal),
            "Debe informar cuántas pestañas no se refrescaron.");
        Assert.IsTrue(mensaje.Contains("control ya liberado", StringComparison.Ordinal),
            "Debe incluir el detalle del fallo de refresco.");
        Assert.IsTrue(mensaje.Contains("reabre el proyecto", StringComparison.Ordinal),
            "Debe ofrecer una acción de recuperación.");
        Assert.IsFalse(mensaje.Contains("revirt", StringComparison.OrdinalIgnoreCase),
            "El refresco no revierte el cálculo ya guardado.");
    }

    [TestMethod]
    public void FalloRefresco_SinErrorNiPestanas_MantieneElMensajeDeExitoGuardado()
    {
        string mensaje = RecalculoGlobalUiFeedback.MensajeFalloRefresco(0, null);

        Assert.IsTrue(mensaje.Contains("se completó y los datos se guardaron", StringComparison.Ordinal));
        Assert.IsFalse(mensaje.Contains("Pestañas que no se pudieron refrescar", StringComparison.Ordinal),
            "No debe listar pestañas cuando no hubo fallos por pestaña.");
    }

    [TestMethod]
    public void FalloInesperado_NoAfirmaQueLosDatosSeRevirtieron()
    {
        string mensaje = RecalculoGlobalUiFeedback.MensajeFalloInesperado("fallo de infraestructura");

        Assert.IsTrue(mensaje.Contains("fallo de infraestructura", StringComparison.Ordinal));
        Assert.IsTrue(mensaje.Contains("no se revierten", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ResultadoRefresco_TieneFallosConPestanaOConError()
    {
        Assert.IsFalse(new SOPRO.WinForms.Forms.RefrescoPostRecalculoResultado(0, null).TieneFallos);
        Assert.IsTrue(new SOPRO.WinForms.Forms.RefrescoPostRecalculoResultado(1, null).TieneFallos);
        Assert.IsTrue(SOPRO.WinForms.Forms.RefrescoPostRecalculoResultado
            .ConError(new InvalidOperationException()).TieneFallos);
    }
}
