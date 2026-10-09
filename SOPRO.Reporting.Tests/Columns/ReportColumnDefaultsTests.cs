using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Reporting.Columns;

namespace SOPRO.Reporting.Tests.Columns;

/// <summary>
/// Tests de <see cref="ReportColumnDefaults"/>: la columna sintética de numeración
/// del Presupuesto ("#"/<c>Numero</c>) que los renderers PDF/Excel anteponen para
/// conservar la paridad por defecto con el grid legacy. Es una definición neutral
/// compartida; el contrato persistido no se modifica.
/// </summary>
[TestClass]
public class ReportColumnDefaultsTests
{
    private static ReportColumnDefinition Columna(string identificador)
        => new(
            Identificador: identificador,
            Encabezado: identificador,
            Visible: true,
            Orden: 0,
            Ancho: 100,
            EstiloEncabezado: ReportTableStyle.LegacyPresupuesto().EstiloEncabezado,
            EstiloContenido: ReportTableStyle.LegacyPresupuesto().EstiloContenido,
            Alineacion: ReportTextAlignment.Izquierda,
            AlineacionVertical: ReportVerticalAlignment.Medio,
            Wrap: false,
            FormatoNumerico: string.Empty,
            EsNumerica: false);

    [TestMethod]
    public void PresupuestoNumeroColumn_ReproduceLaColumnaDelGridLegacy()
    {
        var col = ReportColumnDefaults.PresupuestoNumeroColumn();

        Assert.AreEqual("Numero", col.Identificador);
        Assert.AreEqual("#", col.Encabezado);
        Assert.AreEqual(ReportColumnDefaults.NumeroAnchoPx, col.Ancho);
        Assert.AreEqual(40, col.Ancho, "El grid legacy fija colNumero.Width = 40.");
        Assert.AreEqual(ReportTextAlignment.Centro, col.Alineacion);
        Assert.AreEqual(ReportVerticalAlignment.Medio, col.AlineacionVertical);
        Assert.IsFalse(col.Wrap);
        Assert.IsFalse(col.EsNumerica, "El valor es el índice de fila como texto.");
        Assert.AreEqual(string.Empty, col.FormatoNumerico);

        var estilo = ReportTableStyle.LegacyPresupuesto();
        Assert.AreEqual(estilo.EstiloEncabezado, col.EstiloEncabezado);
        Assert.AreEqual(estilo.EstiloContenido, col.EstiloContenido);
    }

    [TestMethod]
    public void ConPrefijoNumeroPresupuesto_AnteponeCuandoNoExiste()
    {
        var columnas = new List<ReportColumnDefinition>
        {
            Columna("Clave"),
            Columna("Descripcion"),
        };

        var resultado = ReportColumnDefaults.ConPrefijoNumeroPresupuesto(columnas);

        Assert.AreEqual(3, resultado.Count);
        Assert.IsTrue(ReportColumnDefaults.EsNumero(resultado[0]));
        CollectionAssert.AreEqual(
            new[] { "Numero", "Clave", "Descripcion" },
            resultado.Select(c => c.Identificador).ToArray(),
            "La numeración debe quedar primero y preservar el orden del resto.");
    }

    [TestMethod]
    public void ConPrefijoNumeroPresupuesto_NoDuplicaCuandoYaExiste()
    {
        var columnas = new List<ReportColumnDefinition>
        {
            Columna("Numero"),
            Columna("Clave"),
        };

        var resultado = ReportColumnDefaults.ConPrefijoNumeroPresupuesto(columnas);

        Assert.AreEqual(2, resultado.Count, "Si la configuración persistida define Numero, debe respetarse.");
        Assert.AreEqual(1, resultado.Count(ReportColumnDefaults.EsNumero));
        Assert.AreEqual("Numero", resultado[0].Identificador);
        Assert.AreEqual("Clave", resultado[1].Identificador);
    }

    [TestMethod]
    public void ConPrefijoNumeroPresupuesto_DetectaIdentificadorSinDistinguirMayusculas()
    {
        var resultado = ReportColumnDefaults.ConPrefijoNumeroPresupuesto(
            new List<ReportColumnDefinition> { Columna("numero") });

        Assert.AreEqual(1, resultado.Count);
        Assert.AreEqual("numero", resultado[0].Identificador);
    }

    [TestMethod]
    public void ConPrefijoNumeroPresupuesto_ListaVacia_SoloDevuelveLaNumeracion()
    {
        var resultado = ReportColumnDefaults.ConPrefijoNumeroPresupuesto(
            Array.Empty<ReportColumnDefinition>());

        Assert.AreEqual(1, resultado.Count);
        Assert.IsTrue(ReportColumnDefaults.EsNumero(resultado[0]));
    }

    [TestMethod]
    public void ConPrefijoNumeroPresupuesto_Nulo_LanzaArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ReportColumnDefaults.ConPrefijoNumeroPresupuesto(null!));
    }
}
