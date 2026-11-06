using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Estado persistido del workspace: herencia del formato heredado de un entero,
/// posición inválida degradando a Abajo y round-trip de ambas dimensiones.
/// </summary>
[TestClass]
public class PresupuestoWorkspaceEstadoTests
{
    private string? _ruta;

    /// <summary>Cada prueba ocupa su archivo temporal y lo elimina al final.</summary>
    private string Ruta()
        => _ruta ??= Path.Combine(Path.GetTempPath(), $"sopro_ws_estado_{Guid.NewGuid():N}.txt");

    [TestCleanup]
    public void Limpiar()
    {
        if (_ruta == null) return;
        try
        {
            if (File.Exists(_ruta)) File.Delete(_ruta);
        }
        catch { }
    }

    [TestMethod]
    public void SinArchivo_SeLeeComoDefecto_AbajoSinDimensiones()
    {
        PresupuestoWorkspaceEstado estado = PresupuestoWorkspaceEstado.Leer(Ruta());

        Assert.AreEqual(PosicionWorkspace.Abajo, estado.Posicion);
        Assert.AreEqual(0, estado.Alto);
        Assert.AreEqual(0, estado.Ancho);
        Assert.IsFalse(File.Exists(Ruta()), "Leer no debe crear el archivo.");
    }

    [TestMethod]
    public void FormatoHeredado_UnSoloEntero_SeInterpretaComoAltoEnAbajo()
    {
        File.WriteAllText(Ruta(), "402");

        PresupuestoWorkspaceEstado estado = PresupuestoWorkspaceEstado.Leer(Ruta());

        Assert.AreEqual(PosicionWorkspace.Abajo, estado.Posicion,
            "El formato heredado no registraba posición: el default debe mantenerse Abajo.");
        Assert.AreEqual(402, estado.Alto);
        Assert.AreEqual(0, estado.Ancho);
    }

    [TestMethod]
    public void PosicionInvalida_IndefinidaOCadenaDesconocida_CaeAbajo()
    {
        File.WriteAllText(Ruta(), "Posicion: 7\nAlto: 310\nAncho: 500");
        PresupuestoWorkspaceEstado numerica = PresupuestoWorkspaceEstado.Leer(Ruta());
        Assert.AreEqual(PosicionWorkspace.Abajo, numerica.Posicion,
            "Enum.TryParse aceptaría 7 como valor no definido; debe degradar a Abajo sin perder dimensiones.");

        File.Delete(Ruta());
        File.WriteAllText(Ruta(), "Posicion: Izquierda\nAlto: 310");
        PresupuestoWorkspaceEstado texto = PresupuestoWorkspaceEstado.Leer(Ruta());
        Assert.AreEqual(PosicionWorkspace.Abajo, texto.Posicion);
        Assert.AreEqual(310, texto.Alto);
    }

    [TestMethod]
    public void EstadoCompleto_RoundTrip_ConservaPosicionYAmbasDimensiones()
    {
        string ruta = Ruta();
        new PresupuestoWorkspaceEstado(PosicionWorkspace.Derecha, 402, 638).Guardar(ruta);

        Assert.IsTrue(File.Exists(ruta), "Guardar debe crear el archivo y su carpeta.");

        PresupuestoWorkspaceEstado estado = PresupuestoWorkspaceEstado.Leer(ruta);
        Assert.AreEqual(PosicionWorkspace.Derecha, estado.Posicion);
        Assert.AreEqual(402, estado.Alto);
        Assert.AreEqual(638, estado.Ancho);
    }

    [TestMethod]
    public void LineasInvalidas_SeIgnoran_YLaPosicionCaseInsensitiveSeAcepta()
    {
        File.WriteAllText(Ruta(), "Posicion: derecha\nBasura\nAlto: 310\nAncho: 0");

        PresupuestoWorkspaceEstado estado = PresupuestoWorkspaceEstado.Leer(Ruta());

        Assert.AreEqual(PosicionWorkspace.Derecha, estado.Posicion);
        Assert.AreEqual(310, estado.Alto);
        Assert.AreEqual(0, estado.Ancho,
            "Un ancho bajo el umbral se normaliza a cero (sin preferencia), el resto de líneas se respeta.");
        Assert.AreNotSame(estado, PresupuestoWorkspaceEstado.PorDefecto);
    }

    [TestMethod]
    public void DimensionDebajoDelUmbral_NuncaSeAcepta()
    {
        PresupuestoWorkspaceEstado bajo = new(PosicionWorkspace.Abajo, 13, 120);
        Assert.AreEqual(0, bajo.Alto);
        Assert.AreEqual(0, bajo.Ancho);

        PresupuestoWorkspaceEstado enUmbral = new(
            PosicionWorkspace.Derecha,
            PresupuestoWorkspaceEstado.DimensionMinimaValida,
            PresupuestoWorkspaceEstado.DimensionMinimaValida);
        Assert.AreEqual(PresupuestoWorkspaceEstado.DimensionMinimaValida, enUmbral.Alto);
        Assert.AreEqual(PresupuestoWorkspaceEstado.DimensionMinimaValida, enUmbral.Ancho);
    }
}
