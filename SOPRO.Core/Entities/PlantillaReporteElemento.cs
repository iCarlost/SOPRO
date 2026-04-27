using System;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Elemento libre del diseñador WYSIWYG de encabezado/pie de página PDF.
    /// Coordenadas en décimas de milímetro (dmm). Independiente de PlantillaReporte.
    /// </summary>
    public class PlantillaReporteElemento
    {
        public int    Id                { get; set; }
        public int    PlantillaReporteId { get; set; }

        // Zona: "Encabezado" | "PieDePagina"
        public string Zona              { get; set; } = "Encabezado";

        // Tipo: "TextoLibre" | "EtiquetaDinamica" | "Imagen"
        public string Tipo              { get; set; } = "TextoLibre";

        // Posición y tamaño en décimas de mm (dmm)
        public int    X                 { get; set; }
        public int    Y                 { get; set; }
        public int    Ancho             { get; set; }
        public int    Alto              { get; set; }

        // Contenido
        public string Contenido         { get; set; } = "";

        // Tipografía
        public string Fuente            { get; set; } = "Segoe UI";
        public double TamanoFuente      { get; set; } = 10.0;
        public bool   Negrita           { get; set; }
        public bool   Cursiva           { get; set; }
        public string ColorTextoHex     { get; set; } = "#000000";
        public string Alineacion        { get; set; } = "MiddleLeft";

        // Capa
        public int    ZOrder            { get; set; }

        // Imagen — bytes como fuente de verdad — nullable porque no todo elemento tiene imagen
        public byte[]? ImagenBytes       { get; set; }
        public string? ImagenNombreOrigen { get; set; }
        public string? ImagenRutaOrigen  { get; set; }
        public string? ImagenMimeType    { get; set; }

        // Navegación
        public virtual PlantillaReporte PlantillaReporte { get; set; }
    }
}
