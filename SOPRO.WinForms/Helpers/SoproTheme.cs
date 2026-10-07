using System.Drawing;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Tokens de tema centralizados para acercar la interfaz WinForms al look
    /// "glass-vibrant" de la web SOPRO. No depende de librerias de terceros ni
    /// de fuentes empaquetadas: solo usa la familia Segoe UI del sistema.
    ///
    /// Este archivo es infraestructura pura: agrupa la paleta y la tipografia
    /// para evitar hexadecimales dispersos por los formularios. Los metodos de
    /// pintura que consumen estos tokens se agregan por formulario.
    /// </summary>
    public static class SoproTheme
    {
        /// <summary>Familia tipografica unica del tema (no se empaquetan fuentes).</summary>
        private const string FontFamily = "Segoe UI";

        /// <summary>Angulo del gradiente del encabezado, en grados (equivalente web: 35deg).</summary>
        public const float HeaderGradientAngle = 35f;

        /// <summary>
        /// Paleta de colores del tema SOPRO. Los valores replican los tokens de
        /// la web (brand, ink, superficies y rejilla).
        /// </summary>
        public static class Colors
        {
            // --- Marca (brand) ---
            public static readonly Color Brand = Color.FromArgb(0x1E, 0x40, 0xAF);       // #1e40af
            public static readonly Color BrandMd = Color.FromArgb(0x1D, 0x4E, 0xD8);     // #1d4ed8
            public static readonly Color BrandLt = Color.FromArgb(0x3B, 0x82, 0xF6);     // #3b82f6

            // --- Acento (accent) ---
            public static readonly Color Accent = Color.FromArgb(0x08, 0x91, 0xB2);      // #0891b2
            public static readonly Color AccentLt = Color.FromArgb(0x22, 0xD3, 0xEE);    // #22d3ee

            // --- Texto (ink) ---
            public static readonly Color Ink = Color.FromArgb(0x0A, 0x0F, 0x1E);         // #0a0f1e
            public static readonly Color Ink2 = Color.FromArgb(0x1E, 0x29, 0x3B);        // #1e293b
            public static readonly Color Muted = Color.FromArgb(0x4B, 0x5A, 0x72);       // #4b5a72

            // --- Fondos (backgrounds) ---
            public static readonly Color BgBase = Color.FromArgb(0xDB, 0xEA, 0xFE);      // #dbeafe
            public static readonly Color BgTint2 = Color.FromArgb(0xE0, 0xE7, 0xFF);     // #e0e7ff

            // --- Superficies (surfaces) ---
            public static readonly Color Surface = Color.FromArgb(0xFF, 0xFF, 0xFF);     // #ffffff
            public static readonly Color SurfaceBorder = Color.FromArgb(0xC7, 0xD7, 0xF0); // #c7d7f0

            // --- Rejilla (grid) ---
            public static readonly Color RowAlt = Color.FromArgb(0xF4, 0xF7, 0xFD);      // #f4f7fd
            public static readonly Color GridLine = Color.FromArgb(0xE3, 0xE9, 0xF4);    // #e3e9f4
            public static readonly Color SelBg = Color.FromArgb(0xDB, 0xEA, 0xFE);       // #dbeafe

            // --- Derivados usados por el encabezado y las tarjetas de FormPrincipal ---
            public static readonly Color SubtitleText = Color.FromArgb(0xBA, 0xE6, 0xFD); // #bae6fd
            public static readonly Color HeaderBg = Color.FromArgb(0xF1, 0xF5, 0xFB);     // #f1f5fb
            public static readonly Color CenterTop = Color.FromArgb(0xEA, 0xF1, 0xFD);    // #eaf1fd
            public static readonly Color CenterBottom = Color.FromArgb(0xED, 0xF0, 0xFB); // #edf0fb
            public static readonly Color SecondaryBg = Color.FromArgb(0xF0, 0xF8, 0xFF);  // #f0f8ff
            public static readonly Color SecondaryHoverBg = Color.FromArgb(0xF0, 0xF6, 0xFF); // #f0f6ff
            public static readonly Color GlyphIdle = Color.FromArgb(0x8A, 0x94, 0xA6);    // gris icono papelera
            public static readonly Color HoverBg = Color.FromArgb(0xEA, 0xF2, 0xFF);      // hover suave en rejilla
            public static readonly Color Glow = Color.FromArgb(0x4D, 0x3B, 0x82, 0xF6);   // glow azul (alpha 30%)
        }

        /// <summary>
        /// Tipografia del tema. Todo Segoe UI: Bold para titulos/botones/encabezados.
        /// </summary>
        public static class Fonts
        {
            public static readonly Font Title = new Font(FontFamily, 26F, FontStyle.Bold, GraphicsUnit.Point);
            public static readonly Font Subtitle = new Font(FontFamily, 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            public static readonly Font GridCell = new Font(FontFamily, 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            public static readonly Font GridHeader = new Font(FontFamily, 9F, FontStyle.Bold, GraphicsUnit.Point);
            public static readonly Font Button = new Font(FontFamily, 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            public static readonly Font GroupTitle = new Font(FontFamily, 10F, FontStyle.Bold, GraphicsUnit.Point);
        }
    }
}
