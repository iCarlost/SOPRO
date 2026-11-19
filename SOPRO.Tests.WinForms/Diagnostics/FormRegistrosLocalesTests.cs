using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.Tests.WinForms.Diagnostics;

/// <summary>
/// Composición del gestor de registros locales: cromo SOPRO, botones
/// neutralmente secundarios en celdas disjuntas del pie, splitter legible y
/// estado inicial sin captura. No se abre la ventana de forma modal ni se
/// ejecutan operaciones reales (la carga ocurre en el evento Shown, que nunca
/// se dispara aquí). Las verificaciones de layout usan el layout REAL del
/// formulario tras PerformLayout.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class FormRegistrosLocalesTests
{
    [TestMethod]
    public void Ventana_UsaElCromoSoproYMantieneElTamanoMinimo()
    {
        using var ventana = new SOPRO.WinForms.Forms.FormRegistrosLocales();

        Assert.AreEqual("Registros locales", ventana.Text);
        Assert.AreEqual(AutoScaleMode.Dpi, ventana.AutoScaleMode);
        Assert.AreEqual(new Size(760, 480), ventana.MinimumSize);
        Assert.AreEqual(SoproTheme.Colors.CenterBg, ventana.BackColor);
        Assert.AreEqual(FormBorderStyle.Sizable, ventana.FormBorderStyle, "El gestor sigue siendo redimensionable.");
        Assert.IsTrue(ventana.MaximizeBox && ventana.MinimizeBox, "La ventana conserva los botones nativos minimizar/maximizar.");
    }

    [TestMethod]
    public void Ventana_TodosLosBotonesSonSoproButtonNeutralSecundario()
    {
        using var ventana = new SOPRO.WinForms.Forms.FormRegistrosLocales();

        var actualizar = ObtenerBoton(ventana, "Actualizar");
        var exportar = ObtenerBoton(ventana, "Exportar archivo…");
        var borrar = ObtenerBoton(ventana, "Borrar todos…");

        foreach (var boton in new[] { actualizar, exportar, borrar })
        {
            Assert.IsInstanceOfType(boton, typeof(SoproButton));
            Assert.AreEqual(SoproTheme.Colors.Surface, boton.BackColor, "Ningún botón del gestor es primario; Borrar todos es neutral.");
        }

        Assert.AreEqual(SoproIconType.Recalcular, ((SoproButton)actualizar).SoproIcon);
        Assert.AreEqual(SoproIconType.Copiar, ((SoproButton)exportar).SoproIcon);
        Assert.AreEqual(SoproIconType.Papelera, ((SoproButton)borrar).SoproIcon);
        Assert.AreEqual(SoproButtonLayout.Small, ((SoproButton)actualizar).SoproLayout);
        Assert.AreEqual(SoproButtonLayout.Small, ((SoproButton)exportar).SoproLayout);
        Assert.AreEqual(SoproButtonLayout.Small, ((SoproButton)borrar).SoproLayout);
    }

    [TestMethod]
    public void Ventana_UsaElIconoSOPROEnLaBarraDeTitulo()
    {
        using var ventana = new SOPRO.WinForms.Forms.FormRegistrosLocales();

        Assert.IsNotNull(ventana.Icon, "La barra de titulo nativa debe mostrar el icono SOPRO incrustado.");
    }

    [TestMethod]
    public void Ventana_SinCapturaExportarYBorrarEstanDeshabilitadosYActualizarHabilitado()
    {
        using var ventana = new SOPRO.WinForms.Forms.FormRegistrosLocales();

        var actualizar = ObtenerBoton(ventana, "Actualizar");
        var exportar = ObtenerBoton(ventana, "Exportar archivo…");
        var borrar = ObtenerBoton(ventana, "Borrar todos…");

        Assert.IsTrue(actualizar.Enabled, "Actualizar siempre está disponible.");
        Assert.IsFalse(exportar.Enabled, "Exportar requiere un archivo seleccionado.");
        Assert.IsFalse(borrar.Enabled, "Borrar requiere registros disponibles.");
    }

    [TestMethod]
    public void Ventana_ConservaLosNombresAccesibles()
    {
        using var ventana = new SOPRO.WinForms.Forms.FormRegistrosLocales();

        var lista = enumerarControles(ventana).OfType<ListBox>().Single();
        var vista = enumerarControles(ventana).OfType<TextBox>().Single();
        var estado = enumerarControles(ventana).OfType<Label>().Single(l => l.AccessibleRole == AccessibleRole.StatusBar);

        Assert.AreEqual("Archivos de registro", lista.AccessibleName);
        Assert.AreEqual("Vista previa completa del archivo", vista.AccessibleName);
        Assert.IsTrue(!string.IsNullOrEmpty(ventana.AccessibleDescription));
        Assert.AreEqual("Resumen de registros",
            enumerarControles(ventana).OfType<Label>().Single(l => l.AccessibleName == "Resumen de registros").AccessibleName);
    }

    [TestMethod]
    public void Lista_UsaDibujoPropioConElEstiloDelTema()
    {
        using var ventana = new SOPRO.WinForms.Forms.FormRegistrosLocales();

        var lista = enumerarControles(ventana).OfType<ListBox>().Single();

        Assert.AreEqual(DrawMode.OwnerDrawFixed, lista.DrawMode);
        Assert.AreEqual(BorderStyle.None, lista.BorderStyle);
        Assert.AreEqual(SoproTheme.Colors.Surface, lista.BackColor);
        Assert.IsTrue(lista.ItemHeight >= SoproDialogTheme.AltoMinimoItemLista);
    }

    [TestMethod]
    public void Ventana_Panel1DeLaListaMantieneElAnchoPrevisto()
    {
        using var ventana = new SOPRO.WinForms.Forms.FormRegistrosLocales();
        ventana.PerformLayout();

        var split = enumerarControles(ventana).OfType<SplitContainer>().Single();

        Assert.IsTrue(split.SplitterDistance >= 250,
            $"SplitterDistance={split.SplitterDistance}: el tamaño por defecto del SplitContainer (150px) recortaba la distancia y Panel1 quedaba en ~121px.");
        Assert.IsTrue(split.Panel1.Width >= 250 - 2,
            $"El panel de la lista debe mantener el ancho previsto (Panel1={split.Panel1.Width}).");
    }

    [TestMethod]
    public void Ventana_LosBotonesDelPieSonVisiblesYNoLosCubreElEstado()
    {
        using var ventana = new SOPRO.WinForms.Forms.FormRegistrosLocales();
        ventana.PerformLayout();

        var pie = enumerarControles(ventana).OfType<Panel>().Single(p => p.Dock == DockStyle.Bottom);
        var tabla = enumerarControles(pie).OfType<TableLayoutPanel>().Single();
        var flujo = enumerarControles(tabla).OfType<FlowLayoutPanel>().Single();
        var estado = enumerarControles(ventana).OfType<Label>().Single(l => l.AccessibleRole == AccessibleRole.StatusBar);
        var botones = new[]
        {
            ObtenerBoton(ventana, "Actualizar"),
            ObtenerBoton(ventana, "Exportar archivo…"),
            ObtenerBoton(ventana, "Borrar todos…")
        };

        // Celdas disjuntas: la etiqueta de estado (columna 100%) nunca puede
        // invadir la columna de botones (defecto de la captura anterior: la
        // etiqueta Dock=Fill tapaba los tres botones).
        Assert.IsTrue(estado.Bounds.Right <= flujo.Bounds.Left + 1,
            $"La etiqueta de estado termina en {estado.Bounds.Right} y los botones empiezan en {flujo.Bounds.Left}.");

        foreach (var boton in botones)
        {
            Assert.IsTrue(flujo.ClientRectangle.Contains(boton.Bounds),
                $"{boton.Text} debe quedar dentro del flujo de botones (bounds={boton.Bounds}, flujo={flujo.ClientRectangle}).");
            Assert.IsTrue(boton.Height <= pie.Height && flujo.Bounds.IntersectsWith(pie.ClientRectangle),
                $"{boton.Text} debe caber dentro del pie.");
        }

        for (int i = 0; i < botones.Length; i++)
        {
            for (int j = i + 1; j < botones.Length; j++)
            {
                Assert.IsFalse(botones[i].Bounds.IntersectsWith(botones[j].Bounds),
                    $"{botones[i].Text} y {botones[j].Text} no pueden solaparse.");
            }
        }

        // El grupo llega hasta el margen derecho del pie y está en orden
        // visual izquierdo→derecho Actualizar / Exportar / Borrar.
        var masDerecha = botones.OrderByDescending(b => b.Bounds.Right).First();
        Assert.AreEqual(flujo.ClientSize.Width - flujo.Padding.Right, masDerecha.Bounds.Right,
            "El botón más a la derecha debe quedar pegado al margen derecho del pie.");
        Assert.AreEqual("Borrar todos…", masDerecha.Text);
    }

    [TestMethod]
    public void Ventana_EncabezadoMuestraTituloYSubtituloCompletosSinSolapes()
    {
        using var ventana = new SOPRO.WinForms.Forms.FormRegistrosLocales();
        ventana.PerformLayout();

        var encabezado = enumerarControles(ventana).OfType<TableLayoutPanel>()
            .Single(p => p.Dock == DockStyle.Top && p.BackColor == SoproTheme.Colors.HeaderDark);
        var titulo = encabezado.Controls.OfType<Label>().Single(l => l.Text == "Registros locales");
        var subtitulo = encabezado.Controls.OfType<Label>().Single(l => l.Text == "Revisión y exportación manual de registros de diagnóstico locales.");

        AssertarRotuloMideSuTextoCompleto(titulo);
        AssertarRotuloMideSuTextoCompleto(subtitulo);
        Assert.IsTrue(subtitulo.Top >= titulo.Bottom, "El subtítulo no puede solaparse con el título.");
        Assert.IsTrue(subtitulo.Bottom <= encabezado.Height, "El subtítulo no puede recortarse por abajo.");
        Assert.IsTrue(encabezado.Height >= SoproDialogTheme.AltoEncabezado);
    }

    [TestMethod]
    public void FormatearEntrada_ConservaLaFilaConNombreFechaYTamano()
    {
        var entrada = new DiagnosticsLogEntry(
            Id: "id-1",
            FileName: "sopro-2026-01-01.log",
            LastWriteTimeUtc: new DateTime(2026, 1, 1, 12, 30, 0, DateTimeKind.Utc),
            Length: 1234,
            PreviewText: string.Empty);

        string fila = SOPRO.WinForms.Forms.FormRegistrosLocales.FormatearEntrada(entrada);

        StringAssert.Contains(fila, "sopro-2026-01-01.log");
        StringAssert.Contains(fila, "·");
        StringAssert.Contains(fila, entrada.Length.ToString("N0") + " bytes");
    }

    [TestMethod]
    public void SafeMessage_MapeaCadaEstadoAEspanol()
    {
        Assert.AreEqual("Los registros están ocupados. Inténtalo de nuevo.",
            SOPRO.WinForms.Forms.FormRegistrosLocales.SafeMessage(DiagnosticsOperationStatus.Busy));
        Assert.AreEqual("La operación se canceló.",
            SOPRO.WinForms.Forms.FormRegistrosLocales.SafeMessage(DiagnosticsOperationStatus.Cancelled));
        Assert.AreEqual("Los registros superan el tamaño que se puede revisar.",
            SOPRO.WinForms.Forms.FormRegistrosLocales.SafeMessage(DiagnosticsOperationStatus.TooLarge));
        Assert.AreEqual("El archivo de destino ya existe. Elige otro nombre.",
            SOPRO.WinForms.Forms.FormRegistrosLocales.SafeMessage(DiagnosticsOperationStatus.AlreadyExists));
        Assert.AreEqual("Algunos registros se borraron, pero no se pudieron eliminar todos.",
            SOPRO.WinForms.Forms.FormRegistrosLocales.SafeMessage(DiagnosticsOperationStatus.Partial));
        Assert.AreEqual("No se pudo completar la operación. Inténtalo de nuevo.",
            SOPRO.WinForms.Forms.FormRegistrosLocales.SafeMessage((DiagnosticsOperationStatus)999));
    }

    private static void AssertarRotuloMideSuTextoCompleto(Label rotulo)
    {
        // Garantía estructural contra el colapso a un caracter: el rotulo es
        // AutoSize y se renderiza a su tamaño preferido (el texto completo).
        Assert.IsTrue(rotulo.AutoSize, "Un rotulo con ancho fijo puede colapsar el texto a un caracter.");
        Assert.AreEqual(rotulo.PreferredSize.Width, rotulo.Width,
            $"El rotulo debe renderizarse a su tamaño preferido (texto completo) (ancho={rotulo.Width}, preferido={rotulo.PreferredSize.Width}).");
        Assert.IsTrue(rotulo.Height >= rotulo.Font.Height,
            "El alto del rotulo debe cubrir la altura de la fuente.");
    }

    private static Button ObtenerBoton(Control raiz, string texto)
    {
        return enumerarControles(raiz).OfType<Button>().Single(boton => boton.Text == texto);
    }

    private static System.Collections.Generic.IEnumerable<Control> enumerarControles(Control raiz)
    {
        foreach (Control hijo in raiz.Controls)
        {
            yield return hijo;

            foreach (var anidado in enumerarControles(hijo))
            {
                yield return anidado;
            }
        }
    }
}
