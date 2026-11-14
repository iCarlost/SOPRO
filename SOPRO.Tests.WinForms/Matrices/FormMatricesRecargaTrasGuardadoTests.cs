using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Matrices;

/// <summary>
/// Recarga del catálogo después de que el editor guardó una matriz (#121). El
/// guardado escribe por un contexto por operación y el tracker de la sesión
/// queda intacto: puede tener trabajo pendiente ajeno o matrices rastreadas con
/// valores stale. Tras cerrar el editor, <see cref="FormMatrices"/> debe
/// materializar los valores confirmados cuando el tracker está limpio y, cuando
/// no lo está, mantener el aviso de conflicto visible sin recargar (que la
/// recarga inmediata no enmascare el conflicto con el identity map) y sin
/// guardar ni limpiar el tracker ajeno.
/// </summary>
[STATestClass]
[DoNotParallelize] // Forms, eventos estáticos y contextos SQLite temporales.
public class FormMatricesRecargaTrasGuardadoTests
{
    private const string ClaveMatriz = "MAT-01";
    private const decimal CostoOriginal = 100m;
    private const decimal CostoConfirmado = 250m;
    private const string MensajeConflictoGuardado =
        "La matriz se guardó, pero hay cambios sin guardar; el catálogo no se recargó.";
    private const string MensajeConflictoPisarCommit =
        "La matriz se guardó, pero hay cambios sin guardar en matrices del proyecto; confirmarlos después puede pisar los valores recién guardados. El catálogo no se recargó.";
    private const string MensajeConflictoInsumos =
        "Hay cambios sin guardar; no se recargó el catálogo de matrices.";

    [TestMethod]
    public void RecargarTrasGuardado_ConTrackerLimpio_MaterializaElValorConfirmado()
    {
        EjecutarConEscenario((ctx, dbPath, proyecto, matriz, form, bombear) =>
        {
            bombear(() => LeerCostoDirecto(form) == CostoOriginal,
                "El catálogo debe arrancar con el valor rastreado original.");

            // Escritura confirmada equivalente al guardado del editor: contexto por
            // operación; el tracker de la sesión no se entera y su identidad sigue
            // devolviendo el valor viejo.
            using (var escritura = new SOPROContext(dbPath))
            {
                var fresca = escritura.Matrices.Single(m => m.Id == matriz.Id);
                fresca.CostoDirecto = CostoConfirmado;
                escritura.SaveChanges();
            }

            bool recargo = form.RecargarTrasGuardadoDeMatriz();

            Assert.IsTrue(recargo, "Con el tracker limpio la recarga tras el guardado debe ejecutarse.");
            bombear(() => LeerCostoDirecto(form) == CostoConfirmado,
                "Tras la recarga el grid debe mostrar el costo confirmado en la base, no el stale del identity map.");
            Assert.AreNotEqual(MensajeConflictoGuardado, LeerStatus(form),
                "Sin trabajo pendiente no debe aparecer el aviso de conflicto.");
        });
    }

