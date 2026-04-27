// ============================================================
// SOPRO POC v9 — Modelos
// Decisiones cerradas:
//   - Imágenes: bytes en BD como fuente de verdad,
//     ruta/nombre solo como metadato informativo
//   - Coordenadas en dmm
//   - Y no escala al adaptar formato
// ============================================================
using System.Text.Json.Serialization;

namespace SOPRO.WinForms.Forms.Disenador;

public enum TipoElemento { TextoLibre, EtiquetaDinamica, Imagen }
public enum ZonaCanvas   { Encabezado, PieDePagina }

// ── Elemento del canvas ───────────────────────────────────────
public class ElementoCanvas
{
    // ── Identidad ─────────────────────────────────────────────
    public Guid         Id           { get; set; } = Guid.NewGuid();
    public TipoElemento Tipo         { get; set; }

    // ── Posición y tamaño en dmm ──────────────────────────────
    public int X     { get; set; }
    public int Y     { get; set; }
    public int Ancho { get; set; }
    public int Alto  { get; set; }

    // ── Contenido ─────────────────────────────────────────────
    public string Contenido     { get; set; } = string.Empty;

    // ── Tipografía ────────────────────────────────────────────
    public string Fuente        { get; set; } = "Segoe UI";
    public float  TamanoFuente  { get; set; } = 10f;          // pt
    public bool   Negrita       { get; set; }
    public bool   Cursiva       { get; set; }
    public string ColorTextoHex { get; set; } = "#000000";
    public string Alineacion    { get; set; } = "MiddleLeft";

    // ── Capas ─────────────────────────────────────────────────
    public int ZOrder { get; set; }

    // ── Imagen — bytes como fuente de verdad ──────────────────
    // Los bytes se guardan en BD. La ruta es solo metadato.
    public byte[]? ImagenBytes        { get; set; }  // FUENTE DE VERDAD
    public string? ImagenNombreOrigen { get; set; }  // nombre original del archivo
    public string? ImagenRutaOrigen   { get; set; }  // ruta de donde se importó (informativo)
    public string? ImagenMimeType     { get; set; }  // ej: "image/png"

    // ── Helpers no persistibles ───────────────────────────────
    [JsonIgnore]
    public Color ColorTexto
    {
        get { try { return ColorTranslator.FromHtml(ColorTextoHex); } catch { return Color.Black; } }
        set => ColorTextoHex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
    }

    [JsonIgnore]
    public ContentAlignment AlineacionEnum
    {
        get => Alineacion switch
        {
            "TopLeft"      => ContentAlignment.TopLeft,
            "TopCenter"    => ContentAlignment.TopCenter,
            "TopRight"     => ContentAlignment.TopRight,
            "MiddleCenter" => ContentAlignment.MiddleCenter,
            "MiddleRight"  => ContentAlignment.MiddleRight,
            "BottomLeft"   => ContentAlignment.BottomLeft,
            "BottomCenter" => ContentAlignment.BottomCenter,
            "BottomRight"  => ContentAlignment.BottomRight,
            _              => ContentAlignment.MiddleLeft,
        };
        set => Alineacion = value.ToString();
    }

    // ── Clonar ────────────────────────────────────────────────
    public ElementoCanvas Clonar() => new()
    {
        Id                = Guid.NewGuid(),
        Tipo              = Tipo,
        X = X, Y = Y, Ancho = Ancho, Alto = Alto,
        Contenido         = Contenido,
        Fuente            = Fuente,
        TamanoFuente      = TamanoFuente,
        Negrita           = Negrita,
        Cursiva           = Cursiva,
        ColorTextoHex     = ColorTextoHex,
        Alineacion        = Alineacion,
        ZOrder            = ZOrder,
        ImagenBytes       = ImagenBytes?.ToArray(),
        ImagenNombreOrigen = ImagenNombreOrigen,
        ImagenRutaOrigen  = ImagenRutaOrigen,
        ImagenMimeType    = ImagenMimeType,
    };
}

// ── Documento de diseño ───────────────────────────────────────
// Un solo diseño por proyecto — se adapta a cualquier formato de hoja.
public class DocumentoDiseno
{
    // Alturas de franja en dmm — no escalan con el formato
    public int AlturaEncabezadoDmm { get; set; } = Unidades.MmADmm(40);
    public int AlturaPieDmm        { get; set; } = Unidades.MmADmm(20);

    // Identificador del proyecto (en SOPRO será ProyectoId; aquí es nombre)
    public string ProyectoId { get; set; } = "demo";

    public List<ElementoCanvas> Encabezado  { get; set; } = new();
    public List<ElementoCanvas> PieDePagina { get; set; } = new();

    // Ancho canónico de diseño — siempre el mismo
    [JsonIgnore]
    public int AnchoDmm => Unidades.ANCHO_BASE_DMM;
}
