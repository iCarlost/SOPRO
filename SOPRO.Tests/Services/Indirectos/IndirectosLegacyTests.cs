using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Indirectos;

/// <summary>
/// Caracterización N0 de las propiedades computadas legacy de indirectos.
/// Nota: la fórmula de porcentajes (totalOC/volumenAnual × 100 y totalCampo/costoDirecto × 100)
/// vive embebida en FormIndirectos.Acciones.cs (WinForms), no es invocable desde los tests;
/// queda documentada en la tabla de divergencias como comportamiento legacy a extraer en N1+.
/// </summary>
[TestClass]
public class IndirectosLegacyTests
{
    [TestMethod]
    public void ImporteTotal_DebeSerImporteMensualPorDuracionMeses()
    {
        var concepto = new ConceptoIndirecto
        {
            Concepto = "Renta de Edificio",
            ImporteMensual = 5000m,
            DuracionMeses = 12
        };

        Assert.AreEqual(60000m, concepto.ImporteTotal);
    }

    [TestMethod]
    public void ImporteTotal_ConDuracionUnMes_DebeSerElImporteMensual()
    {
        var concepto = new ConceptoIndirecto
        {
            Concepto = "Gerente General",
            ImporteMensual = 10000m,
            DuracionMeses = 1
        };

        Assert.AreEqual(10000m, concepto.ImporteTotal);
    }

    [TestMethod]
    public void GrupoIndirecto_Total_DebeSumarSoloConceptosActivos()
    {
        var grupo = new GrupoIndirecto
        {
            Nombre = "HONORARIOS, SUELDOS Y PRESTACIONES",
            Tipo = TipoIndirecto.OficinaCentral,
            Conceptos = new List<ConceptoIndirecto>
            {
                new() { Concepto = "Gerente General", ImporteMensual = 10000m, Activo = true },
                new() { Concepto = "Contador", ImporteMensual = 5000m, Activo = true },
                new() { Concepto = "Capturista", ImporteMensual = 3000m, Activo = false }
            }
        };

        Assert.AreEqual(15000m, grupo.Total);
    }

    [TestMethod]
    public void GrupoIndirecto_Total_ConTodosInactivos_DebeSerCero()
    {
        var grupo = new GrupoIndirecto
        {
            Nombre = "VACÍO",
            Tipo = TipoIndirecto.Campo,
            Conceptos = new List<ConceptoIndirecto>
            {
                new() { Concepto = "Viáticos", ImporteMensual = 2000m, Activo = false }
            }
        };

        Assert.AreEqual(0m, grupo.Total);
    }

    [TestMethod]
    public void ConfiguracionIndirectos_PorcentajeTotal_DebeSumarAmbosPorcentajes()
    {
        var configuracion = new ConfiguracionIndirectos
        {
            PorcentajeOficinaCentral = 1.5m,
            PorcentajeCampo = 2m
        };

        Assert.AreEqual(3.5m, configuracion.PorcentajeTotal);
    }

    [TestMethod]
    public void ConfiguracionIndirectos_PorcentajeTotal_ConCeros_DebeSerCero()
    {
        var configuracion = new ConfiguracionIndirectos();

        Assert.AreEqual(0m, configuracion.PorcentajeTotal);
    }
}