    [TestMethod]
    public void RecargarTrasGuardado_ConCambiosPendientes_NoRecargaYMantieneElConflicto()
    {
        EjecutarConEscenario((ctx, dbPath, proyecto, matriz, form, bombear) =>
        {
            bombear(() => LeerCostoDirecto(form) == CostoOriginal,
                "El catálogo debe arrancar con el valor rastreado original.");

            // Trabajo pendiente ajeno ajeno a las matrices (edición del proyecto): la
            // recarga no puede descartar rastreados sin perderlo.
            proyecto.Nombre = "borrador pendiente";
            var entry = ctx.Entry(proyecto);

            using (var escritura = new SOPROContext(dbPath))
            {
                var fresca = escritura.Matrices.Single(m => m.Id == matriz.Id);
                fresca.CostoDirecto = CostoConfirmado;
                escritura.SaveChanges();
            }

            bool recargo = form.RecargarTrasGuardadoDeMatriz();

            Assert.IsFalse(recargo, "Con trabajo pendiente la recarga debe abstenerse.");
            Assert.AreEqual(MensajeConflictoGuardado, LeerStatus(form),
                "El aviso de conflicto debe quedar visible tras cerrar el editor.");

            // La recarga inmediata no enmascara el aviso con datos stale del grid.
            bombear(() => LeerStatus(form) == MensajeConflictoGuardado && LeerCostoDirecto(form) == CostoOriginal,
                "El grid debe conservar su estado y el aviso no debe ser reemplazado por una recarga.");

            // El tracker ajeno se conserva intacto: ni guardado ni limpiado.
            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "La guarda no debe guardar ni limpiar las ediciones pendientes.");
            Assert.AreEqual(EntityState.Modified, entry.State,
                "El borrador debe seguir rastreado como modificado.");
            Assert.AreEqual("borrador pendiente", proyecto.Nombre,
                "El texto pendiente del usuario debe sobrevivir a la guarda.");
        });
    }

    [TestMethod]
    public void RecargarTrasGuardado_ConEscrituraDeMatrizPendiente_NombraElRiesgoDePisarElCommit()
    {
        EjecutarConEscenario((ctx, dbPath, proyecto, matriz, form, bombear) =>
        {
            bombear(() => LeerCostoDirecto(form) == CostoOriginal,
                "El catálogo debe arrancar con el valor rastreado original.");

            // Edición pendiente de la MISMA matriz rastreada por la sesión: si un
            // módulo ajeno la confirma después, pisaría los valores recién
            // guardados por el commit por operación.
            matriz.Descripcion = "borrador pendiente";
            var entry = ctx.Entry(matriz);

            using (var escritura = new SOPROContext(dbPath))
            {
                var fresca = escritura.Matrices.Single(m => m.Id == matriz.Id);
                fresca.CostoDirecto = CostoConfirmado;
                fresca.Descripcion = "Confirmada por el editor";
                escritura.SaveChanges();
            }

            bool recargo = form.RecargarTrasGuardadoDeMatriz();

            Assert.IsFalse(recargo, "Con escritura de matriz pendiente la recarga debe abstenerse.");
            Assert.AreEqual(MensajeConflictoPisarCommit, LeerStatus(form),
                "El aviso debe nombrar el riesgo de que el guardado ajeno pise el commit.");

            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "La guarda no debe guardar ni limpiar la edición pendiente de la matriz.");
            Assert.AreEqual(EntityState.Modified, entry.State,
                "El borrador de la matriz debe seguir rastreado como modificado.");
            Assert.AreEqual("borrador pendiente", matriz.Descripcion,
                "El borrador del usuario debe sobrevivir a la guarda.");
        });
    }

    [TestMethod]
    public void RecargarTrasCambioDeInsumos_ConservaElAvisoYLlamadorPrevios()
    {
        EjecutarConEscenario((ctx, dbPath, proyecto, matriz, form, bombear) =>
        {
            bombear(() => LeerCostoDirecto(form) == CostoOriginal,
                "El catálogo debe arrancar con el valor rastreado original.");

            matriz.Descripcion = "borrador pendiente";

            form.RecargarTrasCambioDeInsumos();

            Assert.AreEqual(MensajeConflictoInsumos, LeerStatus(form),
                "La ruta de insumos debe conservar su aviso original.");
            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "La guarda no debe guardar ni limpiar las ediciones pendientes.");
        });
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static void EjecutarConEscenario(
        Action<SOPROContext, string, Proyecto, Matriz, FormMatrices, Action<Func<bool>, string>> prueba)
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fm_recarga_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        SqliteConnection.ClearAllPools();
        SOPROContext? ctx = null;
        FormMatrices? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();
            var proyecto = AgregarProyecto(ctx);
            var matriz = AgregarMatriz(ctx, proyecto.Id);

            form = new FormMatrices(ctx, proyecto.Id, modoEmbebido: true);
            form.Show();

            void BombearHasta(Func<bool> condicion, string mensaje)
            {
                var limite = DateTime.UtcNow.AddSeconds(30);
                while (!condicion() && DateTime.UtcNow < limite)
                {
                    System.Windows.Forms.Application.DoEvents();
                    System.Threading.Thread.Sleep(10);
                }
                Assert.IsTrue(condicion(), mensaje);
            }

            prueba(ctx, dbPath, proyecto, matriz, form, BombearHasta);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            SqliteConnection.ClearAllPools();
            for (int intento = 0; intento < 5; intento++)
            {
                try
                {
                    if (Directory.Exists(root))
                        Directory.Delete(root, recursive: true);
                    break;
                }
                catch (IOException) when (intento < 4)
                {
                    System.Threading.Thread.Sleep(200);
                }
                catch (UnauthorizedAccessException) when (intento < 4)
                {
                    System.Threading.Thread.Sleep(200);
                }
            }
        }
    }

    private static Proyecto AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto recarga tras guardado",
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
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto;
    }

    private static Matriz AgregarMatriz(SOPROContext ctx, int proyectoId)
    {
        var matriz = new Matriz
        {
            Clave = ClaveMatriz,
            Descripcion = "Matriz de recarga",
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            ProyectoId = proyectoId,
            CostoDirecto = CostoOriginal,
            Notas = string.Empty,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();
        return matriz;
    }

    /// <summary>CostoDirecto del DataBoundItem de la fila de la matriz de prueba.</summary>
    private static decimal LeerCostoDirecto(FormMatrices form)
    {
        foreach (DataGridViewRow fila in form.GridPrincipal.Rows)
        {
            if (fila.DataBoundItem is MatrixGridRowDisplay row && row.Clave == ClaveMatriz)
                return row.CostoDirecto;
        }
        return decimal.MinValue;
    }

    private static string LeerStatus(FormMatrices form)
        => ((ToolStripStatusLabel?)(form.GetType()
                .GetField("lblStatus", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(form)))?.Text ?? string.Empty;
}
