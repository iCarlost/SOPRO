using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// Escenario compartido de los tests de tema de catálogos (issue #207):
/// proyecto SQLite temporal, paleta activa por preset y restauración del
/// estado global del proceso (paleta y el proyecto actual de FormatoHelper).
/// No conoce el helper de tema del producto: construye los formularios reales
/// y expone sus controles por nombre.
/// </summary>
internal sealed class CatalogosTemaEscenario : IDisposable
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    private readonly string _root;
    private readonly SoproTemaPreset _presetPrevio;
    private readonly Proyecto? _proyectoGlobalPrevio;
    private readonly List<Form> _formularios = new();
    private readonly int _suscripcionesDecimalesPrevias;

    public CatalogosTemaEscenario(SoproTemaPreset preset, Action<SOPROContext, int>? preparar = null)
    {
        _root = Path.Combine(Path.GetTempPath(), $"sopro_cat_tema_{preset}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);

        _presetPrevio = SoproTheme.CurrentPalette.Preset;
        _proyectoGlobalPrevio = CatalogosTemaEntorno.LeerProyectoGlobalFormato();
        SoproTheme.InitializePalette(preset);

        DbPath = Path.Combine(_root, "proyecto-temporal.soproproj");
        Contexto = new SOPROContext(DbPath);
        Contexto.Database.EnsureDeleted();
        Contexto.Database.EnsureCreated();

        Proyecto = CatalogosTemaEntorno.CrearProyecto(Contexto);
        preparar?.Invoke(Contexto, Proyecto.Id);
        Contexto.ChangeTracker.Clear();

        _suscripcionesDecimalesPrevias =
            CatalogosTemaEntorno.ContarSuscripcionesDecimalesActualizados();
    }

    public SOPROContext Contexto { get; }

    public Proyecto Proyecto { get; }

    public string DbPath { get; }

    public Form Crear(CatalogoBajoTema catalogo)
    {
        Form form = catalogo.Crear(Contexto, Proyecto.Id);
        // El escenario es dueño de TODO formulario creado: los cierra y libera
        // al final, con el DbContext aún vivo. Así se ejecuta el FormClosed que
        // desuscribe FormDatosProyecto.DecimalesActualizados.
        _formularios.Add(form);

        // Sin Show(): el Load de los catálogos dispara cargas y no hace falta
        // para validar el chrome, que se fija en el constructor.
        form.CreateControl();
        form.PerformLayout();
        return form;
    }

    public void Dispose()
    {
        try
        {
            int formulariosCerrados = _formularios.Count;
            CerrarFormularios();

            int suscripcionesRestantes =
                CatalogosTemaEntorno.ContarSuscripcionesDecimalesActualizados();
            Assert.AreEqual(
                _suscripcionesDecimalesPrevias,
                suscripcionesRestantes,
                "Fuga de suscripciones a FormDatosProyecto.DecimalesActualizados: " +
                $"la línea base era {_suscripcionesDecimalesPrevias} y tras cerrar " +
                $"{formulariosCerrados} formulario(s) quedaron {suscripcionesRestantes}. " +
                "Cerrar los formularios con el DbContext vivo debe disparar FormClosed y desuscribir.");
        }
        finally
        {
            Contexto.Dispose();
            SoproTheme.InitializePalette(_presetPrevio);
            CatalogosTemaEntorno.RestaurarProyectoGlobalFormato(_proyectoGlobalPrevio);
            CatalogosTemaEntorno.EliminarDirectorio(_root);
        }
    }

    private void CerrarFormularios()
    {
        foreach (Form form in _formularios)
        {
            if (form.IsDisposed) continue;

            // Evidencia del gate: Close() sobre un formulario nunca mostrado
            // (sólo CreateControl) NO dispara FormClosed, así que la
            // desuscripción de FormDatosProyecto.DecimalesActualizados no corre.
            // Se ejecuta el ciclo real Show→Close fuera de pantalla: el Load
            // corre contra la BD temporal vacía y la cancelación de cada
            // catálogo corre en Dispose. El formulario sigue siendo propiedad
            // del escenario; no se libera antes de cerrarlo.
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-32000, -32000);
            form.WindowState = FormWindowState.Normal;
            form.Opacity = 0;
            form.Show();
            form.Close();

            if (!form.IsDisposed)
                form.Dispose();
        }

        _formularios.Clear();
    }

    internal static FieldInfo CampoProyectoGlobalFormatoInfo
        => typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato, BindingFlags.Static | BindingFlags.NonPublic)
           ?? throw new InvalidOperationException(
               $"No se encontró el campo FormatoHelper.{CampoProyectoGlobalFormato}.");
}

