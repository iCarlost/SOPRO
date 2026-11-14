using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Matrices;

/// <summary>
/// Reconciliación de los rastreados propios del editor de matrices tras el
/// guardado por contexto por operación (#121). El editor trabaja con COPIAS
/// DESACOPLADAS (AsNoTracking): las ediciones propias jamás quedan pendientes
/// en el tracker de la sesión. La reconciliación resincroniza la matriz editada
/// (Unchanged con OriginalValues alineados), desconecta en profundidad cualquier
/// entrada legado del lote reemplazado y conserva el trabajo pendiente ajeno sin
/// guardar ni limpiar el tracker.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper; contextos SQLite temporales.
public class FormEditarMatrizReconciliacionRastreadosTests
{
    private const decimal CostoConfirmado = 50m;

    [TestMethod]
    public void ReconciliarTrasGuardado_LiberaComponentesReemplazados_YElSaveChangesAjenoNoPisaElCommit()
    {
        EjecutarConEditor((ctx, dbPath, proyecto, matriz, form) =>
        {
            // Las copias de trabajo del editor son desacopladas (#121): la
            // edición del usuario NO deja trabajo pendiente en el tracker.
            var editado = form.ComponentesTempParaPrueba.First();
            editado.Cantidad = 5m;
            editado.Importe = 12.5m;
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Las copias desacopladas no deben ensuciar el tracker de la sesión.");

            var dto = MatrixEditorService.BuildEditDto(
                proyecto.Id,
                matriz.Clave,
                "Descripción confirmada",
                matriz.Unidad,
                esCuadrilla: false,
                esApu: true,
                CostoConfirmado,
                form.ComponentesTempParaPrueba);

            var idsOriginales = form.ComponentesTempParaPrueba.Select(c => c.Id).ToList();

            var resultado = MatrixApplicationService
                .SaveAsync(new ProjectDbContextFactory(), dbPath, dto, matriz, ctx)
                .GetAwaiter().GetResult();

            Assert.IsFalse(resultado.IsNew);
            Assert.AreEqual(matriz.Id, resultado.MatrixId);

            form.ReconciliarRastreadosPropios(dto);

            // La matriz queda Unchanged con los valores confirmados: un SaveChanges
            // ajeno posterior la reescribe como no-op, nunca pisa el commit.
            var entradaMatriz = ctx.Entry(matriz);
            Assert.AreEqual(EntityState.Unchanged, entradaMatriz.State,
                "La matriz editada debe quedar Unchanged tras la reconciliación.");
            Assert.AreEqual(CostoConfirmado, matriz.CostoDirecto);
            Assert.AreEqual("Descripción confirmada", matriz.Descripcion);
            Assert.AreEqual(dto.ProyectoId, matriz.ProyectoId,
                "La reconciliación debe sincronizar también los campos que Apply reescribe, como ProyectoId.");

            // El SaveChanges ajeno del contexto de sesión confirma lo pendiente
            // ajeno y nunca toca las copias desacopladas del editor.
            ctx.SaveChanges();

            using (var lectura = new SOPROContext(dbPath))
            {
                var fresca = lectura.Matrices.Include(m => m.Componentes).Single(m => m.Id == matriz.Id);
                Assert.AreEqual(CostoConfirmado, fresca.CostoDirecto,
                    "El SaveChanges ajeno no debe pisar el costo confirmado.");
                Assert.AreEqual("Descripción confirmada", fresca.Descripcion);
                Assert.AreEqual(dto.Componentes.Count, fresca.Componentes.Count,
                    "El SaveChanges ajeno no debe alterar el lote de componentes confirmado.");
                Assert.AreEqual(5m, fresca.Componentes.Single(c => c.Orden == 1).Cantidad,
                    "La cantidad editada en la copia desacoplada debe confirmarse vía DTO.");
                Assert.IsFalse(fresca.Componentes.Any(c => idsOriginales.Contains(c.Id)),
                    "Los registros de componentes reemplazados deben seguir eliminados.");
            }
        });
    }

