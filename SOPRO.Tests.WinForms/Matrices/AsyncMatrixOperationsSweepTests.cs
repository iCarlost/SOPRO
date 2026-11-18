using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
/// Barrido de seguridad de las operaciones asíncronas de <see cref="FormMatrices"/>
/// (#151, lane C): una carga superada no publica su resultado, una carga en curso
/// no toca la UI de un formulario ya descartado y el borrado captura desde su
/// primera sentencia (incluida la resolución de la selección) con un mensaje
/// recuperable que no expone la excepción.
/// </summary>
[STATestClass]
[DoNotParallelize] // Formularios, contextos SQLite temporales y sincronización de UI.
public class AsyncMatrixOperationsSweepTests
{
    private const string ClaveObsoleta = "STALE";
    private const string ClaveVigente = "FRESH";
    private const string DetalleExcepcionSeleccion = "detalle interno sensible";

    [TestMethod]
    public void CargaSuperada_NoPublicaSuResultadoAunqueTermineDespues()
    {
        ConFormulario((_, form) =>
        {
            var inicioPrimera = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var liberarPrimera = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var inicioSegunda = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var liberarSegunda = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            int invocaciones = 0;
            form.CargaMatricesParaPrueba = _ =>
            {
                int actual = Interlocked.Increment(ref invocaciones);
                if (actual == 1)
                    return Task.FromResult(ResultadoVacio());

                if (actual == 2)
                {
                    inicioPrimera.TrySetResult(true);
                    return EsperarYResultar(liberarPrimera.Task, Resultado(ClaveObsoleta, matrizId: 1001));
                }

                inicioSegunda.TrySetResult(true);
                return EsperarYResultar(liberarSegunda.Task, Resultado(ClaveVigente, matrizId: 1002));
            };

            form.Show();
            Assert.AreEqual(1, invocaciones, "La carga inicial debe completarse al mostrar el formulario.");

            var primera = form.CargarMatricesAsync();
            Bombear(() => inicioPrimera.Task.IsCompleted, "La primera carga debe quedar en curso.");

            var segunda = form.CargarMatricesAsync();
            liberarPrimera.TrySetResult(true);

            Bombear(() => inicioSegunda.Task.IsCompleted,
                "La segunda carga debe entrar después de liberar la primera.");
            Assert.AreEqual(0, LeerClaves(form).Count,
                "Mientras la carga vigente no termina, la obsoleta no debe publicar en el grid.");
            Assert.IsFalse(LeerClaves(form).Contains(ClaveObsoleta),
                "El resultado superado no debe llegar nunca a la UI.");

            liberarSegunda.TrySetResult(true);
            Bombear(() => primera.IsCompleted && segunda.IsCompleted,
                "Ambas cargas deben terminar sin propagar excepciones.");
            primera.GetAwaiter().GetResult();
            segunda.GetAwaiter().GetResult();

            Bombear(() => LeerClaves(form).Contains(ClaveVigente),
                "Sólo la generación vigente debe publicar su resultado.");
            Assert.IsFalse(LeerClaves(form).Contains(ClaveObsoleta),
                "El grid no debe conservar el resultado obsoleto.");
        });
    }

    [TestMethod]
    public void CargaEnCurso_AlDescartarElFormulario_NoPublicaNiPropaga()
    {
        ConFormulario((_, form) =>
        {
            var inicio = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var liberar = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            int invocaciones = 0;
            form.CargaMatricesParaPrueba = _ =>
            {
                int actual = Interlocked.Increment(ref invocaciones);
                if (actual == 1)
                    return Task.FromResult(ResultadoVacio());

                inicio.TrySetResult(true);
                return EsperarYResultar(liberar.Task, Resultado(ClaveVigente, matrizId: 1002));
            };

            form.Show();
            var carga = form.CargarMatricesAsync();
            Bombear(() => inicio.Task.IsCompleted, "La carga debe quedar en curso antes de cerrar.");

            form.Dispose();
            liberar.TrySetResult(true);

            Bombear(() => carga.IsCompleted,
                "La carga debe observar el cierre y terminar sin propagar la excepción.");
            carga.GetAwaiter().GetResult();
            Assert.IsTrue(carga.IsCompletedSuccessfully,
                "Publicar sobre un formulario descartado no debe fallar ni propagarse.");
        });
    }

