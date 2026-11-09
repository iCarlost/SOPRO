using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Catalogos;

[STATestClass]
[DoNotParallelize]
public sealed class FormCalculoCostoHorarioVinculosTests
{
    [TestMethod]
    public void CargaVinculada_BloqueaPrecioYMuestraClave()
    {
        WithForm(true, true, (form, context, maquinaria, material) =>
        {
            Assert.IsFalse(Field<NumericUpDown>(form, "nudPrecioCombustible").Enabled);
            Assert.AreEqual("DIE-01", Field<Label>(form, "lblVinculoCombustible").Text);
        });
    }

    [TestMethod]
    public void MaquinariaManual_NoHabilitaSelector()
    {
        WithForm(false, false, (form, context, maquinaria, material) =>
            Assert.IsFalse(Field<Button>(form, "btnVincularCombustible").Enabled));
    }

    [TestMethod]
    public void FiltroDePropietario_ExcluyeOtroProyectoYMaestro()
    {
        WithForm(true, true, (form, context, maquinaria, material) =>
        {
            Assert.AreEqual(material.Id, maquinaria.MaterialCombustibleId);
            Assert.AreEqual(1, FormCalculoCostoHorario.ConsultarMaterialesDelPropietario(context.Materiales, maquinaria.ProyectoId).Count());
        });
    }

    [TestMethod]
    public void SelectorOperador_ConsultaSoloPropietarioExactoIncluyendoMaestro()
    {
        ManoDeObra[] personal =
        {
            new ManoDeObra { Clave = "LOCAL", ProyectoId = 4 },
            new ManoDeObra { Clave = "AJENO", ProyectoId = 5 },
            new ManoDeObra { Clave = "MAESTRO", ProyectoId = null }
        };

        Assert.AreEqual("LOCAL", FormCalculoCostoHorario.ConsultarOperadoresDelPropietario(personal.AsQueryable(), 4).Single().Clave);
        Assert.AreEqual("MAESTRO", FormCalculoCostoHorario.ConsultarOperadoresDelPropietario(personal.AsQueryable(), null).Single().Clave);
    }

    [TestMethod]
    public void MenuUsoMaquinaria_DevuelveSoloVinculosDelMaterialYOperador()
    {
        Maquinaria[] equipos =
        {
            new Maquinaria { Id = 1, ProyectoId = 4, MaterialCombustibleId = 8, ManoDeObraOperadorId = 9 },
            new Maquinaria { Id = 2, ProyectoId = 4, MaterialAceiteId = 8 },
            new Maquinaria { Id = 3, ProyectoId = 5, MaterialCombustibleId = 8, ManoDeObraOperadorId = 9 },
            new Maquinaria { Id = 4, ProyectoId = null, ManoDeObraOperadorId = 9 }
        };

        Assert.AreEqual(2, equipos.AsQueryable().Where(x => x.ProyectoId == 4 && (x.MaterialCombustibleId == 8 || x.MaterialAceiteId == 8)).Count());
        Assert.AreEqual(1, FormCatalogoManoObra.ConsultarMaquinariaOperador(equipos.AsQueryable(), 9, 4).Count());
        Assert.AreEqual(1, FormCatalogoManoObra.ConsultarMaquinariaOperador(equipos.AsQueryable(), 9, null).Count());
    }

    [TestMethod]
    public void MenuMaterial_MuestraRolesDeVinculoYNoComoMatriz()
    {
        string[] labels = FormCatalogoMateriales.CrearEtiquetasMaquinaria(new[]
        {
            new MaterialMachineryUsage(1, "M-01", "Excavadora", "combustible"),
            new MaterialMachineryUsage(2, "M-02", "Rodillo", "lubricante")
        }).ToArray();

        CollectionAssert.AreEqual(new[] { "M-01 — Excavadora · combustible", "M-02 — Rodillo · lubricante" }, labels);
    }

