using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// Invariantes estructurales del cromo compartido de los editores de catálogo
/// (issue #176): escala por DPI del tema, botones primario/secundario reales
/// enlazados a Enter/Esc, y todo campo visible dentro de la zona útil entre el
/// encabezado y el pie (ni solapado por el encabezado ni recortado al fondo).
/// Son invariantes de bounds y tipos, no de píxeles ni colores exactos.
/// </summary>
[STATestClass]
[DoNotParallelize] // Los forms de WinForms requieren un hilo STA dedicado.
public class CromoEditoresCatalogoTests
{
    [TestMethod]
    public void EditoresMaterial_CromoCompartido_CamposDentroDeLaZonaUtil()
    {
        EjecutarConContexto(ctx =>
        {
            using FormEditarMaterial form = new FormEditarMaterial(
                LegacySessionBridge.FromLegacy(ctx, null),
                new MaterialListItem(1, "MAT-1", "Material de prueba", "kg", 1234.5678m, string.Empty, OrigenInsumo.Proyecto));
            AfirmarCromo(form, new[] { "txtClave", "txtDescripcion", "txtUnidad", "nudPrecio", "txtNotas" });
            AfirmarGrupoOrigenOculto(form);
        });
    }

    [TestMethod]
    public void EditoresManoObra_CromoCompartido_CamposDentroDeLaZonaUtil()
    {
        EjecutarConContexto(ctx =>
        {
            using FormEditarManoObra form = new FormEditarManoObra(
                ctx,
                null,
                new ManoDeObra
                {
                    Id = 2,
                    Clave = "MO-1",
                    Descripcion = "Mano de obra de prueba",
                    Unidad = "jor",
                    SalarioBase = 1234.56m,
                    FactorSalarioReal = 1m,
                    SalarioReal = 1234.56m,
                    Notas = string.Empty,
                    Origen = OrigenInsumo.Proyecto,
                });
            AfirmarCromo(form, new[] { "txtClave", "txtDescripcion", "cboUnidad", "nudSalarioBase", "nudFSR", "nudSalarioReal", "txtNotas" });
            AfirmarGrupoOrigenOculto(form);
        });
    }

    [TestMethod]
    public void EditoresHerramienta_CromoCompartido_CamposDentroDeLaZonaUtil()
    {
        EjecutarConContexto(ctx =>
        {
            using FormEditarHerramienta form = new FormEditarHerramienta(
                ctx,
                0,
                new Herramienta
                {
                    Id = 3,
                    ProyectoId = 0,
                    Clave = "HER-1",
                    Descripcion = "Herramienta de prueba",
                    Unidad = "pza",
                    PrecioUnitario = 1234.56m,
                    Notas = string.Empty,
                    Origen = OrigenInsumo.Proyecto,
                });
            AfirmarCromo(form, new[] { "txtClave", "txtDescripcion", "cboUnidad", "nudPrecio" });
        });
    }