/// <summary>
/// Catálogo real bajo prueba. Los nombres de controles son los que exponen
/// los diseñadores actuales (no hay API de tema que consultar).
/// </summary>
internal sealed record CatalogoBajoTema(
    string Nombre,
    Type TipoFormulario,
    Func<SOPROContext, int, Form> Crear,
    string Grid,
    string PanelHeader,
    string PanelToolbar,
    string PanelFiltros,
    string LblTitulo,
    string LblBuscar,
    string TxtBuscar,
    string StatusStrip,
    string LblStatus,
    string[] Hosts)
{
    public static readonly CatalogoBajoTema[] Todos =
    {
        new(
            "Materiales",
            typeof(FormCatalogoMateriales),
            (ctx, proyectoId) => new FormCatalogoMateriales(ctx, proyectoId),
            Grid: "dgvMateriales",
            PanelHeader: "panelTop",
            PanelToolbar: "panelToolbar",
            PanelFiltros: "panelFiltros",
            LblTitulo: "lblTitulo",
            LblBuscar: "lblBuscar",
            TxtBuscar: "txtBuscar",
            StatusStrip: "statusStrip",
            LblStatus: "lblStatus",
            Hosts: new[] { "panelTopHost", "panelToolbarHost", "panelFiltrosHost" }),
        new(
            "Mano de Obra",
            typeof(FormCatalogoManoObra),
            (ctx, proyectoId) => new FormCatalogoManoObra(ctx, proyectoId),
            Grid: "dgvManoObra",
            PanelHeader: "panelTop",
            PanelToolbar: "panelToolbar",
            PanelFiltros: "panelFiltros",
            LblTitulo: "lblTitulo",
            LblBuscar: "lblBuscar",
            TxtBuscar: "txtBuscar",
            StatusStrip: "statusStrip",
            LblStatus: "lblStatus",
            Hosts: new[] { "panelTopHost", "panelToolbarHost", "panelFiltrosHost" }),
        new(
            "Herramientas",
            typeof(FormCatalogoHerramientas),
            (ctx, proyectoId) => new FormCatalogoHerramientas(ctx, proyectoId),
            Grid: "dgvHerramientas",
            PanelHeader: "panelHeader",
            PanelToolbar: "panelBotones",
            PanelFiltros: "panelBusqueda",
            LblTitulo: "lblTitulo",
            LblBuscar: "lblBuscar",
            TxtBuscar: "txtBuscar",
            StatusStrip: "statusStrip",
            LblStatus: "lblStatus",
            Hosts: new[] { "panelHeaderHost", "panelBotonesHost", "panelBusquedaHost" }),
        new(
            "Maquinaria",
            typeof(FormCatalogoMaquinaria),
            (ctx, proyectoId) => new FormCatalogoMaquinaria(ctx, proyectoId),
            Grid: "dgvMaquinaria",
            PanelHeader: "panelTop",
            PanelToolbar: "panelToolbar",
            PanelFiltros: "panelFiltros",
            LblTitulo: "lblTitulo",
            LblBuscar: "lblBuscar",
            TxtBuscar: "txtBuscar",
            StatusStrip: "statusStrip",
            LblStatus: "lblStatus",
            Hosts: new[] { "panelTopHost", "panelToolbarHost", "panelFiltrosHost" }),
    };
}

internal static class CatalogosTemaEntorno
{
    public static Proyecto CrearProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto tema catálogos",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            PorcentajeIndirectosCentral = 0m,
            PorcentajeIndirectosCampo = 0m,
            PorcentajeFinanciamiento = 0m,
            PorcentajeUtilidad = 0m,
            PorcentajeCargosAdicionales = 0m,
            PorcentajeIVA = 0m,
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

    public static Control Buscar(Form form, string nombre)
        => form.Controls.Find(nombre, searchAllChildren: true).FirstOrDefault()
           ?? throw new AssertFailedException($"No se encontró el control '{nombre}'.");

    /// <summary>
    /// Invoca la reconfiguración real del grid (privada en cada formulario):
    /// limpia y recrea columnas, por lo que el chrome debe reaplicarse.
    /// </summary>
    public static void InvocarConfigurarGrid(Form form, Type tipoFormulario)
    {
        MethodInfo? metodo = tipoFormulario.GetMethod(
            "ConfigurarGrid", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(metodo, $"{tipoFormulario.Name} debe exponer ConfigurarGrid.");
        metodo!.Invoke(form, null);
    }

    public static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)CatalogosTemaEscenario.CampoProyectoGlobalFormatoInfo.GetValue(null);

    public static void RestaurarProyectoGlobalFormato(Proyecto? proyecto)
        => CatalogosTemaEscenario.CampoProyectoGlobalFormatoInfo.SetValue(null, proyecto);

    /// <summary>
    /// Cuenta los suscriptores vivos del evento estático
    /// <c>FormDatosProyecto.DecimalesActualizados</c>. El backing field de un
    /// evento field-like conserva la invocation list real, así que permite
    /// detectar fugas al teardown (los formularios deben desuscribirse en
    /// FormClosed).
    /// </summary>
    public static int ContarSuscripcionesDecimalesActualizados()
    {
        FieldInfo campo = typeof(FormDatosProyecto).GetField(
            nameof(FormDatosProyecto.DecimalesActualizados),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "No se encontró el backing field de FormDatosProyecto.DecimalesActualizados.");

        return campo.GetValue(null) is Delegate manejadores
            ? manejadores.GetInvocationList().Length
            : 0;
    }

    public static double Contraste(Color frente, Color fondo)
    {
        double la = Luminancia(frente);
        double lb = Luminancia(fondo);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    public static void AssertContraste(Color frente, Color fondo, double minimo, string contexto)
    {
        double contraste = Contraste(frente, fondo);
        Assert.IsTrue(contraste >= minimo, $"{contexto}: {contraste:F2}:1 (mínimo {minimo:F1}:1)");
    }

    private static double Luminancia(Color color)
    {
        static double Canal(int valor)
        {
            double s = valor / 255d;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Canal(color.R) + 0.7152 * Canal(color.G) + 0.0722 * Canal(color.B);
    }

    public static void EliminarDirectorio(string root)
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
