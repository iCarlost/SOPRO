using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// Alineación Origen vs propiedad:
///   1. Alternancia de edición/operación de los catálogos por PROPIEDAD
///      (ProyectoId/alcance), no por Origen: una copia importada del maestro
///      (Origen = Maestro, ProyectoId = proyecto) es de este proyecto y se edita.
///   2. Etiqueta de procedencia: solo la marca reconocida en Notas habla de
///      importación; sin marca, la propiedad decide entre "Local" y "Maestro" —
///      nunca un reclamo falso de "Catálogo Maestro" sobre una fila del proyecto.
///   3. Los editores no marcan "Guardar en Maestro" a partir de un Origen
///      desactualizado de una fila propiedad del proyecto.
/// </summary>
[STATestClass]
[DoNotParallelize] // Los forms de WinForms requieren un hilo STA dedicado.
public class OrigenYProcedenciaCatalogosTests
{
    // ═════════════════════ Procedencia visible (etiqueta de la columna Origen) ═════════════════════

    [TestMethod]
    public void Procedencia_ConMarcaReconocida_MuestraImportadoConElNombreOrigen()
    {
        Assert.AreEqual("Importado de: Obra Norte",
            EtiquetaProcedenciaCatalogoHelper.ParaFila("[IMPORTADO DE: Obra Norte] notas restantes", 5),
            "La marca en Notas manda y nombra al catálogo de procedencia.");
    }

    [TestMethod]
    public void Procedencia_FilaDelProyectoSinMarca_EtiquetaLocalNuncaMaestro()
    {
        // Fila propiedad del proyecto (aunque conserve Origen = Maestro de una
        // copia importada sin marca o de datos legacy): nunca puede decir "Maestro".
        Assert.AreEqual("Local", EtiquetaProcedenciaCatalogoHelper.ParaFila(string.Empty, 5));
    }

    [TestMethod]
    public void Procedencia_FilaDelMaestroSinMarca_EtiquetaMaestro()
    {
        Assert.AreEqual("Maestro", EtiquetaProcedenciaCatalogoHelper.ParaFila("cualquier nota", null));
    }

    // ═════════════════════ Elegibilidad de edición (alcance, no Origen) ═════════════════════

    [TestMethod]
    public void Catalogos_CopiaImportadaDelMaestro_HabilitaEdicionSinMirarOrigen()
    {
        using var ctx = CrearContexto();
        var proyecto = AgregarProyecto(ctx);

        using (var form = new FormCatalogoMateriales(ctx, proyecto.Id))
        {
            var filaImportada = new MaterialListItem(1, "MAT-1", "Copia importada", "kg", 10m, "IMPORTADO", OrigenInsumo.Maestro);
            EnlazarYSeleccionarPrimeraFila(form.GridPrincipal, new List<MaterialListItem> { filaImportada });

            Assert.IsTrue(BotonEditar(form).Enabled,
                "Materiales: una copia importada (Origen Maestro) cargada de su proyecto debe poder editarse.");

            var seleccionada = form.ObtenerMaterialesSeleccionados().Single();
            Assert.IsTrue(form.PuedeEditar(seleccionada),
                "Menú contextual: la elegibilidad de Editar no debe depender del Origen desactualizado.");
        }

        using (var form = new FormCatalogoMaquinaria(ctx, proyecto.Id))
        {
            EnlazarYSeleccionarPrimeraFila(form.GridPrincipal, new List<Maquinaria>
            {
                new() { Id = 1, ProyectoId = proyecto.Id, Clave = "MAQ-1", Descripcion = "Copia importada", Origen = OrigenInsumo.Maestro },
            });

            Assert.IsTrue(BotonEditar(form).Enabled,
                "Maquinaria: una copia importada (Origen Maestro) con ProyectoId local debe poder editarse.");
            Assert.IsTrue(BotonPorNombre(form, "btnCalcularCosto").Enabled,
                "Maquinaria: el costo horario de una copia importada del proyecto debe poder calcularse.");
        }

        using (var form = new FormCatalogoManoObra(ctx, proyecto.Id))
        {
            EnlazarYSeleccionarPrimeraFila(form.GridPrincipal, new List<ManoDeObra>
            {
                new() { Id = 1, ProyectoId = proyecto.Id, Clave = "MO-1", Descripcion = "Copia importada", Unidad = "jor", Origen = OrigenInsumo.Maestro },
            });

            Assert.IsTrue(BotonEditar(form).Enabled,
                "Mano de obra: una copia importada (Origen Maestro) con ProyectoId local debe poder editarse.");
        }

        using (var form = new FormCatalogoHerramientas(ctx, proyecto.Id))
        {
            EnlazarYSeleccionarPrimeraFila(form.GridPrincipal, new List<Herramienta>
            {
                new() { Id = 1, ProyectoId = proyecto.Id, Clave = "H-1", Descripcion = "Copia importada", Unidad = "hr", Origen = OrigenInsumo.Maestro },
            });

            Assert.IsTrue(BotonEditar(form).Enabled,
                "Herramientas: una copia importada (Origen Maestro) con ProyectoId local debe poder editarse.");
        }
    }

    // ═════════════════════ Editores: destino del guardado por propiedad, no Origen ═════════════════════