    [TestMethod]
    public void Eliminacion_FallaAlResolverLaSeleccion_SeRecuperaSinExponerLaExcepcion()
    {
        ConFormulario((_, form) =>
        {
            string? mensaje = null;
            form.SeleccionMatricesParaPrueba = () => throw new InvalidOperationException(DetalleExcepcionSeleccion);
            form.MostrarMensajeEliminacionParaPrueba = texto => mensaje = texto;

            var tarea = form.EliminarMatricesSeleccionadasAsync();
            Bombear(() => tarea.IsCompleted, "El borrado debe capturar el fallo de selección.");
            tarea.GetAwaiter().GetResult();

            Assert.IsTrue(tarea.IsCompletedSuccessfully,
                "El fallo de selección no debe escapar del comando de borrado.");
            Assert.IsNotNull(mensaje, "El usuario debe recibir un mensaje recuperable.");
            StringAssert.Contains(mensaje, FormMatrices.MensajeEliminacionRecuperable,
                "El mensaje debe indicar que la operación puede reintentarse.");
            Assert.IsFalse(mensaje!.Contains(DetalleExcepcionSeleccion),
                "El mensaje no debe exponer el detalle técnico de la excepción.");
        });
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static void ConFormulario(Action<SOPROContext, FormMatrices> prueba)
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_async_matrices_{Guid.NewGuid():N}");
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

            form = new FormMatrices(ctx, proyecto.Id, modoEmbebido: true);
            prueba(ctx, form);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            SqliteConnection.ClearAllPools();
            EliminarDirectorio(root);
        }
    }

    private static Proyecto AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto barrido async matrices",
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

    private static MatrixCatalogLoadResult Resultado(string clave, int matrizId)
    {
        var matriz = new Matriz
        {
            Id = matrizId,
            Clave = clave,
            Descripcion = clave,
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 10m,
            Notas = string.Empty,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        return new MatrixCatalogLoadResult
        {
            Rows = new List<MatrixGridRowDisplay>
            {
                new()
                {
                    MatrizId = matriz.Id,
                    Clave = matriz.Clave,
                    Descripcion = matriz.Descripcion,
                    Unidad = matriz.Unidad,
                    Tipo = matriz.Tipo,
                    TipoTexto = "APU",
                    CostoDirecto = matriz.CostoDirecto,
                    NumInsumos = 0,
                    OrigenDetalle = string.Empty,
                    Source = matriz,
                },
            },
            StatusText = $"{clave} cargada",
        };
    }

    private static MatrixCatalogLoadResult ResultadoVacio()
        => new() { Rows = new List<MatrixGridRowDisplay>(), StatusText = "Inicial" };

    private static async Task<MatrixCatalogLoadResult> EsperarYResultar(
        Task espera, MatrixCatalogLoadResult resultado)
    {
        await espera.ConfigureAwait(false);
        return resultado;
    }

    private static List<string> LeerClaves(FormMatrices form)
    {
        var claves = new List<string>();
        foreach (DataGridViewRow fila in form.GridPrincipal.Rows)
        {
            if (fila.DataBoundItem is MatrixGridRowDisplay row)
                claves.Add(row.Clave);
        }
        return claves;
    }

    private static void Bombear(Func<bool> condicion, string mensaje)
    {
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (!condicion() && DateTime.UtcNow < limite)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(5);
        }
        Assert.IsTrue(condicion(), mensaje);
    }

    private static void EliminarDirectorio(string root)
    {
        for (int intento = 0; intento < 5; intento++)
        {
            try
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException) when (intento < 4)
            {
                Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException) when (intento < 4)
            {
                Thread.Sleep(200);
            }
        }
    }
}
