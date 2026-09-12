using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Reporting.Tests.TestInfrastructure;
using SOPRO.WinForms.Services;

namespace SOPRO.WinForms.Tests.Services.Reporting;

/// <summary>
/// Pruebas focalizadas N7-18d del swap: el servicio genera ambos formatos con firmas
/// reales, nunca toca el destino ante un Result fallido y su puente síncrono concluye
/// sin bombeo de mensajes. No crea goldens nuevos.
/// </summary>
[TestClass]
public class CatalogoMatricesExportServiceTests
{
    [DataTestMethod]
    [DataRow("Excel")]
    [DataRow("Pdf")]
    public void Exportar_GeneraArchivoConFirmaReal(string formato)
    {
        using var fixture = new SinteticoCatalogoFixture();
        var session = LegacySessionBridge.FromLegacy(fixture.Context, fixture.Proyecto.Id);
        var request = new BuildMatrixCatalogReportRequest(
            fixture.Matrices.Select(m => m.Id).ToList(), "Todos");
        var service = new CatalogoMatricesExportService(new ProjectDbContextFactory());
        string extension = formato == "Excel" ? "xlsx" : "pdf";
        string destino = Path.Combine(Path.GetTempPath(), $"sopro_catmat_svc_{Guid.NewGuid():N}.{extension}");
        try
        {
            if (formato == "Excel")
                service.ExportarExcel(session, request, destino);
            else
                service.ExportarPdf(session, request, destino);

            Assert.IsTrue(File.Exists(destino), "El servicio debe crear el archivo destino.");
            Assert.IsTrue(new FileInfo(destino).Length > 0, "El archivo generado no debe estar vacío.");

            byte[] magic = new byte[4];
            using (var fs = File.OpenRead(destino))
                fs.ReadExactly(magic);

            if (formato == "Excel")
            {
                Assert.AreEqual(0x50, magic[0], "XLSX es un zip: debe iniciar con 'P'.");
                Assert.AreEqual(0x4B, magic[1], "XLSX es un zip: debe iniciar con 'PK'.");
            }
            else
            {
                Assert.AreEqual((byte)'%', magic[0], "PDF debe iniciar con '%PDF'.");
                Assert.AreEqual((byte)'P', magic[1], "PDF debe iniciar con '%PDF'.");
                Assert.AreEqual((byte)'D', magic[2], "PDF debe iniciar con '%PDF'.");
                Assert.AreEqual((byte)'F', magic[3], "PDF debe iniciar con '%PDF'.");
            }
        }
        finally
        {
            Borrar(destino);
        }
    }

    [TestMethod]
    public void Exportar_ResultadoFallido_NoTocaDestino()
    {
        var service = new CatalogoMatricesExportService(new ProjectDbContextFactory());
        var session = ProjectSessionInfo.Create(ProjectRef.Master, @"C:\no-existe-esta-base\proyecto.db");
        var request = new BuildMatrixCatalogReportRequest(new[] { 1 });
        string ausente = Path.Combine(Path.GetTempPath(), $"sopro_catmat_svc_fail_{Guid.NewGuid():N}.xlsx");
        string centinela = Path.Combine(Path.GetTempPath(), $"sopro_catmat_svc_sentinel_{Guid.NewGuid():N}.pdf");
        File.WriteAllText(centinela, "centinela");
        try
        {
            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => service.ExportarExcel(session, request, ausente));
            StringAssert.Contains(ex.Message, "requiere un proyecto");
            Assert.IsFalse(File.Exists(ausente), "Un destino ausente debe seguir ausente tras el fallo.");

            Assert.ThrowsException<InvalidOperationException>(
                () => service.ExportarPdf(session, request, centinela));
            Assert.AreEqual("centinela", File.ReadAllText(centinela),
                "Un archivo preexistente debe quedar intacto tras el fallo.");
        }
        finally
        {
            Borrar(ausente);
            Borrar(centinela);
        }
    }

    [TestMethod]
    public void PuenteSincrono_ConContextoSinBombeo_ConcluyeSinDeadlock()
    {
        using var fixture = new SinteticoCatalogoFixture();
        var session = LegacySessionBridge.FromLegacy(fixture.Context, fixture.Proyecto.Id);
        var request = new BuildMatrixCatalogReportRequest(
            fixture.Matrices.Select(m => m.Id).ToList(), "Todos");
        var service = new CatalogoMatricesExportService(new YieldingFactory());
        string destino = Path.Combine(Path.GetTempPath(), $"sopro_catmat_svc_sync_{Guid.NewGuid():N}.xlsx");
        Exception? error = null;
        try
        {
            var t = Task.Run(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new NonPumpingContext());
                try
                {
                    service.ExportarExcel(session, request, destino);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            });

            Assert.IsTrue(t.Wait(TimeSpan.FromSeconds(60)),
                "El puente síncrono debe concluir sin bombeo de mensajes.");
            Assert.IsNull(error, $"El puente no debe fallar: {error?.Message}");
            Assert.IsTrue(File.Exists(destino), "El puente debe producir el archivo destino.");
        }
        finally
        {
            Borrar(destino);
        }
    }

    private sealed class YieldingFactory : IProjectDbContextFactory
    {
        public SOPROContext Create(string databasePath) => new(databasePath);

        public async Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return new SOPROContext(databasePath);
        }
    }

    private sealed class NonPumpingContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            // Sin bombeo a propósito: si el puente capturara este contexto,
            // la continuación se perdería y el Wait detectaría el deadlock.
        }
    }

    private static void Borrar(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}