    [TestMethod]
    public void EditorMaquinaria_CopiaImportadaEnProyecto_GuardaEnProyectoNoEnMaestro()
    {
        using var ctx = CrearContexto();
        var maquinaria = new Maquinaria
        {
            Clave = "MAQ-IMP",
            Descripcion = "Copia importada",
            Origen = OrigenInsumo.Maestro, // desactualizado: la copia vive en el proyecto
            ProyectoId = 1,
        };

        using var form = new FormEditarMaquinaria(ctx, 1, maquinaria);

        Assert.IsTrue(RadioPorNombre(form, "rbProyecto").Checked,
            "Una fila propiedad del proyecto debe guardarse en el proyecto aunque conserve Origen = Maestro.");
        Assert.IsFalse(RadioPorNombre(form, "rbMaestro").Checked,
            "El Origen desactualizado no debe enviar la edición al catálogo maestro.");
    }

    [TestMethod]
    public void EditorManoObra_CopiaImportadaEnProyecto_GuardaEnProyectoNoEnMaestro()
    {
        using var ctx = CrearContexto();
        var manoObra = new ManoDeObra
        {
            Clave = "MO-IMP",
            Descripcion = "Copia importada",
            Unidad = "jor",
            Origen = OrigenInsumo.Maestro,
            ProyectoId = 1,
        };

        using var form = new FormEditarManoObra(ctx, 1, manoObra);

        Assert.IsTrue(RadioPorNombre(form, "rbProyecto").Checked,
            "Una fila propiedad del proyecto debe guardarse en el proyecto aunque conserve Origen = Maestro.");
        Assert.IsFalse(RadioPorNombre(form, "rbMaestro").Checked,
            "El Origen desactualizado no debe enviar la edición al catálogo maestro.");
    }

    [TestMethod]
    public void EditorMaquinaria_SesionMaestra_MantieneDestinoMaestro()
    {
        using var ctx = CrearContexto();
        var maquinaria = new Maquinaria
        {
            Clave = "MAQ-MAE",
            Descripcion = "Registro del maestro",
            Origen = OrigenInsumo.Maestro,
            ProyectoId = null,
        };

        using var form = new FormEditarMaquinaria(ctx, null, maquinaria);

        Assert.IsTrue(RadioPorNombre(form, "rbMaestro").Checked,
            "En sesión maestra el guardado sigue dirigido al catálogo maestro.");
    }

    // ═════════════════════ Procedencia en reportes PDF (maquinaria) ═════════════════════

    [TestMethod]
    public void PdfCostoHorario_CopiaImportadaDelProyecto_ProcedenciaLocalNuncaMaestro()
    {
        var copia = new Maquinaria
        {
            Clave = "MAQ-IMP",
            Descripcion = "Copia importada",
            Notas = string.Empty, // sin marca: la propiedad de la fila manda
            Origen = OrigenInsumo.Maestro, // desactualizado
            ProyectoId = 5,
        };

        Assert.AreEqual("Local", MaquinariaReportValues.ResolveTexto(copia, "Origen"),
            "Una fila propiedad del proyecto nunca debe etiquetarse como 'Maestro' por su Origen desactualizado.");
    }

    [TestMethod]
    public void PdfCostoHorario_ConMarcaReconocida_NombraElProyectoOrigen()
    {
        var copia = new Maquinaria
        {
            Clave = "MAQ-EXT",
            Descripcion = "Importación externa",
            Notas = "[IMPORTADO DE: Obra Norte]",
            ProyectoId = 5,
        };

        Assert.AreEqual("Importado de: Obra Norte",
            MaquinariaReportValues.ResolveTexto(copia, "Origen"),
            "La marca en Notas manda y nombra al catálogo de procedencia.");
    }

    [TestMethod]
    public void PdfCostoHorario_FilaDelMaestroSinMarca_ProcedenciaMaestro()
    {
        var maestro = new Maquinaria
        {
            Clave = "MAQ-MAE",
            Descripcion = "Registro del maestro",
            Notas = string.Empty,
            ProyectoId = null,
        };

        Assert.AreEqual("Maestro", MaquinariaReportValues.ResolveTexto(maestro, "Origen"));
    }

    // ═════════════════════ Infraestructura ═════════════════════

    private static void EnlazarYSeleccionarPrimeraFila<T>(DataGridView grid, IReadOnlyList<T> filas)
    {
        grid.BindingContext = new BindingContext();
        grid.DataSource = filas.ToList();
        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;

        // Sin handle los eventos de selección no disparan; el catálogo resuelve
        // el estado de los botones en este método (mismo seam que la prueba de
        // eliminación en lote).
        var form = grid.FindForm();
        form?.GetType()
            .GetMethod("ActualizarEstadoBotones", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(form, null);
    }

    private static ToolStripButton BotonEditar(Form form)
        => (ToolStripButton)BotonPorNombre(form, "btnEditar");

    private static ToolStripButton BotonPorNombre(Form form, string nombre)
        => form.GetType()
               .GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic)
               ?.GetValue(form) as ToolStripButton
           ?? throw new InvalidOperationException($"No se encontró el campo '{nombre}' del formulario.");

    private static RadioButton RadioPorNombre(Form form, string nombre)
    {
        var radios = form.Controls.Find(nombre, searchAllChildren: true);
        return radios.Length > 0 ? (RadioButton)radios[0]
            : throw new InvalidOperationException($"No se encontró el control '{nombre}'.");
    }

    private static Proyecto AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto origen y procedencia",
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

    private static SOPROContext CrearContexto()
    {
        var dbPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"sopro_origen_proc_{Guid.NewGuid():N}.db");
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }
}
