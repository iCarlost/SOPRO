using System;
using System.Collections.Generic;
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
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// Regresión de atajos y ayudas visuales de los editores y catálogos:
///   1. Los cuatro editores usan los botones reales Guardar/Cancelar como
///      AcceptButton/CancelButton y sus notas/descripción multilínea aceptan Enter.
///   2. Los controles monetarios seleccionan todo su texto al recibir el foco
///      (constructores conectados al helper) y un clic posterior ya enfocado no
///      fuerza la reselección completa.
///   3. Los tooltips de las barras de catálogo son exactos: sólo anuncian los
///      atajos aceptados (Insert/Enter/Supr) y el resto son descripciones.
///   4. El ribbon de proyecto anuncia F9 para recalcular el módulo activo.
///   5. Los controles monetarios que no aplican siguen ocultos o deshabilitados.
/// </summary>
[STATestClass]
[DoNotParallelize] // Los forms de WinForms requieren un hilo STA dedicado.
public class AtajosCatalogosTests
{
    private const string AtajoNuevo = "Nuevo (Insert)";
    private const string AtajoNueva = "Nueva (Insert)";
    private const string AtajoEditar = "Editar (Enter)";
    private const string AtajoEliminar = "Eliminar (Supr)";
    private const string Importar = "Importar catálogo";
    private const string ConfigurarColumnas = "Configurar columnas";
    private const string Refrescar = "Refrescar catálogo";
    private const string Cerrar = "Cerrar catálogo";
    private const string CalcularCosto = "Calcular costo horario";

