using System;
using System.IO;

namespace SOPRO.WinForms.Helpers;

/// <summary>Posición del workspace de matrices: Abajo (default) o Derecha.</summary>
internal enum PosicionWorkspace
{
    Abajo = 0,
    Derecha = 1,
}

/// <summary>
/// Estado local por proyecto del workspace: posición, alto preferido (modo Abajo)
/// y ancho preferido (modo Derecha). El archivo heredado de un solo entero se
/// interpreta como alto con posición Abajo; un valor inválido degrada a Abajo.
/// </summary>
internal sealed class PresupuestoWorkspaceEstado
{
    /// <summary>
    /// Dimensión mínima efectiva para aceptar un valor guardado; hereda el umbral
    /// de la lectura heredada (<c>&gt; 120</c>) para descartar basura.
    /// </summary>
    public const int DimensionMinimaValida = 121;

    public PosicionWorkspace Posicion { get; }

    public int Alto { get; }

    public int Ancho { get; }

    /// <summary>Estado sin preferencias: posición Abajo y dimensiones a resolver por política.</summary>
    public static PresupuestoWorkspaceEstado PorDefecto { get; } = new(PosicionWorkspace.Abajo, 0, 0);

    public PresupuestoWorkspaceEstado(PosicionWorkspace posicion, int alto, int ancho)
    {
        Posicion = posicion;
        Alto = Normalizar(alto);
        Ancho = Normalizar(ancho);
    }

    public static PresupuestoWorkspaceEstado Leer(string ruta)
    {
        if (!File.Exists(ruta))
            return PorDefecto;

        string[] lineas = File.ReadAllLines(ruta);
        if (lineas.Length == 1 && int.TryParse(lineas[0].Trim(), out int heredado))
            return new PresupuestoWorkspaceEstado(PosicionWorkspace.Abajo, heredado, 0);

        PosicionWorkspace posicion = PosicionWorkspace.Abajo;
        int alto = 0;
        int ancho = 0;

        foreach (string linea in lineas)
        {
            string[] par = linea.Split(':', 2);
            if (par.Length != 2)
                continue;

            string clave = par[0].Trim().ToLowerInvariant();
            string valor = par[1].Trim();
            switch (clave)
            {
                case "posicion" when IntentarLeerPosicion(valor, out PosicionWorkspace leida):
                    posicion = leida;
                    break;
                case "alto" when int.TryParse(valor, out int altoLeido):
                    alto = altoLeido;
                    break;
                case "ancho" when int.TryParse(valor, out int anchoLeido):
                    ancho = anchoLeido;
                    break;
            }
        }

        return new PresupuestoWorkspaceEstado(posicion, alto, ancho);
    }

    public void Guardar(string ruta)
    {
        string? dir = Path.GetDirectoryName(ruta);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllLines(ruta, new[]
        {
            $"Posicion: {Posicion}",
            $"Alto: {Alto}",
            $"Ancho: {Ancho}",
        });
    }

    /// <summary>
    /// Enum.TryParse acepta números de valor no definido (p. ej. "7"); solo se
    /// acepta una posición EXPLÍCITAMENTE definida del enum, el resto cae a Abajo.
    /// </summary>
    private static bool IntentarLeerPosicion(string valor, out PosicionWorkspace posicion)
    {
        if (Enum.TryParse(valor, ignoreCase: true, out PosicionWorkspace leida)
            && Enum.IsDefined(typeof(PosicionWorkspace), leida))
        {
            posicion = leida;
            return true;
        }

        posicion = PosicionWorkspace.Abajo;
        return false;
    }

    private static int Normalizar(int valor)
        => valor >= DimensionMinimaValida ? valor : 0;
}
