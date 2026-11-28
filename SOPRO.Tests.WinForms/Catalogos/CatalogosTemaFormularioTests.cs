using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// Issue #207 — tema de los cuatro catálogos de insumos (Materiales, Mano de
/// Obra, Herramientas y Maquinaria) sobre FORMULARIOS REALES, en los tres
/// presets. Verifica el chrome propio (header, toolbar, filtros/búsqueda,
/// status y rejilla) y que la reconfiguración real de columnas
/// (<c>ConfigurarGrid()</c>) reaplique el chrome de la rejilla.
///
/// No se inspeccionan colores de COLUMNA/CELDA como roles del tema: esos
/// colores son datos del usuario y tienen su propia prueba de preservación.
/// El color del título lo administra <c>EditableReportTitleHelper</c> con el
/// valor configurado por el usuario, así que se valida contraste, no el token.
/// </summary>
[STATestClass]
[DoNotParallelize] // Cambia la paleta global del proceso y construye formularios reales.
public class CatalogosTemaFormularioTests
{
    [TestInitialize]
    public void VerificarApartmentState()
    {
        Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState(),
            "Los tests de tema de catálogos construyen controles WinForms y deben correr en STA.");
    }

    [TestMethod]
    [DataRow("claro-azul")]
    [DataRow("grafito")]
    [DataRow("claro-verde")]
    public void Formularios_ConCadaPreset_TematizanHeaderToolbarFiltrosStatusYGrid(string presetId)
    {
        Assert.IsTrue(SoproTheme.TryParsePreset(presetId, out SoproTemaPreset preset));
        using var escenario = new CatalogosTemaEscenario(preset);
        var paleta = SoproTheme.GetPalette(preset);

        foreach (CatalogoBajoTema catalogo in CatalogoBajoTema.Todos)
        {
            Form form = escenario.Crear(catalogo);
            AssertarChromeCompleto(form, catalogo, paleta, preset.ToString());
        }
    }

    [TestMethod]
    [DataRow("claro-azul")]
    [DataRow("grafito")]
    [DataRow("claro-verde")]
    public void ConfigurarGrid_Reaplicado_ConservaElChromeTematizado(string presetId)
    {
        Assert.IsTrue(SoproTheme.TryParsePreset(presetId, out SoproTemaPreset preset));
        using var escenario = new CatalogosTemaEscenario(preset);
        var paleta = SoproTheme.GetPalette(preset);

        foreach (CatalogoBajoTema catalogo in CatalogoBajoTema.Todos)
        {
            Form form = escenario.Crear(catalogo);

            // Ruta real de reconstrucción de columnas: debe reaplicar el chrome
            // de rejilla (el helper genérico de celdas corre antes y lo pisa).
            CatalogosTemaEntorno.InvocarConfigurarGrid(form, catalogo.TipoFormulario);

            var grid = (DataGridView)CatalogosTemaEntorno.Buscar(form, catalogo.Grid);
            Assert.IsTrue(grid.Columns.Count > 0, $"{preset}: {catalogo.Nombre} debe reconstruir sus columnas.");

            AssertarChromeCompleto(form, catalogo, paleta, $"{preset} tras ConfigurarGrid()");
        }
    }

    [TestMethod]
    [DataRow("claro-azul")]
    [DataRow("grafito")]
    [DataRow("claro-verde")]
    public void ColumnaRelleno_ContinuaElZebraDeLaHoja_TrasConstruirYReconfigurar(string presetId)
    {
        Assert.IsTrue(SoproTheme.TryParsePreset(presetId, out SoproTemaPreset preset));
        using var escenario = new CatalogosTemaEscenario(preset);

        foreach (CatalogoBajoTema catalogo in CatalogoBajoTema.Todos)
        {
            Form form = escenario.Crear(catalogo);
            AssertarColumnaRelleno(form, catalogo, $"{preset}: {catalogo.Nombre} tras construir");

            // El chrome de rejilla se reaplica al final de ConfigurarGrid(); la
            // columna de relleno debe seguir el mismo fondo que la hoja de datos.
            CatalogosTemaEntorno.InvocarConfigurarGrid(form, catalogo.TipoFormulario);
            AssertarColumnaRelleno(
                form, catalogo, $"{preset}: {catalogo.Nombre} tras ConfigurarGrid()");
        }
    }

    [TestMethod]
    [DataRow("claro-azul")]
    [DataRow("grafito")]
    [DataRow("claro-verde")]
    public void HostsFlotantes_ConCadaPreset_CapturanElFondoTematizado(string presetId)
    {
        Assert.IsTrue(SoproTheme.TryParsePreset(presetId, out SoproTemaPreset preset));
        using var escenario = new CatalogosTemaEscenario(preset);
        var paleta = SoproTheme.GetPalette(preset);

        foreach (CatalogoBajoTema catalogo in CatalogoBajoTema.Todos)
        {
            Form form = escenario.Crear(catalogo);

            // ChromeFlotanteHost toma el BackColor del formulario al hospedar:
            // el tema debe aplicarse antes de esa llamada en el constructor.
            Assert.AreEqual(paleta.CenterBg, form.BackColor,
                $"{preset}: {catalogo.Nombre} debe usar el fondo central del tema.");

            foreach (string nombreHost in catalogo.Hosts)
            {
                Control host = CatalogosTemaEntorno.Buscar(form, nombreHost);
                Assert.AreEqual(paleta.CenterBg, host.BackColor,
                    $"{preset}: {catalogo.Nombre}.{nombreHost} debe capturar el fondo tematizado.");
            }
        }
    }

    // ═════════════════════════════ Aserciones compartidas ═════════════════════════════

    private static void AssertarChromeCompleto(
        Form form, CatalogoBajoTema catalogo, SoproTemaPaleta paleta, string contexto)
    {
        string etiqueta = $"{contexto}: {catalogo.Nombre}";

        AssertarHeader(form, catalogo, paleta, etiqueta);
        AssertarToolbar(form, catalogo, paleta, etiqueta);
        AssertarFiltros(form, catalogo, paleta, etiqueta);
        AssertarStatus(form, catalogo, paleta, etiqueta);
        AssertarGrid(form, catalogo, paleta, etiqueta);
    }

    private static void AssertarHeader(
        Form form, CatalogoBajoTema catalogo, SoproTemaPaleta paleta, string etiqueta)
    {
        Control header = CatalogosTemaEntorno.Buscar(form, catalogo.PanelHeader);
        Assert.AreEqual(paleta.HeaderDark, header.BackColor, $"{etiqueta}: banda de encabezado.");

        var titulo = (Label)CatalogosTemaEntorno.Buscar(form, catalogo.LblTitulo);
        // El color del título es DATO del usuario (EditableReportTitleHelper):
        // se exige legibilidad sobre el header oscuro, no un token exacto.
        CatalogosTemaEntorno.AssertContraste(
            titulo.ForeColor, header.BackColor, 4.5,
            $"{etiqueta}: título configurado por el usuario sobre el header");
    }

    private static void AssertarToolbar(
        Form form, CatalogoBajoTema catalogo, SoproTemaPaleta paleta, string etiqueta)
    {
        var toolbar = (ToolStrip)CatalogosTemaEntorno.Buscar(form, catalogo.PanelToolbar);
        Assert.AreEqual(paleta.SecondaryBg, toolbar.BackColor, $"{etiqueta}: banda de comandos.");

        foreach (ToolStripItem item in toolbar.Items)
        {
            if (!item.Available) continue;

            Assert.AreEqual(paleta.SecondaryBg, item.BackColor,
                $"{etiqueta}: fondo del comando '{item.Name}'.");
            Assert.AreEqual(paleta.Ink2, item.ForeColor,
                $"{etiqueta}: texto del comando '{item.Name}'.");
        }
    }

    private static void AssertarFiltros(
        Form form, CatalogoBajoTema catalogo, SoproTemaPaleta paleta, string etiqueta)
    {
        Control filtros = CatalogosTemaEntorno.Buscar(form, catalogo.PanelFiltros);
        Assert.AreEqual(paleta.Surface, filtros.BackColor, $"{etiqueta}: banda de filtros/búsqueda.");

        var rotulo = (Label)CatalogosTemaEntorno.Buscar(form, catalogo.LblBuscar);
        Assert.AreEqual(paleta.Ink2, rotulo.ForeColor, $"{etiqueta}: rótulo de búsqueda.");

        var caja = (TextBox)CatalogosTemaEntorno.Buscar(form, catalogo.TxtBuscar);
        Assert.AreEqual(paleta.Surface, caja.BackColor, $"{etiqueta}: fondo del campo de búsqueda.");
        Assert.AreEqual(paleta.Ink, caja.ForeColor, $"{etiqueta}: texto del campo de búsqueda.");

        foreach (string nombreCheck in new[] { "chkSoloProyecto", "chkSoloMaestros" })
        {
            var check = (CheckBox)CatalogosTemaEntorno.Buscar(form, nombreCheck);
            Assert.IsFalse(check.UseVisualStyleBackColor,
                $"{etiqueta}: '{nombreCheck}' debe usar los colores del tema.");
            Assert.AreEqual(paleta.Surface, check.BackColor,
                $"{etiqueta}: fondo de '{nombreCheck}'.");
            Assert.AreEqual(paleta.Ink2, check.ForeColor,
                $"{etiqueta}: texto de '{nombreCheck}'.");
        }
    }

    private static void AssertarStatus(
        Form form, CatalogoBajoTema catalogo, SoproTemaPaleta paleta, string etiqueta)
    {
        var status = (StatusStrip)CatalogosTemaEntorno.Buscar(form, catalogo.StatusStrip);
        Assert.AreEqual(paleta.HeaderBg, status.BackColor, $"{etiqueta}: fondo del status.");

        ToolStripStatusLabel rotulo = status.Items
            .OfType<ToolStripStatusLabel>()
            .First(i => string.Equals(i.Name, catalogo.LblStatus, StringComparison.Ordinal));
        Assert.AreEqual(paleta.Muted, rotulo.ForeColor, $"{etiqueta}: texto del status.");
    }

    private static void AssertarGrid(
        Form form, CatalogoBajoTema catalogo, SoproTemaPaleta paleta, string etiqueta)
    {
        var grid = (DataGridView)CatalogosTemaEntorno.Buscar(form, catalogo.Grid);

        Assert.AreEqual(paleta.Surface, grid.BackgroundColor, $"{etiqueta}: fondo de la rejilla.");
        Assert.AreEqual(paleta.GridLine, grid.GridColor, $"{etiqueta}: líneas de la rejilla.");

        // El zebra de filas alternas NO es un rol del tema en esta migración:
        // se conserva el gris histórico (#F5F5F5) para no volver ilegible el
        // texto negro que el usuario persiste por columna en Grafito.
        Assert.AreEqual(
            Color.FromArgb(245, 245, 245).ToArgb(),
            grid.AlternatingRowsDefaultCellStyle.BackColor.ToArgb(),
            $"{etiqueta}: el zebra de filas alternas debe conservar el gris histórico.");

        Assert.IsFalse(grid.EnableHeadersVisualStyles,
            $"{etiqueta}: los encabezados deben usar los colores del tema.");

        DataGridViewCellStyle encabezado = grid.ColumnHeadersDefaultCellStyle;
        Assert.AreEqual(paleta.HeaderBg, encabezado.BackColor,
            $"{etiqueta}: fondo del encabezado de columnas.");
        Assert.AreEqual(paleta.Ink, encabezado.ForeColor,
            $"{etiqueta}: texto del encabezado de columnas.");

        DataGridViewCellStyle encabezadoFila = grid.RowHeadersDefaultCellStyle;
        Assert.AreEqual(paleta.HeaderBg, encabezadoFila.BackColor,
            $"{etiqueta}: fondo del encabezado de fila.");
        Assert.AreEqual(paleta.Ink2, encabezadoFila.ForeColor,
            $"{etiqueta}: texto del encabezado de fila.");
        Assert.AreEqual(paleta.SelBg, encabezadoFila.SelectionBackColor,
            $"{etiqueta}: selección del encabezado de fila.");
        Assert.AreEqual(paleta.OnSelection, encabezadoFila.SelectionForeColor,
            $"{etiqueta}: texto seleccionado del encabezado de fila.");
    }

    /// <summary>
    /// Regresión del tablero de ajedrez en la zona de relleno: <c>colDummy</c>
    /// es la columna Fill sin datos ni encabezado que cierra el ancho. Sin su
    /// propio fondo hereda el <c>DefaultCellStyle</c> del tema (oscuro en
    /// Grafito) en las filas pares mientras el zebra legacy pinta las impares,
    /// dejando una franja ajedrezada a la derecha de la hoja. Debe continuar el
    /// mismo ritmo que las columnas de datos: blanco en filas pares y #F5F5F5
    /// en impares. No se valida geometría ni formato persistido del usuario.
    /// </summary>
    private static void AssertarColumnaRelleno(
        Form form, CatalogoBajoTema catalogo, string contexto)
    {
        var grid = (DataGridView)CatalogosTemaEntorno.Buscar(form, catalogo.Grid);

        Assert.IsTrue(grid.Columns.Contains("colDummy"),
            $"{contexto}: debe existir la columna de relleno 'colDummy'.");

        DataGridViewColumn relleno = grid.Columns["colDummy"]
            ?? throw new AssertFailedException($"{contexto}: no se encontró 'colDummy'.");

        DataGridViewColumn datos = grid.Columns
            .Cast<DataGridViewColumn>()
            .FirstOrDefault(c => !ReferenceEquals(c, relleno)
                                 && !string.IsNullOrEmpty(c.DataPropertyName))
            ?? throw new AssertFailedException(
                $"{contexto}: debe existir al menos una columna de datos.");

        while (grid.Rows.Count < 4)
            grid.Rows.Add();

        for (int fila = 0; fila < 4; fila++)
        {
            int esperado = (fila % 2 == 0 ? Color.White : Color.FromArgb(245, 245, 245)).ToArgb();

            Assert.AreEqual(
                esperado,
                grid.Rows[fila].Cells[relleno.Index].InheritedStyle.BackColor.ToArgb(),
                $"{contexto}: fila {fila} de 'colDummy' debe continuar el zebra de la hoja " +
                "(blanco en pares, #F5F5F5 en impares) y no exponer el fondo del tema.");

            // La corrección del relleno no debe alterar el fondo/zebra de las
            // columnas de datos (su formato persistido lo cubren las pruebas de
            // preservación de formato).
            Assert.AreEqual(
                esperado,
                grid.Rows[fila].Cells[datos.Index].InheritedStyle.BackColor.ToArgb(),
                $"{contexto}: fila {fila} de la columna de datos '{datos.Name}' no debe cambiar.");
        }
    }
}