    [TestMethod]
    public void QuitarVinculo_NoMutaEntidadAntesDeGuardar()
    {
        WithForm(true, true, (form, context, maquinaria, material) =>
        {
            form.Show();
            Field<TabControl>(form, "tabControl").SelectedTab = Field<TabPage>(form, "tabConsumos");
            System.Windows.Forms.Application.DoEvents();
            Field<Button>(form, "btnQuitarCombustible").PerformClick();
            Assert.AreEqual(material.Id, maquinaria.MaterialCombustibleId);
            Assert.IsTrue(Field<NumericUpDown>(form, "nudPrecioCombustible").Enabled);
            Assert.AreEqual("Sin vínculo", Field<Label>(form, "lblVinculoCombustible").Text);
        });
    }

    [TestMethod]
    public void Guardado_ConservaValorFinalAlDesvincularAntesDeGuardar()
    {
        Maquinaria maquinaria = new Maquinaria
        {
            MaterialCombustibleId = 12,
            PrecioCombustible = 20m,
            MaterialAceiteId = 13,
            PrecioAceite = 30m,
            ManoDeObraOperadorId = 14,
            SalarioOperador = 100m,
            FactorSalarioReal = 1.5m
        };

        FormCalculoCostoHorario.AplicarPrecioManualSiEstaDesvinculado(maquinaria, null, true, 27m);
        FormCalculoCostoHorario.AplicarPrecioManualSiEstaDesvinculado(maquinaria, null, false, 91m);
        FormCalculoCostoHorario.AplicarOperadorManualSiEstaDesvinculado(maquinaria, null, 450m, 1.8m);

        Assert.AreEqual(27m, maquinaria.PrecioCombustible);
        Assert.AreEqual(91m, maquinaria.PrecioAceite);
        Assert.AreEqual(450m, maquinaria.SalarioOperador);
        Assert.AreEqual(1.8m, maquinaria.FactorSalarioReal);
        Assert.AreEqual(12, maquinaria.MaterialCombustibleId);
        Assert.AreEqual(13, maquinaria.MaterialAceiteId);
        Assert.AreEqual(14, maquinaria.ManoDeObraOperadorId);
    }

    private static T Field<T>(Form form, string name) where T : class
    {
        return (T)(form.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(form)
            ?? throw new InvalidOperationException(name));
    }

    private static void WithForm(bool calculated, bool linked, Action<FormCalculoCostoHorario, SOPROContext, Maquinaria, Material> test)
    {
        string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        using SOPROContext context = new SOPROContext(Path.Combine(folder, "links.db"));
        context.Database.EnsureCreated();
        var proyecto = new Proyecto { Nombre = "Proyecto de prueba", Descripcion = "Prueba", Ubicacion = "Local", Convocante = "", Contratista = "", ApoderadoLegal = "" };
        context.Proyectos.Add(proyecto);
        context.SaveChanges();
        var otroProyecto = new Proyecto { Nombre = "Otro", Descripcion = "Otro", Ubicacion = "Local", Convocante = "", Contratista = "", ApoderadoLegal = "" };
        context.Proyectos.Add(otroProyecto);
        context.SaveChanges();
        var material = new Material { Clave = "DIE-01", Descripcion = "Diésel", Unidad = "lt", PrecioUnitario = 23m, ProyectoId = proyecto.Id, Notas = "" };
        context.Materiales.Add(material);
        context.Materiales.Add(new Material { Clave = "OTRO", Descripcion = "Ajeno", Unidad = "lt", PrecioUnitario = 99m, ProyectoId = otroProyecto.Id, Notas = "" });
        context.Materiales.Add(new Material { Clave = "MASTER", Descripcion = "Maestro", Unidad = "lt", PrecioUnitario = 88m, ProyectoId = null, Notas = "" });
        context.SaveChanges();
        var maquinaria = new Maquinaria { Clave = "M-01", Descripcion = "Equipo", ProyectoId = proyecto.Id, EsCostoCalculado = calculated, MaterialCombustibleId = linked ? material.Id : null, PrecioCombustible = 20m };
        using var form = new FormCalculoCostoHorario(context, maquinaria);
        test(form, context, maquinaria, material);
    }
}
