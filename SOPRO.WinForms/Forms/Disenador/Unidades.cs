// ============================================================
// SOPRO POC v9 — Unidades
// Hub de conversión y lógica de escala para adaptación de formato.
//
// Unidad interna: décimas de milímetro (dmm)
//   1 dmm = 0.1 mm
//
// Ancho base de diseño: carta vertical imprimible (195.9mm)
// El diseñador siempre muestra este ancho.
// El renderizador aplica scaleX para adaptar al formato real.
//
// Regla de escala confirmada:
//   scaleX = anchoRealDmm / ANCHO_BASE_DMM
//   xReal      = xBase * scaleX
//   yReal      = yBase              ← Y no escala
//   widthReal  = widthBase * scaleX
//   heightReal = heightBase * scaleX
//   fontReal   = Clamp(fontBase * scaleX, MIN_FONT, MAX_FONT)
//   AlturaFranja = sin cambio      ← altura de franja no escala
// ============================================================
namespace SOPRO.WinForms.Forms.Disenador;

// ── Formatos de hoja ─────────────────────────────────────────
public enum FormatoHoja
{
    CartaVertical,
    CartaHorizontal,
    OficioVertical,
    OficioHorizontal,
}

public static class Unidades
{
    // ── Ancho base canónico (carta vertical imprimible) ───────
    // 215.9mm total - 10mm margen izq - 10mm margen der = 195.9mm
    public static readonly int ANCHO_BASE_DMM = MmADmm(195.9);

    // ── Límites de fuente al escalar ─────────────────────────
    public const float FONT_MIN = 6f;
    public const float FONT_MAX = 72f;

    // ── Conversiones básicas ──────────────────────────────────

    /// <summary>Décimas de mm → milímetros</summary>
    public static double DmmAMm(int dmm) => dmm / 10.0;

    /// <summary>Milímetros → décimas de mm (redondeo)</summary>
    public static int MmADmm(double mm) => (int)Math.Round(mm * 10);

    /// <summary>Décimas de mm → puntos tipográficos (pt)</summary>
    public static double DmmAPt(int dmm) => dmm / 10.0 * (72.0 / 25.4);

    /// <summary>Décimas de mm → centímetros</summary>
    public static double DmmACm(int dmm) => dmm / 100.0;

    /// <summary>Décimas de mm → píxeles dado el factor de escala px/dmm</summary>
    public static int DmmAPx(int dmm, double escala) => (int)Math.Round(dmm * escala);

    /// <summary>Píxeles → décimas de mm dado el factor de escala px/dmm</summary>
    public static int PxADmm(int px, double escala) => escala > 0
        ? (int)Math.Round(px / escala)
        : 0;

    // ── Snap ─────────────────────────────────────────────────

    /// <summary>Snap al milímetro entero más cercano (intervaloMm=1 → cada 10dmm)</summary>
    public static int SnapA(int dmm, int intervaloMm = 1)
        => (int)Math.Round(dmm / (double)(intervaloMm * 10)) * intervaloMm * 10;

    // ── Ancho imprimible por formato ──────────────────────────
    // Márgenes izq+der = 20mm en todos los formatos
    public static int AnchoPrintDmm(FormatoHoja f) => f switch
    {
        FormatoHoja.CartaVertical    => MmADmm(195.9),   // carta 215.9 - 20
        FormatoHoja.CartaHorizontal  => MmADmm(259.4),   // carta 279.4 - 20
        FormatoHoja.OficioVertical   => MmADmm(195.9),   // oficio 215.9 - 20
        FormatoHoja.OficioHorizontal => MmADmm(335.6),   // oficio 355.6 - 20
        _                            => MmADmm(195.9),
    };

    public static string NombreFormato(FormatoHoja f) => f switch
    {
        FormatoHoja.CartaVertical    => "Carta vertical",
        FormatoHoja.CartaHorizontal  => "Carta horizontal",
        FormatoHoja.OficioVertical   => "Oficio vertical",
        FormatoHoja.OficioHorizontal => "Oficio horizontal",
        _                            => "Desconocido",
    };

    // ── Escala de adaptación ──────────────────────────────────

    /// <summary>
    /// Factor de escala horizontal para adaptar del ancho base al formato real.
    /// scaleX = anchoRealDmm / ANCHO_BASE_DMM
    /// </summary>
    public static double FactorEscalaX(FormatoHoja formato)
        => (double)AnchoPrintDmm(formato) / ANCHO_BASE_DMM;

    /// <summary>
    /// Aplica la regla de escala a un elemento para un formato dado.
    /// Devuelve los valores ya escalados (no modifica el original).
    /// </summary>
    public static (int x, int y, int ancho, int alto, float fuente) EscalarElemento(
        int xBase, int yBase, int anchoBase, int altoBase, float fuenteBase,
        FormatoHoja formato)
    {
        double sx = FactorEscalaX(formato);
        return (
            (int)Math.Round(xBase     * sx),
            yBase,                              // Y no escala
            (int)Math.Round(anchoBase * sx),
            (int)Math.Round(altoBase  * sx),
            Math.Clamp((float)(fuenteBase * sx), FONT_MIN, FONT_MAX)
        );
    }
}
