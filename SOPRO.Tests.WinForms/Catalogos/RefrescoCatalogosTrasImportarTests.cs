using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// Regresión #78: al importar insumos desde otro proyecto (matriz o APU) los
/// catálogos abiertos quedaban desactualizados y exigían F9. El refresco
/// posteriores a una importación externa debe reconsultar y mostrar las filas
/// nuevas sin intervención del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize] // Los forms de WinForms requieren un hilo STA dedicado.
public class RefrescoCatalogosTrasImportarTests
{
    [TestMethod]
    public void RefrescarCatalogosAbiertos_MuestraInsumoPersistidoSinRefrescoManual()
    {
        WithCatalogoMaterialesAbierto((contexto, proyecto, formulario) =>
        {
            Assert.AreEqual(
                1,
                formulario.GridPrincipal.Rows.Count,
                "El catálogo debe iniciar con el material precargado.");

            contexto.Materiales.Add(new Material
            {
                ProyectoId = proyecto.Id,
                Clave = "MAT-2",
                Descripcion = "Cemento importado",
                Unidad = "kg",
                PrecioUnitario = 250m,
                Notas = string.Empty
            });
            contexto.SaveChanges();

            Assert.AreEqual(
                1,
                formulario.GridPrincipal.Rows.Count,
                "Sin refresco el grid conserva el estado anterior.");

            InvocarRefrescoDeCatalogosAbiertos();

            Assert.AreEqual(
                2,
                formulario.GridPrincipal.Rows.Count,
                "El refresco posterior a la importación debe mostrar el insumo persistido.");
        });
    }

    [TestMethod]
    public void RefrescarCatalogosAbiertos_SinCatalogosAbiertos_NoFalla()
    {
        InvocarRefrescoDeCatalogosAbiertos();
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static void InvocarRefrescoDeCatalogosAbiertos()
    {
        var tipo = typeof(FormCatalogoMateriales).Assembly.GetType(
            "SOPRO.WinForms.Helpers.OpenFormsRefreshHelper",
            throwOnError: true)!;

        var metodo = tipo.GetMethod("RefrescarCatalogosAbiertos", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "No existe 'RefrescarCatalogosAbiertos' en el helper de refresco de catálogos.");

        metodo.Invoke(null, null);
    }

    private static void WithCatalogoMaterialesAbierto(
        Action<SOPROContext, Proyecto, FormCatalogoMateriales> verificar)
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_refresco_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "catalogo-temporal.db");

        SOPROContext? contexto = null;
        FormCatalogoMateriales? formulario = null;
        try
        {
            contexto = new SOPROContext(dbPath);
            contexto.Database.EnsureCreated();

            var proyecto = new Proyecto
            {
                Nombre = "Proyecto refresco",
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
            };
            contexto.Proyectos.Add(proyecto);
            contexto.SaveChanges();

            contexto.Materiales.Add(new Material
            {
                ProyectoId = proyecto.Id,
                Clave = "MAT-1",
                Descripcion = "Material base",
                Unidad = "kg",
                PrecioUnitario = 10m,
                Notas = string.Empty
            });
            contexto.SaveChanges();

            formulario = new FormCatalogoMateriales(contexto, proyecto.Id);
            formulario.Show();

            verificar(contexto, proyecto, formulario);
        }
        finally
        {
            if (formulario != null)
            {
                formulario.Close();
                formulario.Dispose();
            }
            contexto?.Dispose();

            // EF Core (SQLite) usa pooling: se liberan las conexiones antes de borrar
            // y la limpieza queda como mejor esfuerzo si Windows aún retiene el WAL.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
