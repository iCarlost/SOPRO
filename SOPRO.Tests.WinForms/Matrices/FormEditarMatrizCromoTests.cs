using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Matrices;

/// <summary>
/// Invariantes estructurales del cromo de <see cref="FormEditarMatriz"/>
/// (issue #177): el encabezado flota con el host compartido de los módulos,
/// los botones son SOPRO, el formulario conserva su redimensionado y sus
/// atajos originales (este editor nunca tuvo Accept/Cancel), el título del
/// encabezado acompaña a la identidad del tipo, los tintes funcionales
/// APU/Básico/Cuadrilla se conservan y la geometría del grid no cambia.
/// Invariantes de bounds y tipos, no de píxeles ni colores exactos.
/// </summary>
[STATestClass]
[DoNotParallelize] // Los forms de WinForms requieren un hilo STA dedicado.
public class FormEditarMatrizCromoTests
{
    [TestMethod]
    public void Cromo_EncabezadoFlotanteCompartido_BotonesSopro_ConservaVentana()
    {
        Proyecto? previo = null;
        EjecutarConEditor(form =>
        {
            previo = LeerProyectoGlobalFormato();
            AfirmarEncabezadoFlotante(form);

            // Botones de acción y de alta reales de SOPRO.
            Assert.IsInstanceOfType(LeerCampo<Button>(form, "btnGuardar"), typeof(SoproButton));
            Assert.IsInstanceOfType(LeerCampo<Button>(form, "btnCancelar"), typeof(SoproButton));
            foreach (var nombre in new[]
                     {
                         "btnAgregarMaterial", "btnAgregarManoObra", "btnAgregarMaquinaria",
                         "btnAgregarBasico", "btnAgregarHerramienta"
                     })
            {
                Assert.IsInstanceOfType(LeerCampo<Button>(form, nombre), typeof(SoproButton),
                    $"{nombre} debe ser un botón SOPRO.");
            }

            // Este editor nunca tuvo Accept/Cancel ni diálogo fijo: piel, no esqueleto.
            Assert.IsNull(form.AcceptButton, "El editor no debe adquirir AcceptButton nuevo.");
            Assert.IsNull(form.CancelButton, "El editor no debe adquirir CancelButton nuevo.");
            Assert.AreEqual(FormBorderStyle.Sizable, form.FormBorderStyle,
                "El editor sigue siendo redimensionable.");

            AfirmarPieConBotones(form);
            AfirmarGridIntacto(form);
            AfirmarCamposEnContenidoDesplazable(form);
        });
        Assert.AreSame(previo, LeerProyectoGlobalFormato(),
            "La prueba no debe dejar alterado el proyecto global de FormatoHelper.");
    }

