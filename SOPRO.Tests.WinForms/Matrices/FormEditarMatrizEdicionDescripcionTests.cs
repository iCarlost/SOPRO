using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
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
/// Staging de descripciones por fuente en el editor de matrices (#121). La
/// edición de descripción NO muta la entidad de la fuente en la sesión: se
/// registra por (TipoComponente, SourceId) —incluido Auxiliar, cuya fuente es
/// otra Matriz—, viaja como parche explícito en el DTO y la Application lo
/// confirma en la misma transacción del commit. Unidad y P.U. son de sólo
/// lectura en este editor.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper; contextos SQLite temporales.
public class FormEditarMatrizEdicionDescripcionTests
{
    private const string DescripcionCementoOriginal = "Cemento";

    [TestMethod]
    public void NuevaMatriz_EditarDescripcionYCantidad_ElCommitPersisteAmbosYElTrackerQuedaLimpio()
    {
        EjecutarConNuevaMatriz((ctx, dbPath, proyecto, material, form, grid) =>
        {
            InyectarComponenteMaterial(form, material, cantidad: 3m, importe: 30m);
            ReconstruirGrid(form);

            AplicarEdicionCelda(form, grid, "colCantidad", 0, "7");
            AplicarEdicionCelda(form, grid, "colDescripcion", 0, "Cemento modificado");

            var staging = form.EdicionesDescripcionParaPrueba;
            Assert.AreEqual(1, staging.Count, "La edición de descripción debe quedar en el staging.");
            var (original, propuesta) = staging[(TipoComponenteMatriz.Material, material.Id)];
            Assert.AreEqual(DescripcionCementoOriginal, original, "El baseline es la descripción original de la fuente.");
            Assert.AreEqual("Cemento modificado", propuesta);

            // El staging y el componente de trabajo no tocan el tracker: la fuente
            // rastreada por la sesión queda intacta.
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(material).State,
                "La fuente rastreada por la sesión no debe ser mutada por el editor.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Ni el staging ni la copia de trabajo deben ensuciar el tracker.");

            var dto = ConstruirDtoReal(form);
            Assert.AreEqual(1, dto.SourceDescriptionEdits.Count,
                "El DTO debe llevar el parche de descripción.");

            var resultado = MatrixApplicationService
                .SaveAsync(new ProjectDbContextFactory(), dbPath, dto, null, ctx)
                .GetAwaiter().GetResult();
            Assert.IsTrue(resultado.IsNew);

            form.ReconciliarRastreadosPropios(dto);
            form.ReconciliarDescripcionesConfirmadas();

            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El guardado normal no debe dejar cambios pendientes en el tracker.");
            Assert.IsFalse(CatalogoBatchDeletionHelper.HayCambiosSinGuardar(ctx),
                "El tracker limpio no debe bloquear operaciones posteriores por contexto nuevo.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(material).State,
                "La fuente rastreada por la sesión debe seguir limpia.");
            Assert.AreEqual("Cemento modificado", form.ComponentesTempParaPrueba[0].Material!.Descripcion,
                "La copia de trabajo del editor debe reflejar la descripción confirmada.");

            using (var lectura = new SOPROContext(dbPath))
            {
                var nueva = lectura.Matrices.Include(m => m.Componentes).Single(m => m.Id == resultado.MatrixId);
                var componente = nueva.Componentes.Single();
                Assert.AreEqual(7m, componente.Cantidad,
                    "La cantidad editada debe persistir con el commit.");
                Assert.AreEqual(material.Id, componente.MaterialId);
                Assert.AreEqual("Cemento modificado",
                    lectura.Materiales.Single(m => m.Id == material.Id).Descripcion,
                    "La descripción de la fuente debe persistir con el commit.");
            }
        });
    }

    [TestMethod]
    public void MatrizExistente_EditarDescripcionYQuitarComponente_LaDescripcionPersisteYElTrackerQuedaLimpio()
    {
        EjecutarConEditor((ctx, dbPath, proyecto, matriz, form, grid) =>
        {
            var material = form.ComponentesTempParaPrueba[0].Material!;

            AplicarEdicionCelda(form, grid, "colDescripcion", 0, "Cemento modificado");
            Assert.AreEqual(1, form.EdicionesDescripcionParaPrueba.Count,
                "El staging debe registrar el parche antes de la eliminación.");

            // Quitar TODAS las filas de la fuente editada: el staging sobrevive a
            // la eliminación y el parche persiste aunque ninguna fila lo
            // referencie ya (comp1 fila 0 y comp3 fila 2, ambas cemento).
            var listaTemp = (List<ComponenteMatriz>)form.ComponentesTempParaPrueba;
            var eliminado3 = listaTemp[2];
            var eliminado1 = listaTemp[0];
            Assert.IsTrue(MatrixComponentCollectionService.RemoveAt(listaTemp, 2),
                "La eliminación debe replicar la ruta real del editor.");
            Assert.AreEqual(1, form.EdicionesDescripcionParaPrueba.Count,
                "El staging debe sobrevivir a la eliminación de una fila de la misma fuente.");
            Assert.IsTrue(MatrixComponentCollectionService.RemoveAt(listaTemp, 0),
                "La eliminación debe replicar la ruta real del editor.");
            Assert.AreEqual(1, form.EdicionesDescripcionParaPrueba.Count,
                "El staging debe sobrevivir incluso sin filas de la fuente.");

            var dto = ConstruirDtoReal(form);
            Assert.AreEqual(1, dto.Componentes.Count,
                "El DTO debe capturar sólo el lote restante en la lista.");
            Assert.AreEqual(1, dto.SourceDescriptionEdits.Count,
                "El parche de descripción debe viajar aunque la fuente ya no tenga filas.");

            var resultado = MatrixApplicationService
                .SaveAsync(new ProjectDbContextFactory(), dbPath, dto, matriz, ctx)
                .GetAwaiter().GetResult();
            Assert.IsFalse(resultado.IsNew);

            form.ReconciliarRastreadosPropios(dto);
            form.ReconciliarDescripcionesConfirmadas();

            Assert.AreEqual(EntityState.Detached, ctx.Entry(eliminado1).State,
                "El componente quitado debe quedar Detached tras la reconciliación.");
            Assert.AreEqual(EntityState.Detached, ctx.Entry(eliminado3).State,
                "El componente quitado debe quedar Detached tras la reconciliación.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Sin trabajo ajeno, el tracker debe quedar limpio tras guardar.");
            Assert.IsFalse(CatalogoBatchDeletionHelper.HayCambiosSinGuardar(ctx),
                "La eliminación posterior de matrices no debe bloquearse tras este guardado.");
            Assert.AreEqual(EntityState.Detached, ctx.Entry(material).State,
                "La copia de trabajo del editor está desacoplada del tracker.");
            Assert.AreEqual(DescripcionCementoOriginal, material.Descripcion,
                "La copia de la fila quitada no se reconcilia (ya no hay filas de la fuente); el valor confirmado vive en la base.");

            using (var lectura = new SOPROContext(dbPath))
            {
                Assert.AreEqual(1, lectura.Matrices.Include(m => m.Componentes)
                    .Single(m => m.Id == matriz.Id).Componentes.Count,
                    "El lote confirmado debe reflejar la eliminación de las dos filas.");
                Assert.AreEqual("Cemento modificado",
                    lectura.Materiales.Single(m => m.Id == material.Id).Descripcion,
                    "La descripción de la fuente debe persistir aunque ninguna fila la referencie.");
            }
        });
    }

    [TestMethod]
    public void MismaFuenteEnDosFilas_ElTextoSeComparteYGeneraUnSoloParche()
    {
        EjecutarConEditor((ctx, dbPath, proyecto, matriz, form, grid) =>
        {
            // comp1 (fila 0) y comp3 (fila 2) referencian el mismo material.
            var material = form.ComponentesTempParaPrueba[0].Material!;

            AplicarEdicionCelda(form, grid, "colDescripcion", 0, "Cemento modificado");

            Assert.AreEqual(1, form.EdicionesDescripcionParaPrueba.Count,
                "Dos filas con la misma fuente producen un solo parche.");
            Assert.AreEqual("Cemento modificado", grid.Rows[2].Cells["colDescripcion"].Value,
                "El rebuild debe superponer el borrador en la otra fila de la misma fuente.");

            AplicarEdicionCelda(form, grid, "colDescripcion", 2, "Cemento mejorado");

            var staging = form.EdicionesDescripcionParaPrueba;
            Assert.AreEqual(1, staging.Count, "Reeditar la misma fuente actualiza el único parche.");
            var (original, propuesta) = staging[(TipoComponenteMatriz.Material, material.Id)];
            Assert.AreEqual(DescripcionCementoOriginal, original);
            Assert.AreEqual("Cemento mejorado", propuesta);

            Assert.AreEqual("Cemento mejorado", grid.Rows[0].Cells["colDescripcion"].Value,
                "El texto más reciente debe verse en todas las filas de la fuente.");
            Assert.AreEqual("Cemento mejorado", grid.Rows[2].Cells["colDescripcion"].Value);

            var dto = ConstruirDtoReal(form);
            Assert.AreEqual(1, dto.SourceDescriptionEdits.Count);
            Assert.AreEqual("Cemento mejorado", dto.SourceDescriptionEdits.Single().NewDescription);

            MatrixApplicationService
                .SaveAsync(new ProjectDbContextFactory(), dbPath, dto, matriz, ctx)
                .GetAwaiter().GetResult();

            form.ReconciliarRastreadosPropios(dto);
            form.ReconciliarDescripcionesConfirmadas();

            Assert.IsFalse(ctx.ChangeTracker.HasChanges());
            using (var lectura = new SOPROContext(dbPath))
            {
                Assert.AreEqual("Cemento mejorado",
                    lectura.Materiales.Single(m => m.Id == material.Id).Descripcion,
                    "El parche único debe confirmar la última propuesta.");
            }
        });
    }

    [TestMethod]
    public void EditarDescripcionDeVueltaAlOriginal_EliminaElParche()
    {
        EjecutarConEditor((ctx, dbPath, proyecto, matriz, form, grid) =>
        {
            var comp = form.ComponentesTempParaPrueba[0];

            form.EstablecerDescripcionComponente(comp, "Cemento modificado");
            Assert.AreEqual(1, form.EdicionesDescripcionParaPrueba.Count);

            form.EstablecerDescripcionComponente(comp, "  Cemento  ");
            Assert.AreEqual(0, form.EdicionesDescripcionParaPrueba.Count,
                "Volver al original (tras el recorte) debe eliminar el parche.");

            ReconstruirGrid(form);
            Assert.AreEqual(DescripcionCementoOriginal, grid.Rows[0].Cells["colDescripcion"].Value,
                "Sin parche, el grid muestra la descripción de la fuente.");

            var dto = ConstruirDtoReal(form);
            Assert.AreEqual(0, dto.SourceDescriptionEdits.Count,
                "Sin parche, el DTO no debe llevar ediciones de descripción.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El staging no ensucia el tracker ni al registrarse ni al descartarse.");
        });
    }

    [TestMethod]
    public void CancelarSinGuardar_LaFuenteYElTrackerQuedanIntactos()
    {
        EjecutarConEditor((ctx, dbPath, proyecto, matriz, form, grid) =>
        {
            var material = form.ComponentesTempParaPrueba[0].Material!;

            AplicarEdicionCelda(form, grid, "colDescripcion", 0, "Cemento modificado");

            Assert.AreEqual(1, form.EdicionesDescripcionParaPrueba.Count,
                "El staging existe mientras el editor no confirme ni cancele.");
            Assert.AreEqual(EntityState.Detached, ctx.Entry(material).State,
                "La copia de trabajo del editor está desacoplada del tracker.");
            Assert.AreEqual(DescripcionCementoOriginal, material.Descripcion,
                "La descripción de la copia de trabajo sigue intacta sin guardado.");

            var rastreada = ctx.Materiales.First(m => m.Id == material.Id);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(rastreada).State,
                "La fuente rastreada por la sesión no debe mutarse sin guardado.");
            Assert.AreEqual(DescripcionCementoOriginal, rastreada.Descripcion,
                "La descripción rastreada sigue intacta en la sesión.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Sin guardado, el tracker debe seguir limpio: el staging vive sólo en el editor.");
            Assert.IsFalse(CatalogoBatchDeletionHelper.HayCambiosSinGuardar(ctx),
                "Cancelar sin guardar no debe dejar bloqueos derivados del editor.");

            using (var lectura = new SOPROContext(dbPath))
            {
                Assert.AreEqual(DescripcionCementoOriginal,
                    lectura.Materiales.Single(m => m.Id == material.Id).Descripcion,
                    "La base no debe cambiar sin guardado.");
            }
        });
    }

    [TestMethod]
    public void Reconciliacion_PreservaPropiedadAjenaModificada_MientrasSincronizaLaDescripcion()
    {
        EjecutarConEditor((ctx, dbPath, proyecto, matriz, form, grid) =>
        {
            // Trabajo ajeno REAL sobre la instancia rastreada por la sesión: otra
            // flujo del contexto compartido dejó Notas pendiente. La copia de
            // trabajo del editor es otra instancia, desacoplada.
            var material = form.ComponentesTempParaPrueba[0].Material!;
            var rastreada = ctx.Materiales.First(m => m.Id == material.Id);
            Assert.AreNotSame(rastreada, material,
                "La carga del editor debe ser desacoplada: otra instancia que la rastreada.");

            rastreada.Notas = "nota ajena";
            ctx.ChangeTracker.DetectChanges();
            Assert.AreEqual(EntityState.Modified, ctx.Entry(rastreada).State,
                "El borrador ajeno sobre otra propiedad deja la entrada Modified.");

            AplicarEdicionCelda(form, grid, "colDescripcion", 0, "Cemento modificado");

            var dto = ConstruirDtoReal(form);
            MatrixApplicationService
                .SaveAsync(new ProjectDbContextFactory(), dbPath, dto, matriz, ctx)
                .GetAwaiter().GetResult();

            form.ReconciliarRastreadosPropios(dto);
            form.ReconciliarDescripcionesConfirmadas();

            var entrada = ctx.Entry(rastreada);
            Assert.AreEqual(EntityState.Modified, entrada.State,
                "El trabajo ajeno pendiente debe seguir rastreado como modificado.");
            Assert.IsFalse(entrada.Property(nameof(Material.Descripcion)).IsModified,
                "La descripción reconciliada no debe quedar como pendiente.");
            Assert.AreEqual("Cemento modificado", rastreada.Descripcion,
                "La descripción rastreada debe reflejar el valor confirmado en la base.");
            Assert.AreEqual("nota ajena", rastreada.Notas,
                "El borrador ajeno debe sobrevivir a la reconciliación.");
            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "El aviso de cambios pendientes sigue siendo legítimo por el trabajo ajeno.");
            Assert.IsTrue(CatalogoBatchDeletionHelper.HayCambiosSinGuardar(ctx),
                "Con trabajo ajeno real, la eliminación posterior sigue bloqueada.");
            Assert.AreEqual("Cemento modificado", material.Descripcion,
                "La copia de trabajo del editor también se sincroniza con lo confirmado.");

            ctx.SaveChanges();
            using (var lectura = new SOPROContext(dbPath))
            {
                var fresco = lectura.Materiales.Single(m => m.Id == material.Id);
                Assert.AreEqual("Cemento modificado", fresco.Descripcion,
                    "El SaveChanges ajeno no debe pisar la descripción confirmada.");
                Assert.AreEqual("nota ajena", fresco.Notas,
                    "El borrador ajeno debe confirmarse normalmente.");
            }
        });
    }

    [TestMethod]
    public void UnidadYPU_NoSonEditablesEnEsteEditor()
    {
        EjecutarConEditor((ctx, dbPath, proyecto, matriz, form, grid) =>
        {
            var material = form.ComponentesTempParaPrueba[0].Material!;

            Assert.IsTrue(grid.Columns["colUnidad"].ReadOnly,
                "Unidad debe ser de sólo lectura en el editor de matrices.");
            Assert.IsTrue(grid.Columns["colPU"].ReadOnly,
                "P.U. debe ser de sólo lectura en el editor de matrices.");
            Assert.IsFalse(grid.Columns["colDescripcion"].ReadOnly,
                "La descripción sigue siendo editable.");
            Assert.IsFalse(grid.Columns["colCantidad"].ReadOnly,
                "La cantidad sigue siendo editable.");

            grid.CurrentCell = grid.Rows[0].Cells["colUnidad"];
            Assert.IsFalse(grid.BeginEdit(true), "Unidad debe rechazar la edición.");
            grid.CurrentCell = grid.Rows[0].Cells["colPU"];
            Assert.IsFalse(grid.BeginEdit(true), "P.U. debe rechazar la edición.");

            grid.CurrentCell = grid.Rows[0].Cells["colDescripcion"];
            Assert.IsTrue(grid.BeginEdit(true), "La descripción debe seguir editándose.");
            grid.CancelEdit();

            Assert.AreEqual(0, form.EdicionesDescripcionParaPrueba.Count,
                "Ninguna edición debe haberse registrado.");
            Assert.AreEqual(EntityState.Detached, ctx.Entry(material).State,
                "La copia de trabajo del editor está desacoplada del tracker.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());
        });
    }

    [TestMethod]
    public void EditarCantidadLuegoCancelar_ElTrackerQuedaLimpioYUnSaveChangesAjenoNoPersisteLaCantidad()
    {
        EjecutarConEditor((ctx, dbPath, proyecto, matriz, form, grid) =>
        {
            var componente = form.ComponentesTempParaPrueba[0];
            var material = componente.Material!;

            AplicarEdicionCelda(form, grid, "colCantidad", 0, "7");
            Assert.AreEqual(7m, componente.Cantidad,
                "La edición queda en la copia de trabajo desacoplada.");

            Assert.AreEqual(EntityState.Detached, ctx.Entry(componente).State,
                "La copia de trabajo del componente debe estar desacoplada.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Editar cantidad en la copia desacoplada no debe dejar trabajo pendiente.");

            // Cancelar el editor y que OTRO módulo guarde la sesión después: la
            // cantidad cancelada no debe colarse en ese guardado.
            proyecto.Nombre = "borrador ajeno";
            ctx.ChangeTracker.DetectChanges();
            ctx.SaveChanges();

            using (var lectura = new SOPROContext(dbPath))
            {
                var confirmado = lectura.ComponentesMatriz.Single(c => c.Id == componente.Id);
                Assert.AreEqual(2m, confirmado.Cantidad,
                    "La cantidad cancelada no debe persistirse por un SaveChanges ajeno.");
                Assert.AreEqual(20m, confirmado.Importe);
                Assert.AreEqual("borrador ajeno",
                    lectura.Proyectos.Single(p => p.Id == proyecto.Id).Nombre,
                    "El trabajo ajeno sí debe confirmarse con ese guardado.");
                Assert.AreEqual(DescripcionCementoOriginal,
                    lectura.Materiales.Single(m => m.Id == material.Id).Descripcion,
                    "La fuente debe seguir intacta.");
            }
        });
    }

    [TestMethod]
    public void Guardar_CantidadYDescripcion_EnMatrizExistente_PersisteAmbosYElTrackerQuedaLimpio()
    {
        EjecutarConEditor((ctx, dbPath, proyecto, matriz, form, grid) =>
        {
            var material = form.ComponentesTempParaPrueba[0].Material!;

            AplicarEdicionCelda(form, grid, "colCantidad", 0, "7");
            AplicarEdicionCelda(form, grid, "colDescripcion", 0, "Cemento modificado");

            Assert.AreEqual(7m, form.ComponentesTempParaPrueba[0].Cantidad);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Las ediciones en copias desacopladas no ensucian el tracker.");

            var dto = ConstruirDtoReal(form);
            var resultado = MatrixApplicationService
                .SaveAsync(new ProjectDbContextFactory(), dbPath, dto, matriz, ctx)
                .GetAwaiter().GetResult();
            Assert.IsFalse(resultado.IsNew);

            form.ReconciliarRastreadosPropios(dto);
            form.ReconciliarDescripcionesConfirmadas();

            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El guardado normal no debe dejar cambios pendientes en el tracker.");
            Assert.IsFalse(CatalogoBatchDeletionHelper.HayCambiosSinGuardar(ctx),
                "La eliminación posterior de matrices no debe bloquearse.");

            using (var lectura = new SOPROContext(dbPath))
            {
                var comp = lectura.ComponentesMatriz
                    .Single(c => c.MatrizId == matriz.Id && c.MaterialId == material.Id && c.Cantidad == 7m);
                Assert.AreEqual(70m, comp.Importe,
                    "El importe recalculado con el precio persistido debe confirmarse.");
                Assert.AreEqual("Cemento modificado",
                    lectura.Materiales.Single(m => m.Id == material.Id).Descripcion,
                    "La descripción editada debe persistir con el commit.");
            }
        });
    }

    [TestMethod]
    public void PrecioPendienteAjenoEnSesion_ElEditorUsaElPrecioPersistido()
    {
        EjecutarConEditor((ctx, dbPath, proyecto, matriz, form, grid) =>
        {
            var material = form.ComponentesTempParaPrueba[0].Material!;
            var rastreada = ctx.Materiales.First(m => m.Id == material.Id);
            Assert.AreNotSame(rastreada, material,
                "La carga del editor debe ser desacoplada de la instancia rastreada.");

            // Pendiente ajeno previo: el precio en el tracker de la sesión es 99,
            // la base sigue en 10. El editor debe trabajar con la base.
            rastreada.PrecioUnitario = 99m;
            ctx.ChangeTracker.DetectChanges();
            Assert.AreEqual(EntityState.Modified, ctx.Entry(rastreada).State);

            Assert.AreEqual(10m, material.PrecioUnitario,
                "La copia de trabajo debe basarse en el precio persistido, no en el pendiente.");
            Assert.AreEqual(20m, form.ComponentesTempParaPrueba[0].Importe,
                "El recálculo del editor usa el precio persistido.");
            Assert.AreEqual(10m, Convert.ToDecimal(grid.Rows[0].Cells["colPU"].Value),
                "El grid muestra el precio persistido.");
            Assert.AreEqual(20m, Convert.ToDecimal(grid.Rows[0].Cells["colImporte"].Value),
                "El grid muestra el importe con el precio persistido.");

            var dto = ConstruirDtoReal(form);
            Assert.AreEqual(20m, dto.Componentes.Single(c => c.MaterialId == material.Id && c.Orden == 1).Importe,
                "El DTO del editor usa el precio persistido.");

            AplicarEdicionCelda(form, grid, "colDescripcion", 0, "Cemento modificado");
            dto = ConstruirDtoReal(form);
            MatrixApplicationService
                .SaveAsync(new ProjectDbContextFactory(), dbPath, dto, matriz, ctx)
                .GetAwaiter().GetResult();
            form.ReconciliarRastreadosPropios(dto);
            form.ReconciliarDescripcionesConfirmadas();

            Assert.AreEqual(EntityState.Modified, ctx.Entry(rastreada).State,
                "El precio pendiente ajeno debe seguir pendiente en la sesión.");
            Assert.AreEqual(99m, rastreada.PrecioUnitario,
                "El pendiente ajeno no debe ser tocado por el editor.");

            ctx.SaveChanges();
            using (var lectura = new SOPROContext(dbPath))
            {
                Assert.AreEqual(20m, lectura.ComponentesMatriz
                    .Single(c => c.MatrizId == matriz.Id && c.MaterialId == material.Id && c.Orden == 1).Importe,
                    "El commit confirma el importe con el precio persistido, no el pendiente.");
                var fresco = lectura.Materiales.Single(m => m.Id == material.Id);
                Assert.AreEqual(99m, fresco.PrecioUnitario,
                    "El pendiente ajeno se confirma con el SaveChanges de la sesión.");
                Assert.AreEqual("Cemento modificado", fresco.Descripcion,
                    "La descripción editada persiste con el commit.");
            }
        });
    }

    [TestMethod]
    public void SeleccionConFuenteRastreadaPendiente_LaNormalizacionUsaElSnapshotPersistido()
    {
        EjecutarConNuevaMatriz((ctx, dbPath, proyecto, material, form, grid) =>
        {
            // Pendiente ajeno sobre la fuente rastreada por la sesión.
            material.PrecioUnitario = 99m;
            ctx.ChangeTracker.DetectChanges();
            Assert.AreEqual(EntityState.Modified, ctx.Entry(material).State);

            // Simulación de la salida del selector: componente nuevo con la
            // NAVEGACIÓN rastreada (el valor pendiente) y la selección del usuario.
            var seleccion = new List<ComponenteMatriz>
            {
                new ComponenteMatriz
                {
                    TipoComponente = TipoComponenteMatriz.Material,
                    MaterialId = material.Id,
                    Material = material,
                    Cantidad = 3m,
                    Importe = 297m,
                    Orden = 1,
                    Notas = "nota elegida",
                },
            };

            form.NormalizarFuentesSeleccionadas(seleccion);

            var comp = seleccion.Single();
            Assert.AreNotSame(material, comp.Material,
                "La normalización debe reemplazar la navegación rastreada por una instantánea desacoplada.");
            Assert.AreEqual(EntityState.Detached, ctx.Entry(comp.Material).State,
                "La instantánea de la fuente no debe estar rastreada.");
            Assert.AreEqual(10m, comp.Material!.PrecioUnitario,
                "La normalización debe usar el precio persistido, no el pendiente.");
            Assert.AreEqual(3m, comp.Cantidad, "La cantidad seleccionada debe conservarse.");
            Assert.AreEqual(1, comp.Orden, "El orden seleccionado debe conservarse.");
            Assert.AreEqual("nota elegida", comp.Notas, "Las notas seleccionadas deben conservarse.");

            var totales = MatrixComponentCalculationService.RecalculateParaVista(seleccion, 2);
            Assert.AreEqual(30m, comp.Importe,
                "El cálculo del editor usa la instantánea persistida (3 x 10), no el pendiente.");
            Assert.AreEqual(30m, totales.CostoDirectoTotal);

            Assert.AreEqual(EntityState.Modified, ctx.Entry(material).State,
                "El pendiente ajeno debe seguir pendiente y fuera del editor.");
            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "El trabajo ajeno de la sesión debe preservarse intacto.");
        });
    }

    [TestMethod]
    public void AuxiliarComoFuente_EditarSuDescripcion_PersisteEnLaMatrizAuxiliarYElTrackerQuedaLimpio()
    {
        EjecutarConMatrizConAuxiliar((ctx, dbPath, proyecto, matriz, auxiliar, form, grid) =>
        {
            AplicarEdicionCelda(form, grid, "colDescripcion", 0, "Auxiliar renombrado");

            var staging = form.EdicionesDescripcionParaPrueba;
            Assert.AreEqual(1, staging.Count, "El staging debe registrar la fuente Auxiliar (Matriz).");
            var (original, propuesta) = staging[(TipoComponenteMatriz.Auxiliar, auxiliar.Id)];
            Assert.AreEqual("Básico de apoyo", original);
            Assert.AreEqual("Auxiliar renombrado", propuesta);

            var dto = ConstruirDtoReal(form);
            Assert.AreEqual(1, dto.SourceDescriptionEdits.Count);
            Assert.AreEqual(TipoComponenteMatriz.Auxiliar, dto.SourceDescriptionEdits.Single().TipoComponente);

            var resultado = MatrixApplicationService
                .SaveAsync(new ProjectDbContextFactory(), dbPath, dto, matriz, ctx)
                .GetAwaiter().GetResult();
            Assert.IsFalse(resultado.IsNew);

            form.ReconciliarRastreadosPropios(dto);
            form.ReconciliarDescripcionesConfirmadas();

            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "La reconciliación de la Matriz auxiliar debe dejar el tracker limpio.");
            Assert.IsFalse(CatalogoBatchDeletionHelper.HayCambiosSinGuardar(ctx));
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(auxiliar).State,
                "La Matriz auxiliar rastreada por la sesión debe seguir limpia.");
            Assert.AreEqual("Auxiliar renombrado", form.ComponentesTempParaPrueba[0].Auxiliar!.Descripcion,
                "La copia de trabajo del editor debe reflejar la descripción confirmada.");

            using (var lectura = new SOPROContext(dbPath))
            {
                Assert.AreEqual("Auxiliar renombrado",
                    lectura.Matrices.Single(m => m.Id == auxiliar.Id).Descripcion,
                    "La descripción del auxiliar debe persistir con el commit.");
                Assert.AreEqual("Matriz principal",
                    lectura.Matrices.Single(m => m.Id == matriz.Id).Descripcion,
                    "La matriz editada no debe cambiar su propia descripción.");
            }
        });
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static MatrixEditDto ConstruirDtoReal(FormEditarMatriz form)
    {
        var metodo = typeof(FormEditarMatriz).GetMethod(
                "BuildMatrixEditDto", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró la construcción del DTO.");
        return (MatrixEditDto)metodo.Invoke(form, Array.Empty<object?>())!;
    }

    private static void ReconstruirGrid(FormEditarMatriz form)
    {
        var metodo = typeof(FormEditarMatriz).GetMethod(
                "ActualizarGridComponentes", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró la reconstrucción del grid.");
        object?[] parametros = metodo.GetParameters().Length == 0
            ? Array.Empty<object?>()
            : new object?[] { null };
        metodo.Invoke(form, parametros);
    }

    private static void InyectarComponenteMaterial(FormEditarMatriz form, Material material, decimal cantidad, decimal importe)
    {
        var campo = typeof(FormEditarMatriz).GetField(
                "_componentesTemp", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró la colección temporal del editor.");
        var listaTemp = (List<ComponenteMatriz>)(campo.GetValue(form)
            ?? throw new InvalidOperationException("La colección temporal no está disponible."));
        listaTemp.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Material = material,
            Cantidad = cantidad,
            Importe = importe,
            Orden = 1,
            Notas = string.Empty,
        });
    }

    /// <summary>
    /// Invoca el manejador real de CellEndEdit del editor tras fijar el valor de
    /// la celda: la misma ruta que la edición interactiva.
    /// </summary>
    private static void AplicarEdicionCelda(
        FormEditarMatriz form, DataGridView grid, string columna, int fila, string valor)
    {
        int indice = grid.Columns[columna]!.Index;
        grid.Rows[fila].Cells[indice].Value = valor;

        var manejador = typeof(FormEditarMatriz).GetMethod(
                "DgvComponentes_CellEndEdit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró el manejador de fin de edición.");

        manejador.Invoke(form, new object[] { grid, new DataGridViewCellEventArgs(indice, fila) });
        System.Windows.Forms.Application.DoEvents();
    }

    private static void BombearMensajes()
        => System.Windows.Forms.Application.DoEvents();

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

    private static void EjecutarConEditor(
        Action<SOPROContext, string, Proyecto, Matriz, FormEditarMatriz, DataGridView> prueba)
    {
        using DirectorioTemporal temp = new DirectorioTemporal("editar_matriz_descripcion");
        using SOPROContext ctx = CrearContexto(temp.DbPath);
        Proyecto proyecto = AgregarProyecto(ctx);
        Matriz matriz = AgregarMatrizConMateriales(ctx, proyecto);

        Proyecto? proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        try
        {
            using FormEditarMatriz form = new FormEditarMatriz(ctx, proyecto.Id, matriz)
            {
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-4000, -4000),
                ShowInTaskbar = false,
            };
            form.Show();
            BombearMensajes();

            prueba(ctx, temp.DbPath, proyecto, matriz, form, form.DgvComponentesParaPrueba);
        }
        finally
        {
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
        }

        Assert.AreSame(proyectoGlobalPrevio, LeerProyectoGlobalFormato(),
            "La prueba no debe dejar alterado el proyecto global de FormatoHelper.");
    }

    private static void EjecutarConNuevaMatriz(
        Action<SOPROContext, string, Proyecto, Material, FormEditarMatriz, DataGridView> prueba)
    {
        using DirectorioTemporal temp = new DirectorioTemporal("editar_matriz_descripcion_nueva");
        using SOPROContext ctx = CrearContexto(temp.DbPath);
        Proyecto proyecto = AgregarProyecto(ctx);
        Material material = AgregarMaterial(ctx, proyecto, "MAT-CEM", DescripcionCementoOriginal, "kg", 10m);

        Proyecto? proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        try
        {
            using FormEditarMatriz form = new FormEditarMatriz(ctx, proyecto.Id)
            {
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-4000, -4000),
                ShowInTaskbar = false,
            };
            form.Show();
            BombearMensajes();

            prueba(ctx, temp.DbPath, proyecto, material, form, form.DgvComponentesParaPrueba);
        }
        finally
        {
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
        }

        Assert.AreSame(proyectoGlobalPrevio, LeerProyectoGlobalFormato(),
            "La prueba no debe dejar alterado el proyecto global de FormatoHelper.");
    }

    private static void EjecutarConMatrizConAuxiliar(
        Action<SOPROContext, string, Proyecto, Matriz, Matriz, FormEditarMatriz, DataGridView> prueba)
    {
        using DirectorioTemporal temp = new DirectorioTemporal("editar_matriz_descripcion_auxiliar");
        using SOPROContext ctx = CrearContexto(temp.DbPath);
        Proyecto proyecto = AgregarProyecto(ctx);

        Matriz auxiliar = new Matriz
        {
            Clave = "BAS-01",
            Descripcion = "Básico de apoyo",
            Unidad = "m2",
            Tipo = TipoMatriz.Basico,
            ProyectoId = proyecto.Id,
            CostoDirecto = 5m,
            Notas = string.Empty,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.Matrices.Add(auxiliar);

        Matriz matriz = new Matriz
        {
            Clave = "MAT-01",
            Descripcion = "Matriz principal",
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            ProyectoId = proyecto.Id,
            CostoDirecto = 5m,
            Notas = string.Empty,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();

        ctx.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = auxiliar.Id,
            Cantidad = 1m,
            Importe = 5m,
            Orden = 1,
            Notas = string.Empty,
        });
        ctx.SaveChanges();

        Proyecto? proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        try
        {
            using FormEditarMatriz form = new FormEditarMatriz(ctx, proyecto.Id, matriz)
            {
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-4000, -4000),
                ShowInTaskbar = false,
            };
            form.Show();
            BombearMensajes();

            prueba(ctx, temp.DbPath, proyecto, matriz, auxiliar, form, form.DgvComponentesParaPrueba);
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
            .GetField("_proyectoActual", BindingFlags.Static | BindingFlags.NonPublic)
            ?.GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => typeof(FormatoHelper)
            .GetField("_proyectoActual", BindingFlags.Static | BindingFlags.NonPublic)
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
            Nombre = "Proyecto edición descripciones",
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

    private static Material AgregarMaterial(
        SOPROContext ctx, Proyecto proyecto, string clave, string descripcion, string unidad, decimal precio)
    {
        Material material = new Material
        {
            Clave = clave,
            Descripcion = descripcion,
            Unidad = unidad,
            PrecioUnitario = precio,
            ProyectoId = proyecto.Id,
            Notas = string.Empty,
        };
        ctx.Materiales.Add(material);
        ctx.SaveChanges();
        return material;
    }

    private static Matriz AgregarMatrizConMateriales(SOPROContext ctx, Proyecto proyecto)
    {
        Material cemento = AgregarMaterial(ctx, proyecto, "MAT-CEM", DescripcionCementoOriginal, "kg", 10m);
        Material arena = AgregarMaterial(ctx, proyecto, "MAT-ARE", "Arena", "m3", 8m);

        Matriz matriz = new Matriz
        {
            Clave = "MAT-01",
            Descripcion = "Matriz de edición de descripciones",
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            ProyectoId = proyecto.Id,
            CostoDirecto = 28m,
            Notas = string.Empty,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = cemento.Id,
            Cantidad = 2m,
            Importe = 20m,
            Orden = 1,
            Notas = string.Empty,
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = arena.Id,
            Cantidad = 1m,
            Importe = 8m,
            Orden = 2,
            Notas = string.Empty,
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = cemento.Id,
            Cantidad = 1m,
            Importe = 10m,
            Orden = 3,
            Notas = string.Empty,
        });
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();
        return matriz;
    }
}