    [TestMethod]
    public void Editores_DefinenBotonAceptarCancelarYNotasMultilinea()
    {
        EjecutarConContexto(ctx =>
        {
            using FormEditarMaterial material = new FormEditarMaterial(
                LegacySessionBridge.FromLegacy(ctx, null),
                new MaterialListItem(1, "MAT-1", "Material de prueba", "kg", 1234.5678m, string.Empty, OrigenInsumo.Proyecto));
            AfirmarBotoneraYNotasMultilinea(material, "txtNotas");

            using FormEditarManoObra manoObra = new FormEditarManoObra(
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
            AfirmarBotoneraYNotasMultilinea(manoObra, "txtNotas");

            using FormEditarHerramienta herramienta = new FormEditarHerramienta(
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
            AfirmarBotoneraYNotasMultilinea(herramienta, "txtDescripcion");

            using FormEditarMaquinaria maquinaria = new FormEditarMaquinaria(
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
            AfirmarBotoneraYNotasMultilinea(maquinaria, "txtNotas");
        });
    }

    [TestMethod]
    public void Editores_SeleccionanTodoElTextoMonetarioAlRecibirFocoYNoLoReseleccionanConClicPosterior()
    {
        EjecutarConContexto(ctx =>
        {
            using FormEditarMaterial material = new FormEditarMaterial(
                LegacySessionBridge.FromLegacy(ctx, null),
                new MaterialListItem(1, "MAT-1", "Material de prueba", "kg", 1234.5678m, string.Empty, OrigenInsumo.Proyecto));
            AfirmarSeleccionMonetariaYNoReseleccion(material, "nudPrecio", "txtDescripcion");

            using FormEditarManoObra manoObra = new FormEditarManoObra(
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
            AfirmarSeleccionMonetariaYNoReseleccion(manoObra, "nudSalarioBase", "txtDescripcion");

            using FormEditarHerramienta herramienta = new FormEditarHerramienta(
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
            AfirmarSeleccionMonetariaYNoReseleccion(herramienta, "nudPrecio", "txtDescripcion");

            using FormEditarMaquinaria maquinaria = new FormEditarMaquinaria(
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
            AfirmarSeleccionMonetariaYNoReseleccion(maquinaria, "nudCostoHorario", "txtDescripcion");
        });
    }

    [TestMethod]
    public void CatalogoMateriales_TooltipsExactosDeLaBarra()
    {
        EjecutarConContexto(ctx =>
        {
            using FormCatalogoMateriales form = new FormCatalogoMateriales(ctx);
            AfirmarTooltips(
                form,
                "panelToolbar",
                new Dictionary<string, string>
                {
                    ["btnNuevo"] = AtajoNuevo,
                    ["btnEditar"] = AtajoEditar,
                    ["btnEliminar"] = AtajoEliminar,
                    ["btnImportar"] = Importar,
                    ["btnConfigColumnas"] = ConfigurarColumnas,
                    ["btnCerrar"] = Cerrar,
                },
                esperaRefrescarOculto: true);
        });
    }

    [TestMethod]
    public void CatalogoManoObra_TooltipsExactosDeLaBarra()
    {
        EjecutarConContexto(ctx =>
        {
            using FormCatalogoManoObra form = new FormCatalogoManoObra(ctx);
            AfirmarTooltips(
                form,
                "panelToolbar",
                new Dictionary<string, string>
                {
                    ["btnNuevo"] = AtajoNuevo,
                    ["btnEditar"] = AtajoEditar,
                    ["btnEliminar"] = AtajoEliminar,
                    ["btnImportar"] = Importar,
                    ["btnConfigColumnas"] = ConfigurarColumnas,
                    ["btnCerrar"] = Cerrar,
                },
                esperaRefrescarOculto: true);
        });
    }

    [TestMethod]
    public void CatalogoMaquinaria_TooltipsExactosDeLaBarra()
    {
        EjecutarConContexto(ctx =>
        {
            using FormCatalogoMaquinaria form = new FormCatalogoMaquinaria(ctx);
            AfirmarTooltips(
                form,
                "panelToolbar",
                new Dictionary<string, string>
                {
                    ["btnNuevo"] = AtajoNuevo,
                    ["btnEditar"] = AtajoEditar,
                    ["btnEliminar"] = AtajoEliminar,
                    ["btnImportar"] = Importar,
                    ["btnCalcularCosto"] = CalcularCosto,
                    ["btnConfigColumnas"] = ConfigurarColumnas,
                    ["btnCerrar"] = Cerrar,
                },
                esperaRefrescarOculto: true);
        });
    }

    [TestMethod]
    public void CatalogoHerramientas_TooltipsExactosDeLaBarra()
    {
        EjecutarConContexto(ctx =>
        {
            using FormCatalogoHerramientas form = new FormCatalogoHerramientas(ctx);
            AfirmarTooltips(
                form,
                "panelBotones",
                new Dictionary<string, string>
                {
                    ["btnNuevo"] = AtajoNueva,
                    ["btnEditar"] = AtajoEditar,
                    ["btnEliminar"] = AtajoEliminar,
                    ["btnImportar"] = Importar,
                    ["btnConfigColumnas"] = ConfigurarColumnas,
                    ["btnCerrar"] = Cerrar,
                },
                esperaRefrescarOculto: false);
        });
    }

    [TestMethod]
    public void FormProyecto_RibbonRecalcular_AnunciaElAtajoF9()
    {
        EjecutarConContexto(ctx =>
        {
            using FormProyecto form = new FormProyecto(ctx, new Proyecto
            {
                Id = 1,
                Nombre = "Proyecto de prueba",
                Descripcion = string.Empty,
                Ubicacion = string.Empty,
                Convocante = string.Empty,
                Contratista = string.Empty,
                ApoderadoLegal = string.Empty,
            });

            SoproButton btnRecalcular = LeerCampo<SoproButton>(form, "btnRecalcularRibbon");
            ToolTip toolTip = LeerCampo<ToolTip>(form, "toolTipRibbon");

            Assert.AreEqual("btnRecalcularRibbon", btnRecalcular.Name, "El control debe ser el botón real del ribbon.");
            Assert.AreEqual(
                "Recalcular el módulo activo (F9)",
                toolTip.GetToolTip(btnRecalcular),
                "El ribbon debe anunciar exactamente el atajo F9 del recálculo.");
        });
    }

    [TestMethod]
    public void EditorHerramientaPorcentajeMO_MantieneOcultoElPrecio()
    {
        EjecutarConContexto(ctx =>
        {
            using FormEditarHerramienta form = new FormEditarHerramienta(
                ctx,
                0,
                new Herramienta
                {
                    Id = 5,
                    ProyectoId = 0,
                    Clave = "HER-%MO",
                    Descripcion = "Herramienta menor de prueba",
                    Unidad = "%MO",
                    PrecioUnitario = 14.11m,
                    Notas = string.Empty,
                    Origen = OrigenInsumo.Proyecto,
                });

            form.Show();
            ProcesarMensajes();

            NumericUpDown precio = LeerCampo<NumericUpDown>(form, "nudPrecio");
            Label etiqueta = LeerCampo<Label>(form, "lblPrecio");
            Assert.IsFalse(precio.Visible, "Una herramienta %MO no captura precio.");
            Assert.IsFalse(etiqueta.Visible, "La etiqueta de precio no aplica a una herramienta %MO.");
            Assert.AreEqual(14.11m, precio.Value, "El valor heredado se conserva aunque el control esté oculto.");
        });
    }

    [TestMethod]
    public void EditorMaquinariaCostoCalculado_MantieneDeshabilitadoElCosto()
    {
        EjecutarConContexto(ctx =>
        {
            using FormEditarMaquinaria form = new FormEditarMaquinaria(
                ctx,
                null,
                new Maquinaria
                {
                    Id = 6,
                    Clave = "MAQ-CALC",
                    Descripcion = "Maquinaria con costo calculado",
                    CostoHorario = 1234.5678m,
                    EsCostoCalculado = true,
                    Notas = string.Empty,
                    Origen = OrigenInsumo.Proyecto,
                });

            form.Show();
            ProcesarMensajes();

            NumericUpDown costo = LeerCampo<NumericUpDown>(form, "nudCostoHorario");
            Assert.IsFalse(costo.Enabled, "En modo calculado el costo horario no se captura a mano.");
            Assert.AreEqual(1234.5678m, costo.Value, "El costo calculado persistido se conserva.");
        });
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static void AfirmarBotoneraYNotasMultilinea(Form form, string campoTextoMultilinea)
    {
        Button btnGuardar = LeerCampo<Button>(form, "btnGuardar");
        Button btnCancelar = LeerCampo<Button>(form, "btnCancelar");
        TextBox textoMultilinea = LeerCampo<TextBox>(form, campoTextoMultilinea);

        Assert.AreSame(btnGuardar, form.AcceptButton, $"{form.GetType().Name}: Guardar debe ser el AcceptButton real.");
        Assert.AreSame(btnCancelar, form.CancelButton, $"{form.GetType().Name}: Cancelar debe ser el CancelButton real.");
        Assert.AreEqual(DialogResult.Cancel, btnCancelar.DialogResult, $"{form.GetType().Name}: Cancelar debe devolver DialogResult.Cancel.");
        Assert.IsTrue(textoMultilinea.Multiline, $"{form.GetType().Name}: {campoTextoMultilinea} debe ser multilínea.");
        Assert.IsTrue(textoMultilinea.AcceptsReturn, $"{form.GetType().Name}: {campoTextoMultilinea} debe aceptar Enter.");
    }

    private static void AfirmarSeleccionMonetariaYNoReseleccion(
        Form form,
        string campoMonetario,
        string campoOtroControl)
    {
        NumericUpDown monetario = LeerCampo<NumericUpDown>(form, campoMonetario);
        TextBox otro = LeerCampo<TextBox>(form, campoOtroControl);

        form.Show();
        form.Activate();
        ProcesarMensajes();

        otro.Focus();
        ProcesarMensajes();
        Assert.IsTrue(otro.Focused, $"{form.GetType().Name}: no se pudo enfocar {campoOtroControl} para probar el cambio de foco.");

        TextBox edicion = ObtenerEdicion(monetario);
        Assert.IsFalse(string.IsNullOrEmpty(edicion.Text), $"{form.GetType().Name}: {campoMonetario} debe mostrar un valor.");
        Assert.IsTrue(
            TieneHelperDeSeleccionConfigurado(monetario),
            $"{form.GetType().Name}: el constructor debe conectar {campoMonetario} a SeleccionTotalNumericUpDown.");

        monetario.Focus();
        ProcesarMensajes();
        Assert.IsTrue(monetario.ContainsFocus, $"{form.GetType().Name}: no se pudo enfocar {campoMonetario}.");
        Assert.AreEqual(0, edicion.SelectionStart, $"{form.GetType().Name}: {campoMonetario} debe seleccionar desde el inicio.");
        Assert.AreEqual(
            edicion.Text.Length,
            edicion.SelectionLength,
            $"{form.GetType().Name}: {campoMonetario} debe seleccionar todo el texto mostrado.");

        // Clic izquierdo posterior con el foco ya adquirido: no debe forzar
        // la reselección completa (el helper sólo selecciona al entrar).
        edicion.SelectionStart = 2;
        edicion.SelectionLength = 0;
        InvocarEventoMouse(edicion, "OnMouseDown");
        InvocarEventoMouse(edicion, "OnMouseUp");
        Assert.AreEqual(
            0,
            edicion.SelectionLength,
            $"{form.GetType().Name}: un clic posterior en {campoMonetario} no debe reseleccionar todo el texto.");
    }

    private static void AfirmarTooltips(
        Form form,
        string nombreBarra,
        IReadOnlyDictionary<string, string> esperados,
        bool esperaRefrescarOculto)
    {
        Control[] encontrados = form.Controls.Find(nombreBarra, searchAllChildren: true);
        Assert.AreEqual(1, encontrados.Length, $"{form.GetType().Name}: no se encontró la barra '{nombreBarra}'.");
        ToolStrip barra = (ToolStrip)encontrados[0];

        Dictionary<string, string> visibles = barra.Items
            .OfType<ToolStripButton>()
            .Where(boton => boton.Available && !string.IsNullOrEmpty(boton.Name))
            .ToDictionary(boton => boton.Name!, boton => boton.ToolTipText ?? string.Empty);

        Assert.AreEqual(esperados.Count, visibles.Count, $"{form.GetType().Name}: cantidad de acciones visibles inesperada.");

        foreach (KeyValuePair<string, string> esperado in esperados)
        {
            Assert.IsTrue(visibles.ContainsKey(esperado.Key), $"{form.GetType().Name}: falta la acción visible '{esperado.Key}'.");
            Assert.AreEqual(esperado.Value, visibles[esperado.Key], $"{form.GetType().Name}: tooltip incorrecto en '{esperado.Key}'.");
            foreach (string modificador in new[] { "Ctrl+", "Alt+", "Shift+" })
            {
                Assert.IsFalse(
                    visibles[esperado.Key].Contains(modificador, StringComparison.OrdinalIgnoreCase),
                    $"{form.GetType().Name}: '{esperado.Key}' no debe anunciar un atajo {modificador} inexistente.");
            }
        }

        ToolStripButton? refrescar = barra.Items
            .OfType<ToolStripButton>()
            .FirstOrDefault(boton => boton.Name == "btnRefrescar");

        if (esperaRefrescarOculto)
        {
            Assert.IsNotNull(refrescar, $"{form.GetType().Name}: debe existir btnRefrescar.");
            Assert.IsFalse(refrescar!.Available, $"{form.GetType().Name}: btnRefrescar debe permanecer oculto (no disponible en la barra).");
            Assert.AreEqual(Refrescar, refrescar.ToolTipText, $"{form.GetType().Name}: tooltip incorrecto en btnRefrescar.");
        }
        else
        {
            Assert.IsNull(refrescar, $"{form.GetType().Name}: no debe existir btnRefrescar en esta barra.");
        }
    }

    private static TextBox ObtenerEdicion(NumericUpDown control)
    {
        TextBox? edicion = control.Controls.OfType<TextBox>().FirstOrDefault();
        return edicion ?? throw new InvalidOperationException($"El control '{control.Name}' no expone su TextBox interno.");
    }

    private static bool TieneHelperDeSeleccionConfigurado(NumericUpDown control)
    {
        FieldInfo? campoEstados = typeof(SeleccionTotalNumericUpDown).GetField(
            "_estados",
            BindingFlags.Static | BindingFlags.NonPublic);
        object? estados = campoEstados?.GetValue(null);
        if (estados == null)
            return false;

        MethodInfo? intentar = estados.GetType().GetMethod("TryGetValue");
        object?[] argumentos = { ObtenerEdicion(control), null };
        return intentar != null && (bool)intentar.Invoke(estados, argumentos)!;
    }

    private static void InvocarEventoMouse(TextBox edicion, string metodo)
    {
        MethodInfo? evento = typeof(TextBox).GetMethod(metodo, BindingFlags.Instance | BindingFlags.NonPublic);
        if (evento == null)
            throw new InvalidOperationException($"No existe '{metodo}' en TextBox.");

        evento.Invoke(edicion, new object?[] { new MouseEventArgs(MouseButtons.Left, 1, 2, 2, 0) });
    }

    private static void ProcesarMensajes()
    {
        for (int i = 0; i < 5; i++)
            System.Windows.Forms.Application.DoEvents();
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
        string root = Path.Combine(Path.GetTempPath(), $"sopro_atajos_catalogos_{Guid.NewGuid():N}");
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
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