    [TestMethod]
    public void Cromo_TituloEncabezadoSigueALaIdentidadYTintesFuncionalesSeConservan()
    {
        EjecutarConEditor(form =>
        {
            var rotulo = RotuloEncabezado(form);

            Assert.AreEqual("Editor de Matriz (APU)", form.Text);
            Assert.AreEqual("Editor de Matriz (APU)", rotulo.Text);
            Assert.AreEqual(Color.FromArgb(245, 248, 252), LeerCampo<GroupBox>(form, "grpDatos").BackColor,
                "El tinte funcional de APU se conserva.");

            LeerCampo<RadioButton>(form, "rbBasico").Checked = true;
            Assert.AreEqual("Editor de Básicos", form.Text);
            Assert.AreEqual("Editor de Básicos", rotulo.Text,
                "El título del encabezado debe seguir al cambio de tipo.");
            Assert.AreEqual(Color.FromArgb(255, 248, 220), LeerCampo<GroupBox>(form, "grpDatos").BackColor,
                "El tinte funcional de Básico se conserva.");

            LeerCampo<RadioButton>(form, "rbCuadrilla").Checked = true;
            Assert.AreEqual("Editor de Cuadrillas", form.Text);
            Assert.AreEqual("Editor de Cuadrillas", rotulo.Text,
                "El título del encabezado debe seguir al cambio de tipo.");
            Assert.AreEqual(Color.FromArgb(230, 242, 255), LeerCampo<GroupBox>(form, "grpDatos").BackColor,
                "El tinte funcional de Cuadrilla se conserva.");

            LeerCampo<RadioButton>(form, "rbAPU").Checked = true;
            Assert.AreEqual("Editor de Matriz (APU)", rotulo.Text,
                "Volver a APU restaura el título del encabezado.");
        });
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static void AfirmarEncabezadoFlotante(Form form)
    {
        Control host = form.Controls.OfType<Control>().SingleOrDefault(c => c.Dock == DockStyle.Top && c.Visible)
            ?? throw new AssertFailedException("Falta la banda de encabezado acoplada arriba.");

        var flotante = host as ChromeFlotanteHost;
        Assert.IsNotNull(flotante, "El encabezado debe estar alojado en un ChromeFlotanteHost compartido.");
        Assert.IsInstanceOfType(flotante.Superficie, typeof(TableLayoutPanel),
            "La superficie del host flotante debe ser la banda de encabezado.");
        Assert.IsTrue(host.Height > flotante.Superficie.Height,
            "El host debe reservar aire exterior alrededor del encabezado.");
    }

    private static Label RotuloEncabezado(Form form)
    {
        var host = (ChromeFlotanteHost)form.Controls.OfType<Control>()
            .Single(c => c.Dock == DockStyle.Top);
        return (Label)((TableLayoutPanel)host.Superficie).Controls[0];
    }

    private static void AfirmarPieConBotones(Form form)
    {
        Control pie = form.Controls.OfType<Control>().SingleOrDefault(c => c.Dock == DockStyle.Bottom && c.Visible)
            ?? throw new AssertFailedException("Falta la banda de pie acoplada abajo.");

        foreach (var nombre in new[] { "btnGuardar", "btnCancelar" })
        {
            Button boton = LeerCampo<Button>(form, nombre);
            Assert.IsTrue(boton.Visible, $"{nombre} debe ser visible en el pie.");
            Assert.IsTrue(pie.ClientRectangle.Contains(boton.Bounds),
                $"{nombre} debe quedar completo dentro del pie (boton={boton.Bounds}, pie.Client={pie.ClientRectangle}, pie.Bounds={pie.Bounds}).");
        }
    }

    [TestMethod]
    public void Cromo_ScrollSoloCuandoElContenidoNoCabe_YRefluyoSinCorteHorizontal()
    {
        EjecutarConEditor(form =>
        {
            Panel contenido = PanelContenido(form);

            // Con el ancho/alto natural a 100 % (sin acotado por pantalla
            // pequeña): ni scroll vertical en el editor vacío ni horizontal.
            if (form.ClientSize.Height >= 820)
            {
                Assert.IsFalse(contenido.VerticalScroll.Visible,
                    "Con el contenido al descubierto no debe haber scroll vertical (regresión: scrollbar en editor vacío).");
            }
            AfirmarSinCorteHorizontalEvitable(contenido);
            AfirmarResumenSiempreAlcanzable(contenido);

            // Ancho reducido: el reflow apila el resumen; si el grupo de datos
            // (ancho fijo por diseño) no cabe, el scroll horizontal es el
            // recurso físico restante y el contenido sigue alcanzable.
            form.ClientSize = new Size(900, 700);
            Bombear();
            Assert.IsTrue(contenido.VerticalScroll.Visible,
                "Al reducir la altura, el contenido excedente debe quedar accesible con scroll vertical.");
            AfirmarSinCorteHorizontalEvitable(contenido);
            AfirmarResumenSiempreAlcanzable(contenido);
            GroupBox resumen = LeerCampo<GroupBox>(form, "grpResumen");
            GroupBox componentes = LeerCampo<GroupBox>(form, "grpComponentes");
            Assert.IsTrue(resumen.Top >= componentes.Bottom,
                "Con ancho insuficiente el resumen debe apilarse debajo de los componentes.");

            // Ancho restaurado: Windows puede conceder menos que el solicitado.
            // Verificar el reflow según el espacio real y los anchos actuales.
            form.ClientSize = new Size(2000, 830);
            Bombear();
            AfirmarSinCorteHorizontalEvitable(contenido);
            AfirmarResumenSiempreAlcanzable(contenido);
            int anchoNecesario = componentes.Left + componentes.Width + 12
                + resumen.Width + 12 + SystemInformation.VerticalScrollBarWidth;
            if (contenido.Width >= anchoNecesario)
            {
                Assert.AreEqual(componentes.Top, resumen.Top,
                    "Con ancho de sobra el resumen vuelve al costado de los componentes.");
            }
            else
            {
                Assert.IsTrue(resumen.Top >= componentes.Bottom,
                    "Si el área útil impide ensanchar la ventana, el resumen permanece apilado y accesible.");
            }

            AfirmarGridIntacto(form);
        });
    }

    /// <summary>
    /// El scroll horizontal solo se tolera cuando el panel es más angosto que
    /// el ancho FIJO del grupo de datos (sin reflow posible para ese grupo);
    /// nunca por la colocación del resumen, que el reflow resuelve.
    /// </summary>
    private static void AfirmarSinCorteHorizontalEvitable(Panel contenido)
    {
        Control datos = contenido.Controls["grpDatos"];
        bool fisicamenteForzado = contenido.ClientRectangle.Width < datos.Right;

        if (!fisicamenteForzado)
        {
            Assert.IsFalse(contenido.HorizontalScroll.Visible,
                $"No debe haber scroll horizontal cuando el ancho alcanza para el grupo de datos "
                + $"(cliente={contenido.ClientRectangle.Width}, datos={datos.Bounds}).");
            foreach (var nombre in new[] { "grpComponentes", "grpResumen" })
            {
                Control grupo = (Control)contenido.Controls[nombre];
                Assert.IsTrue(grupo.Right <= contenido.ClientRectangle.Right,
                    $"{nombre} debe quedar completo sin scroll horizontal (right={grupo.Right}, cliente={contenido.ClientRectangle.Right}).");
            }
        }

        // Ningún hijo recortado por su propio grupo (regresión: el reflow
        // pre-manejo encogía el resumen por debajo de sus rótulos).
        foreach (var nombre in new[] { "grpDatos", "grpComponentes", "grpResumen" })
        {
            GroupBox grupo = (GroupBox)contenido.Controls[nombre];
            foreach (Control hijo in grupo.Controls)
            {
                if (!hijo.Visible) continue;
                Assert.IsTrue(grupo.ClientRectangle.Contains(hijo.Bounds),
                    $"{nombre}.{hijo.Name} debe quedar completo dentro de su grupo "
                    + $"(hijo={hijo.Bounds}, grupo.Client={grupo.ClientRectangle}).");
            }
        }
    }

    /// <summary>El resumen nunca es el control que desborda horizontalmente.</summary>
    private static void AfirmarResumenSiempreAlcanzable(Panel contenido)
    {
        Control resumen = (Control)contenido.Controls["grpResumen"];
        Assert.IsTrue(resumen.Left >= 0 && resumen.Right <= contenido.ClientRectangle.Right,
            $"El resumen debe quedar dentro del ancho visible sin scroll horizontal (bounds={resumen.Bounds}, cliente={contenido.ClientRectangle}).");
    }

    private static void Bombear()
    {
        for (int i = 0; i < 5; i++)
            System.Windows.Forms.Application.DoEvents();
    }

    private static Panel PanelContenido(FormEditarMatriz form)
        => form.Controls.OfType<Panel>().Single(p => p.Dock == DockStyle.Fill && p.AutoScroll);

    private static void AfirmarGridIntacto(FormEditarMatriz form)
    {
        DataGridView grid = form.DgvComponentesParaPrueba;

        Assert.AreEqual(new Size(860, 454), grid.Size, "El viewport del grid no debe cambiar.");
        Assert.AreEqual(new Point(20, 76), grid.Location, "El grid conserva su posición dentro del grupo.");
        Assert.AreEqual(30, grid.RowTemplate.Height, "El alto de fila del grid se conserva.");
        Assert.AreEqual(8, grid.Columns.Count, "El grid conserva sus 8 columnas.");
        CollectionAssert.AreEqual(
            new[] { "colTipo", "colClave", "colDescripcion", "colUnidad", "colCantidad", "colPU", "colImporte", "colEliminar" },
            grid.Columns.Cast<DataGridViewColumn>().Select(c => c.Name).ToArray(),
            "El grid conserva sus columnas y su orden.");
    }

    private static void AfirmarCamposEnContenidoDesplazable(Form form)
    {
        Panel contenido = form.Controls.OfType<Panel>().Single(p => p.Dock == DockStyle.Fill && p.AutoScroll);
        Assert.IsTrue(contenido.Controls.ContainsKey("grpDatos"), "grpDatos vive en el panel desplazable.");
        Assert.IsTrue(contenido.Controls.ContainsKey("grpComponentes"), "grpComponentes vive en el panel desplazable.");
        Assert.IsTrue(contenido.Controls.ContainsKey("grpResumen"), "grpResumen vive en el panel desplazable.");

        foreach (var nombre in new[] { "txtClave", "txtDescripcion", "cboUnidad", "rbAPU", "rbBasico", "rbCuadrilla" })
        {
            Control campo = LeerCampo<Control>(form, nombre);
            Assert.IsTrue(campo.Visible, $"{nombre} debe ser visible.");
            Assert.IsTrue(contenido.DisplayRectangle.Contains(campo.Bounds),
                $"{nombre} debe quedar completo dentro del área desplazable (bounds={campo.Bounds}, display={contenido.DisplayRectangle}).");
        }
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

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)typeof(FormatoHelper)
            .GetField("_proyectoActual", BindingFlags.Static | BindingFlags.NonPublic)
            ?.GetValue(null);

    private static void EjecutarConEditor(Action<FormEditarMatriz> prueba)
    {
        string root = Path.Combine(Path.GetTempPath(), $"sopro_matriz_cromo_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string dbPath = Path.Combine(root, "catalogo-temporal.db");

        SOPROContext? contexto = null;
        try
        {
            contexto = new SOPROContext(dbPath);
            contexto.Database.EnsureCreated();
            Proyecto proyecto = AgregarProyecto(contexto);
            contexto.SaveChanges();

            using FormEditarMatriz form = new FormEditarMatriz(contexto, proyecto.Id)
            {
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-4000, -4000),
                ShowInTaskbar = false,
            };
            form.Show();
            for (int i = 0; i < 5; i++)
                System.Windows.Forms.Application.DoEvents();

            prueba(form);
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

    private static Proyecto AgregarProyecto(SOPROContext ctx)
    {
        Proyecto proyecto = new Proyecto
        {
            Nombre = "Proyecto cromo editor de matriz",
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
        return proyecto;
    }
}