    [TestMethod]
    public void EditoresMaquinaria_CromoCompartido_CamposYBotonCalcularDentroDeSuPadre()
    {
        EjecutarConContexto(ctx =>
        {
            using FormEditarMaquinaria form = new FormEditarMaquinaria(
                ctx,
                null,
                new Maquinaria
                {
                    Id = 4,
                    Clave = "MAQ-1",
                    Descripcion = "Maquinaria de prueba",
                    CostoHorario = 1234.5678m,
                    EsCostoCalculado = false,
                    Notas = string.Empty,
                    Origen = OrigenInsumo.Proyecto,
                });
            AfirmarCromo(form, new[] { "txtClave", "txtDescripcion", "nudPotencia", "cboTipoCombustible", "txtNotas" });
            AfirmarGrupoOrigenOculto(form);

            // El botón Calcular debe caber COMPLETO dentro del grupo de costo,
            // con margen derecho visible (regresión: quedaba recortado a
            // "Calcular C…" por su posición absoluta heredada del diseñador).
            GroupBox grupo = LeerCampo<GroupBox>(form, "grpCosto");
            SoproButton calcular = LeerCampo<SoproButton>(form, "btnCalcular");
            Assert.IsTrue(grupo.Visible, "El grupo de costo horario debe ser visible.");
            Assert.IsTrue(calcular.Bounds.Left >= grupo.ClientRectangle.Left,
                "El botón Calcular no debe salir por la izquierda del grupo.");
            Assert.IsTrue(calcular.Bounds.Top >= grupo.ClientRectangle.Top,
                "El botón Calcular no debe salir por arriba del grupo.");
            Assert.IsTrue(calcular.Bounds.Bottom <= grupo.ClientRectangle.Bottom,
                "El botón Calcular no debe salir por abajo del grupo.");
            Assert.IsTrue(calcular.Bounds.Right <= grupo.ClientRectangle.Right - 8,
                $"El botón Calcular debe caber completo con margen derecho: right={calcular.Right}, grupo={grupo.ClientSize.Width}.");

            // Sus controles internos también: costo, fecha y advertencia.
            AfirmarContenidoEnPadre(form, grupo, new[] { "nudCostoHorario", "label4", "label6", "lblFechaCalculo" });
        });
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    /// <summary>
    /// Cromo compartido: escala Dpi del tema, bandas de encabezado y pie
    /// acopladas, botones primario/secundario reales (SoproButton) enlazados a
    /// AcceptButton/CancelButton, y todos los campos indicados visibles y
    /// enteros dentro de la zona útil (bajo el encabezado, sobre el pie).
    /// </summary>
    private static void AfirmarCromo(Form form, string[] nombresCampos)
    {
        string editor = form.GetType().Name;

        form.Show();
        for (int i = 0; i < 5; i++)
            System.Windows.Forms.Application.DoEvents();

        Assert.AreEqual(AutoScaleMode.Dpi, form.AutoScaleMode,
            $"{editor}: el cromo compartido debe escalar por DPI.");

        Control encabezado = form.Controls.OfType<Control>().SingleOrDefault(c => c.Dock == DockStyle.Top && c.Visible)
            ?? throw new AssertFailedException($"{editor}: falta la banda de encabezado acoplada arriba.");
        Control pie = form.Controls.OfType<Control>().SingleOrDefault(c => c.Dock == DockStyle.Bottom && c.Visible)
            ?? throw new AssertFailedException($"{editor}: falta la banda de pie acoplada abajo.");

        Button btnGuardar = LeerCampo<Button>(form, "btnGuardar");
        Button btnCancelar = LeerCampo<Button>(form, "btnCancelar");
        Assert.IsInstanceOfType(btnGuardar, typeof(SoproButton), $"{editor}: Guardar debe ser un botón SOPRO primario real.");
        Assert.IsInstanceOfType(btnCancelar, typeof(SoproButton), $"{editor}: Cancelar debe ser un botón SOPRO secundario real.");
        Assert.AreSame(btnGuardar, form.AcceptButton, $"{editor}: Guardar debe ser el AcceptButton real (Enter).");
        Assert.AreSame(btnCancelar, form.CancelButton, $"{editor}: Cancelar debe ser el CancelButton real (Escape).");

        foreach (string nombre in nombresCampos)
            AfirmarCampoEnZonaUtil(form, encabezado, pie, nombre);
    }

    private static void AfirmarCampoEnZonaUtil(Form form, Control encabezado, Control pie, string nombreCampo)
    {
        Control campo = LeerCampo<Control>(form, nombreCampo);
        string editor = $"{form.GetType().Name}.{nombreCampo}";

        Assert.IsTrue(campo.Visible, $"{editor}: el campo debe ser visible con el formulario mostrado.");

        var rect = form.RectangleToClient(campo.Parent!.RectangleToScreen(campo.Bounds));

        Assert.IsTrue(rect.Top >= encabezado.Bottom,
            $"{editor}: el campo queda solapado por el encabezado (top={rect.Top}, encabezado hasta {encabezado.Bottom}).");
        Assert.IsTrue(rect.Bottom <= pie.Top,
            $"{editor}: el campo queda recortado por el pie (bottom={rect.Bottom}, pie desde {pie.Top}).");
        Assert.IsTrue(rect.Left >= 0,
            $"{editor}: el campo queda recortado por la izquierda (left={rect.Left}).");
        Assert.IsTrue(rect.Right <= form.ClientSize.Width,
            $"{editor}: el campo queda recortado por la derecha (right={rect.Right}, cliente={form.ClientSize.Width}).");
    }

    private static void AfirmarContenidoEnPadre(Form form, Control padre, string[] nombresControles)
    {
        foreach (string nombre in nombresControles)
        {
            Control control = LeerCampo<Control>(form, nombre);
            if (!control.Visible)
                continue;

            Assert.IsTrue(padre.ClientRectangle.Contains(control.Bounds),
                $"{form.GetType().Name}.{nombre}: el control debe caber completo dentro de {padre.Name} (bounds={control.Bounds}, cliente={padre.ClientRectangle}).");
        }
    }

    /// <summary>
    /// El grupo de destino del guardado existe, sigue oculto por diseño y
    /// conserva sus radios funcionales (la decisión de destino la toma el
    /// alcance de la sesión, no el usuario).
    /// </summary>
    private static void AfirmarGrupoOrigenOculto(Form form)
    {
        GroupBox grupoOrigen = LeerCampo<GroupBox>(form, "grpOrigen");
        Assert.IsNotNull(grupoOrigen, $"{form.GetType().Name}: grpOrigen debe existir.");
        Assert.IsFalse(grupoOrigen.Visible, $"{form.GetType().Name}: grpOrigen permanece oculto por diseño.");
        Assert.AreEqual(2, grupoOrigen.Controls.OfType<RadioButton>().Count(),
            $"{form.GetType().Name}: grpOrigen conserva sus dos radios funcionales.");
    }

    private static T LeerCampo<T>(Form form, string nombre) where T : class
    {
        FieldInfo? campo = form.GetType().GetField(
            nombre,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (campo == null)
            throw new InvalidOperationException($"No existe el campo '{nombre}' en {form.GetType().Name}.");

        return campo.GetValue(form) as T
            ?? throw new InvalidOperationException($"El campo '{nombre}' de {form.GetType().Name} no está inicializado.");
    }

    private static void EjecutarConContexto(Action<SOPROContext> prueba)
    {
        string root = Path.Combine(Path.GetTempPath(), $"sopro_cromo_editores_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string dbPath = Path.Combine(root, "catalogo-temporal.db");

        SOPROContext? contexto = null;
        try
        {
            contexto = new SOPROContext(dbPath);
            contexto.Database.EnsureCreated();
            prueba(contexto);
        }
        finally
        {
            contexto?.Dispose();
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