    [TestMethod]
    public void ReconciliarTrasGuardado_ConservaElTrabajoPendienteAjeno()
    {
        EjecutarConEditor((ctx, dbPath, proyecto, matriz, form) =>
        {
            var dto = MatrixEditorService.BuildEditDto(
                proyecto.Id,
                matriz.Clave,
                matriz.Descripcion,
                matriz.Unidad,
                esCuadrilla: false,
                esApu: true,
                matriz.CostoDirecto,
                form.ComponentesTempParaPrueba);

            MatrixApplicationService
                .SaveAsync(new ProjectDbContextFactory(), dbPath, dto, matriz)
                .GetAwaiter().GetResult();

            // Trabajo pendiente ajeno: una edición de proyecto y una edición de
            // otra matriz en el contexto de sesión. Nada de eso lo creó el editor.
            proyecto.Nombre = "borrador ajeno";
            var matrizAjena = new Matriz
            {
                Clave = "MAT-AJENA",
                Descripcion = "borrador ajeno",
                Unidad = "m2",
                Tipo = TipoMatriz.APU,
                ProyectoId = proyecto.Id,
                CostoDirecto = 1m,
                Notas = string.Empty,
                FechaCreacion = new DateTime(2026, 1, 1),
                FechaModificacion = new DateTime(2026, 1, 1),
            };
            ctx.Matrices.Add(matrizAjena);
            ctx.SaveChanges();
            matrizAjena.Descripcion = "borrador ajeno editado";

            form.ReconciliarRastreadosPropios(dto);

            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "El trabajo pendiente ajeno debe seguir pendiente tras la reconciliación.");
            Assert.AreEqual(EntityState.Modified, ctx.Entry(matrizAjena).State,
                "La guarda no debe alterar el estado de las entidades ajenas.");
            Assert.AreEqual("borrador ajeno editado", matrizAjena.Descripcion,
                "El borrador ajeno debe sobrevivir a la reconciliación.");
            Assert.AreEqual("borrador ajeno", proyecto.Nombre);
        });
    }

    [TestMethod]
    public void Guardar_ConMatrizYaModificadaEnSesion_LaGuardaBloqueaYLaReconciliacionPreservaElTrabajo()
    {
        EjecutarConEditor((ctx, dbPath, proyecto, matriz, form) =>
        {
            // Estado previo ajeno a esta edición: la entidad Matriz de la sesión ya
            // estaba modificada (p. ej. una escritura heredada del panel embebido
            // aún sin confirmar) cuando el editor abrió.
            matriz.Descripcion = "borrador previo ajeno";
            matriz.CostoDirecto = 999m;

            Assert.IsTrue(form.TieneCambiosPendientesEnLaMatriz(),
                "La guarda debe detectar la matriz ya modificada y bloquear el guardado antes del commit.");
            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "El bloqueo no debe guardar ni limpiar el trabajo pendiente.");

            // Defensa en profundidad: aunque la reconciliación se ejecutara con ese
            // estado, jamás toca una entrada Modified ajena ni pisa su trabajo con
            // los valores de esta edición.
            var dto = MatrixEditorService.BuildEditDto(
                proyecto.Id,
                matriz.Clave,
                "Descripción de esta edición",
                matriz.Unidad,
                esCuadrilla: false,
                esApu: true,
                CostoConfirmado,
                form.ComponentesTempParaPrueba);

            form.ReconciliarRastreadosPropios(dto);

            var entrada = ctx.Entry(matriz);
            Assert.AreEqual(EntityState.Modified, entrada.State,
                "El trabajo ajeno pendiente debe seguir rastreado como modificado.");
            Assert.AreEqual("borrador previo ajeno", matriz.Descripcion,
                "El borrador ajeno debe sobrevivir sin ser pisado por los valores de la edición.");
            Assert.AreEqual(999m, matriz.CostoDirecto,
                "El costo pendiente ajeno debe sobrevivir a la reconciliación.");
            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "El trabajo ajeno debe seguir pendiente.");

            // El lote reemplazado del editor sí queda Detached: sus datos ya
            // viajaron en el DTO del commit, y su estado residual solo envenenaría
            // un SaveChanges ajeno posterior.
            foreach (var componente in form.ComponentesTempParaPrueba)
            {
                Assert.AreEqual(EntityState.Detached, ctx.Entry(componente).State,
                    "El componente de trabajo del editor debe quedar Detached.");
            }
        });
    }

    [TestMethod]
    public void LaGuardaNoBloqueaLasEdicionesPropiasDeLosComponentesDeTrabajo()
    {
        EjecutarConEditor((ctx, dbPath, proyecto, matriz, form) =>
        {
            // Las copias de trabajo del editor son desacopladas (#121): las
            // ediciones propias jamás quedan pendientes en el tracker y jamás
            // bloquean el guardado.
            form.ComponentesTempParaPrueba.First().Cantidad = 7m;
            form.ComponentesTempParaPrueba.First().Importe = 17.5m;

            Assert.AreEqual(EntityState.Detached,
                ctx.Entry(form.ComponentesTempParaPrueba.First()).State,
                "El componente de trabajo debe estar desacoplado del tracker.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "La edición propia no debe dejar trabajo pendiente en el tracker.");

            Assert.IsFalse(form.TieneCambiosPendientesEnLaMatriz(),
                "La guarda sólo debe bloquear por cambios pendientes de la entidad Matriz, nunca por las ediciones propias de componentes.");
        });
    }

    [TestMethod]
    public void Guardar_ConComponenteEditadoYQuitadoDeLaLista_ElSaveChangesAjenoNoFallaYLosAjenosSobreviven()
    {
        EjecutarConEditor((ctx, dbPath, proyecto, matriz, form) =>
        {
            // El usuario edita un componente y luego lo quita de la lista del
            // editor (la misma ruta que la llamada de eliminación del grid). La
            // lista pierde la referencia, pero el contexto de sesión sigue
            // rastreando la entidad: su fila será eliminada por el commit.
            var listaTemp = (List<ComponenteMatriz>)form.ComponentesTempParaPrueba;
            var eliminado = listaTemp[0];
            eliminado.Cantidad = 5m;
            eliminado.Importe = 12.5m;

            Assert.IsTrue(MatrixComponentCollectionService.RemoveAt(listaTemp, 0),
                "La eliminación desde la lista debe replicar la ruta real del editor.");
            Assert.AreNotEqual(0, eliminado.Id);
            ctx.ChangeTracker.DetectChanges();
            Assert.AreEqual(EntityState.Detached, ctx.Entry(eliminado).State,
                "El componente quitado de la lista es una copia desacoplada; el tracker no lo rastrea.");

            // Trabajo pendiente ajeno que la reconciliación no debe tocar. Primero
            // se siembra la matriz ajena (con su propio SaveChanges) y después se
            // ensucian las entidades ajenas, para que ese SaveChanges de siembra no
            // confirme los borradores.
            var matrizAjena = new Matriz
            {
                Clave = "MAT-AJENA",
                Descripcion = "ajena",
                Unidad = "m2",
                Tipo = TipoMatriz.APU,
                ProyectoId = proyecto.Id,
                CostoDirecto = 1m,
                Notas = string.Empty,
                FechaCreacion = new DateTime(2026, 1, 1),
                FechaModificacion = new DateTime(2026, 1, 1),
            };
            matrizAjena.Componentes.Add(new ComponenteMatriz
            {
                TipoComponente = TipoComponenteMatriz.Material,
                MaterialId = ctx.Materiales.OrderBy(m => m.Id).First().Id,
                Cantidad = 2m,
                Importe = 20m,
                Orden = 1,
                Notas = string.Empty,
            });
            ctx.Matrices.Add(matrizAjena);
            ctx.SaveChanges();

            proyecto.Nombre = "borrador ajeno";
            var componenteAjeno = matrizAjena.Componentes.Single();
            componenteAjeno.Cantidad = 9m;
            ctx.ChangeTracker.DetectChanges();

            // Commit real por contexto por operación: elimina el lote completo de
            // la matriz (incluida la fila del componente quitado de la lista) e
            // inserta el lote restante.
            var dto = MatrixEditorService.BuildEditDto(
                proyecto.Id,
                matriz.Clave,
                matriz.Descripcion,
                matriz.Unidad,
                esCuadrilla: false,
                esApu: true,
                CostoConfirmado,
                form.ComponentesTempParaPrueba);
            Assert.AreEqual(1, dto.Componentes.Count,
                "El DTO debe capturar sólo el lote restante en la lista.");

            var idsOriginales = new[] { eliminado.Id, listaTemp[0].Id };

            var resultado = MatrixApplicationService
                .SaveAsync(new ProjectDbContextFactory(), dbPath, dto, matriz, ctx)
                .GetAwaiter().GetResult();
            Assert.IsFalse(resultado.IsNew);

            // La guarda del servicio, con el contexto de sesión, no toca el tracker:
            // el lote propio pendiente y el trabajo ajeno siguen pendientes.
            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "El guardado por operación no debe guardar ni limpiar el tracker de la sesión.");

            form.ReconciliarRastreadosPropios(dto);

            // El lote reemplazado completo queda Detached, incluido el componente
            // quitado de la lista que la sesión seguía rastreando.
            Assert.AreEqual(EntityState.Detached, ctx.Entry(eliminado).State,
                "El componente quitado de la lista debe quedar Detached tras la reconciliación.");
            Assert.AreEqual(EntityState.Detached, ctx.Entry(listaTemp[0]).State,
                "El componente restante reemplazado debe quedar Detached tras la reconciliación.");

            // Las entradas ajenas (otra matriz, otro proyecto) no se tocan.
            Assert.AreEqual(EntityState.Modified, ctx.Entry(componenteAjeno).State,
                "El componente ajeno no debe ser tocado por la reconciliación.");
            Assert.AreEqual(9m, componenteAjeno.Cantidad,
                "El borrador ajeno debe sobrevivir intacto.");
            Assert.AreEqual(EntityState.Modified, ctx.Entry(proyecto).State,
                "La edición ajena del proyecto debe seguir pendiente.");

            // Un SaveChanges ajeno del contexto de sesión ya no encuentra fantasmas
            // que actualizar contra filas inexistentes: no lanza
            // DbUpdateConcurrencyException y confirma sólo el trabajo ajeno.
            try
            {
                ctx.SaveChanges();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Assert.Fail($"El SaveChanges ajeno no debe fallar por fantasmas del lote reemplazado: {ex.Message}");
            }

            using (var lectura = new SOPROContext(dbPath))
            {
                var fresca = lectura.Matrices.Include(m => m.Componentes).Single(m => m.Id == matriz.Id);
                Assert.AreEqual(CostoConfirmado, fresca.CostoDirecto,
                    "El commit externo debe seguir intacto.");
                Assert.AreEqual(dto.Componentes.Count, fresca.Componentes.Count,
                    "El lote confirmado no debe cambiar tras el SaveChanges ajeno.");
                Assert.IsFalse(fresca.Componentes.Any(c => idsOriginales.Contains(c.Id)),
                    "Los registros reemplazados, incluido el quitado de la lista, deben seguir ausentes.");

                var ajenaFresca = lectura.Matrices.Include(m => m.Componentes).Single(m => m.Id == matrizAjena.Id);
                Assert.AreEqual(9m, ajenaFresca.Componentes.Single().Cantidad,
                    "El trabajo ajeno pendiente debe confirmarse normalmente, no perderse.");
            }
        });
    }

    private static int MaterialIdSesion(SOPROContext ctx, Matriz matriz)
    {
        return ctx.Materiales.OrderBy(m => m.Id).First().Id;
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static void EjecutarConEditor(
        Action<SOPROContext, string, Proyecto, Matriz, FormEditarMatriz> prueba)
    {
        using DirectorioTemporal temp = new DirectorioTemporal("editar_matriz_reconciliacion");
        using SOPROContext ctx = CrearContexto(temp.DbPath);
        Proyecto proyecto = AgregarProyecto(ctx);
        Matriz matriz = AgregarMatrizConMateriales(ctx, proyecto);

        Proyecto? proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        try
        {
            using FormEditarMatriz form = new FormEditarMatriz(ctx, proyecto.Id, matriz);
            prueba(ctx, temp.DbPath, proyecto, matriz, form);
        }
        finally
        {
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
        }

        Assert.AreSame(proyectoGlobalPrevio, LeerProyectoGlobalFormato(),
            "La prueba no debe dejar alterado el proyecto global de FormatoHelper.");
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)typeof(FormatoHelper)
            .GetField("_proyectoActual", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => typeof(FormatoHelper)
            .GetField("_proyectoActual", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(null, previo);

    private static SOPROContext CrearContexto(string dbPath)
    {
        SqliteConnection.ClearAllPools();
        SOPROContext ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static Proyecto AgregarProyecto(SOPROContext ctx)
    {
        Proyecto proyecto = new Proyecto
        {
            Nombre = "Proyecto reconciliación rastreados",
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
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto;
    }

    private static Matriz AgregarMatrizConMateriales(SOPROContext ctx, Proyecto proyecto)
    {
        var material = new Material
        {
            Clave = "MAT-1",
            Descripcion = "Cemento",
            Unidad = "kg",
            PrecioUnitario = 10m,
            ProyectoId = proyecto.Id,
            Notas = string.Empty,
        };
        ctx.Materiales.Add(material);
        ctx.SaveChanges();

        var matriz = new Matriz
        {
            Clave = "MAT-01",
            Descripcion = "Matriz de reconciliación",
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            ProyectoId = proyecto.Id,
            CostoDirecto = 100m,
            Notas = string.Empty,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Cantidad = 2m,
            Importe = 20m,
            Orden = 1,
            Notas = string.Empty,
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Cantidad = 1m,
            Importe = 10m,
            Orden = 2,
            Notas = string.Empty,
        });
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();
        return matriz;
    }

    private sealed class DirectorioTemporal : IDisposable
    {
        public string Ruta { get; }
        public string DbPath { get; }

        public DirectorioTemporal(string nombre)
        {
            Ruta = Path.Combine(Path.GetTempPath(), $"sopro_{nombre}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Ruta);
            DbPath = Path.Combine(Ruta, "proyecto-temporal.soproproj");
        }

        public void Dispose()
        {
            for (int intento = 0; intento < 5; intento++)
            {
                try
                {
                    SqliteConnection.ClearAllPools();
                    if (Directory.Exists(Ruta))
                        Directory.Delete(Ruta, recursive: true);
                    return;
                }
                catch (IOException) when (intento < 4)
                {
                    System.Threading.Thread.Sleep(200);
                }
            }
        }
    }
}
