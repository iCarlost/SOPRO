using System.Drawing;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Presets de tema seleccionables por el usuario (issue #203). El tema es
    /// FIJO durante toda la vida del proceso: se resuelve una vez en el arranque
    /// y no se cambia en caliente.
    /// </summary>
    internal enum SoproTemaPreset
    {
        /// <summary>Azul SOPRO histórico. Es la paleta por omisión y la referencia exacta de regresión.</summary>
        ClaroAzul,

        /// <summary>Grafito neutro suavizado con acentos de azul profundo (issue #209).</summary>
        Grafito,

        /// <summary>Claro con acentos verde/teal.</summary>
        ClaroVerde,
    }

    /// <summary>
    /// Paleta inmutable de un preset: TODOS los tokens del tema resueltos para
    /// ese preset, incluidos los roles semánticos de primer plano
    /// (<see cref="OnBrand"/>, <see cref="OnHeader"/>, <see cref="OnSelection"/>),
    /// que existen porque <see cref="Surface"/> no puede servir a la vez de
    /// fondo y de texto sobre acentos.
    ///
    /// Objeto de valor: se construye una sola vez por preset y nunca se muta.
    /// Las paletas viven en <see cref="SoproTheme"/>; ningún diálogo debe
    /// modificarlas.
    /// </summary>
    internal sealed record SoproTemaPaleta
    {
        // --- Identidad estable ---

        public required SoproTemaPreset Preset { get; init; }

        /// <summary>ID estable para persistencia (nunca se traduce ni se renombra).</summary>
        public required string Id { get; init; }

        /// <summary>Nombre visible en español para selectores y diálogos.</summary>
        public required string Nombre { get; init; }

        // --- Marca ---

        public required Color Brand { get; init; }
        public required Color BrandMd { get; init; }
        public required Color BrandLt { get; init; }

        /// <summary>Relleno de hover de la acción primaria (con <see cref="OnBrand"/> encima).</summary>
        public required Color BrandHover { get; init; }

        // --- Texto ---

        public required Color Ink { get; init; }
        public required Color Ink2 { get; init; }
        public required Color Muted { get; init; }

        // --- Fondos ---

        public required Color BgBase { get; init; }
        public required Color BgTint2 { get; init; }

        // --- Superficies ---

        public required Color Surface { get; init; }
        public required Color SurfaceBorder { get; init; }

        // --- Rejilla ---

        public required Color RowAlt { get; init; }
        public required Color GridLine { get; init; }
        public required Color SelBg { get; init; }

        /// <summary>Primer plano legible sobre <see cref="SelBg"/>.</summary>
        public required Color OnSelection { get; init; }

        // --- Chrome histórico (header oscuro, sidebar, ribbon) ---

        public required Color SidebarBg { get; init; }
        public required Color SidebarHeaderBg { get; init; }
        public required Color RibbonSeparator { get; init; }
        public required Color Disabled { get; init; }

        public required Color HeaderDark { get; init; }
        public required Color HeaderBottom { get; init; }
        public required Color RibbonText { get; init; }
        public required Color RibbonHover { get; init; }
        public required Color RibbonActive { get; init; }

        /// <summary>Hover del comando ACTIVO: relleno con <see cref="OnBrand"/> encima.</summary>
        public required Color RibbonActiveHover { get; init; }

        public required Color RibbonDown { get; init; }

        /// <summary>Presionado del comando ACTIVO (paso más profundo que <see cref="RibbonActive"/>).</summary>
        public required Color RibbonActivePressed { get; init; }

        public required Color RibbonDisabledIcon { get; init; }
        public required Color RibbonSeparatorDark { get; init; }
        public required Color SidebarBg2 { get; init; }
        public required Color SidebarHeaderBg2 { get; init; }
        public required Color SidebarHairline { get; init; }
        public required Color SidebarText { get; init; }
        public required Color SidebarTextGroup { get; init; }
        public required Color SidebarActive { get; init; }

        // --- Derivados del encabezado y las tarjetas ---

        public required Color SubtitleText { get; init; }
        public required Color HeaderBg { get; init; }
        public required Color CenterBg { get; init; }
        public required Color SecondaryBg { get; init; }
        public required Color SecondaryHoverBg { get; init; }
        public required Color GlyphIdle { get; init; }
        public required Color HoverBg { get; init; }
        public required Color Glow { get; init; }

        // --- Roles semánticos de primer plano ---

        /// <summary>Primer plano sobre rellenos de marca (<see cref="RibbonActive"/>, <see cref="RibbonActiveHover"/>, <see cref="BrandHover"/>, <see cref="RibbonDown"/>).</summary>
        public required Color OnBrand { get; init; }

        /// <summary>Primer plano sobre la banda de encabezado de diálogo (<see cref="HeaderDark"/>).</summary>
        public required Color OnHeader { get; init; }

        /// <summary>
        /// Color semántico de error (texto y glifos): SIEMPRE legible sobre las
        /// superficies de diálogo de su propia paleta (≥4.5:1 sobre
        /// <see cref="Surface"/> y <see cref="CenterBg"/>).
        /// </summary>
        public required Color Error { get; init; }

        /// <summary>
        /// Tinte de presionado del ribbon claro: fondo de los estados presionados
        /// de la banda de comandos, siempre legible con <see cref="Ink2"/>.
        /// </summary>
        public required Color RibbonPressed { get; init; }

        // --- Vista previa (selectores de tema) ---

        public Color PreviewFondo => Surface;
        public Color PreviewAcento => RibbonActive;
        public Color PreviewTinta => Ink;
        public Color PreviewBorde => SurfaceBorder;

        // =====================================================================
        // Paletas
        // =====================================================================

        /// <summary>
        /// Azul histórico: replica EXACTAMENTE los tokens vigentes (incluidos los
        /// valores que antes estaban codificados en el ribbon claro).
        /// </summary>
        internal static readonly SoproTemaPaleta ClaroAzul = new()
        {
            Preset = SoproTemaPreset.ClaroAzul,
            Id = "claro-azul",
            Nombre = "Azul claro",

            Brand = Color.FromArgb(0x1E, 0x40, 0xAF),
            BrandMd = Color.FromArgb(0x1D, 0x4E, 0xD8),
            BrandLt = Color.FromArgb(0x3B, 0x82, 0xF6),
            BrandHover = Color.FromArgb(0x1E, 0x40, 0xAF),

            Ink = Color.FromArgb(0x0A, 0x0F, 0x1E),
            Ink2 = Color.FromArgb(0x1E, 0x29, 0x3B),
            Muted = Color.FromArgb(0x4B, 0x5A, 0x72),

            BgBase = Color.FromArgb(0xDB, 0xEA, 0xFE),
            BgTint2 = Color.FromArgb(0xE0, 0xE7, 0xFF),

            Surface = Color.FromArgb(0xFF, 0xFF, 0xFF),
            SurfaceBorder = Color.FromArgb(0xC7, 0xD7, 0xF0),

            RowAlt = Color.FromArgb(0xF8, 0xFA, 0xFC),
            GridLine = Color.FromArgb(0xE2, 0xE8, 0xF0),
            SelBg = Color.FromArgb(0xDB, 0xEA, 0xFE),
            OnSelection = Color.FromArgb(0x0A, 0x0F, 0x1E),

            SidebarBg = Color.FromArgb(0xFA, 0xFA, 0xFA),
            SidebarHeaderBg = Color.FromArgb(0xF0, 0xF0, 0xF3),
            RibbonSeparator = Color.FromArgb(0xC8, 0xC8, 0xC8),
            Disabled = Color.FromArgb(0x78, 0x78, 0x78),

            HeaderDark = Color.FromArgb(0x1E, 0x29, 0x3B),
            HeaderBottom = Color.FromArgb(0x0F, 0x17, 0x2A),
            RibbonText = Color.FromArgb(0xE2, 0xE8, 0xF0),
            RibbonHover = Color.FromArgb(0x33, 0x41, 0x55),
            RibbonActive = Color.FromArgb(0x1D, 0x4E, 0xD8),
            RibbonActiveHover = Color.FromArgb(0x1E, 0x40, 0xAF),
            RibbonDown = Color.FromArgb(0x0F, 0x17, 0x2A),
            RibbonActivePressed = Color.FromArgb(0x1E, 0x3A, 0x8A),
            RibbonDisabledIcon = Color.FromArgb(0x94, 0xA3, 0xB8),
            RibbonSeparatorDark = Color.FromArgb(0x47, 0x55, 0x69),
            SidebarBg2 = Color.FromArgb(0xF1, 0xF5, 0xF9),
            SidebarHeaderBg2 = Color.FromArgb(0xE2, 0xE8, 0xF0),
            SidebarHairline = Color.FromArgb(0xCB, 0xD5, 0xE1),
            SidebarText = Color.FromArgb(0x33, 0x41, 0x55),
            SidebarTextGroup = Color.FromArgb(0x0F, 0x17, 0x2A),
            SidebarActive = Color.FromArgb(0xDB, 0xEA, 0xFE),

            SubtitleText = Color.FromArgb(0xCB, 0xD5, 0xE1),
            HeaderBg = Color.FromArgb(0xF1, 0xF5, 0xF9),
            CenterBg = Color.FromArgb(0xF8, 0xFA, 0xFC),
            SecondaryBg = Color.FromArgb(0xF1, 0xF5, 0xF9),
            SecondaryHoverBg = Color.FromArgb(0xE2, 0xE8, 0xF0),
            GlyphIdle = Color.FromArgb(0x8A, 0x94, 0xA6),
            HoverBg = Color.FromArgb(0xEA, 0xF2, 0xFF),
            Glow = Color.FromArgb(0x18, 0x0F, 0x17, 0x2A),

            OnBrand = Color.FromArgb(0xFF, 0xFF, 0xFF),
            OnHeader = Color.FromArgb(0xFF, 0xFF, 0xFF),
            Error = Color.FromArgb(0xB4, 0x23, 0x18),
            RibbonPressed = Color.FromArgb(0xDB, 0xEA, 0xFE),
        };

        /// <summary>
        /// Grafito (issue #209): grafito neutro suavizado. Superficies en grises
        /// neutros (Surface #30363F) que acercan el chrome al blanco de las hojas
        /// de datos sin superar el techo de contraste de los colores semánticos
        /// fijos de Indirectos; acentos de marca, SelBg claro (negro impuesto por
        /// DgvCeldaHelper) y primer plano claro se conservan.
        /// </summary>
        internal static readonly SoproTemaPaleta Grafito = new()
        {
            Preset = SoproTemaPreset.Grafito,
            Id = "grafito",
            Nombre = "Grafito",

            Brand = Color.FromArgb(0x7F, 0xA8, 0xEA),
            BrandMd = Color.FromArgb(0x8F, 0xB3, 0xEA),
            BrandLt = Color.FromArgb(0xA9, 0xC8, 0xF5),
            BrandHover = Color.FromArgb(0x35, 0x68, 0xCF),

            Ink = Color.FromArgb(0xE6, 0xE9, 0xED),
            Ink2 = Color.FromArgb(0xC6, 0xCC, 0xD4),
            Muted = Color.FromArgb(0xA3, 0xAE, 0xBB),

            BgBase = Color.FromArgb(0x22, 0x26, 0x2C),
            BgTint2 = Color.FromArgb(0x26, 0x2B, 0x32),

            Surface = Color.FromArgb(0x30, 0x36, 0x3F),
            SurfaceBorder = Color.FromArgb(0x45, 0x4C, 0x56),

            RowAlt = Color.FromArgb(0x36, 0x3C, 0x45),
            GridLine = Color.FromArgb(0x45, 0x4C, 0x56),
            SelBg = Color.FromArgb(0x9C, 0xC2, 0xF0),
            OnSelection = Color.FromArgb(0x0B, 0x12, 0x20),

            SidebarBg = Color.FromArgb(0x2A, 0x2F, 0x37),
            SidebarHeaderBg = Color.FromArgb(0x27, 0x2C, 0x34),
            RibbonSeparator = Color.FromArgb(0x45, 0x4C, 0x56),
            Disabled = Color.FromArgb(0x8A, 0x97, 0xA8),

            HeaderDark = Color.FromArgb(0x22, 0x27, 0x2E),
            HeaderBottom = Color.FromArgb(0x1A, 0x1F, 0x25),
            RibbonText = Color.FromArgb(0xE2, 0xE8, 0xF0),
            RibbonHover = Color.FromArgb(0x34, 0x3A, 0x44),
            RibbonActive = Color.FromArgb(0x2C, 0x5B, 0xC4),
            RibbonActiveHover = Color.FromArgb(0x24, 0x48, 0x9B),
            RibbonDown = Color.FromArgb(0x1E, 0x3A, 0x8A),
            RibbonActivePressed = Color.FromArgb(0x18, 0x2F, 0x6E),
            RibbonDisabledIcon = Color.FromArgb(0x7C, 0x8F, 0xA8),
            RibbonSeparatorDark = Color.FromArgb(0x4A, 0x50, 0x5A),
            SidebarBg2 = Color.FromArgb(0x26, 0x2B, 0x32),
            SidebarHeaderBg2 = Color.FromArgb(0x2A, 0x2F, 0x37),
            SidebarHairline = Color.FromArgb(0x45, 0x4C, 0x56),
            SidebarText = Color.FromArgb(0xC6, 0xCC, 0xD4),
            SidebarTextGroup = Color.FromArgb(0xE6, 0xE9, 0xED),
            SidebarActive = Color.FromArgb(0x3A, 0x41, 0x49),

            SubtitleText = Color.FromArgb(0xB7, 0xC6, 0xD8),
            HeaderBg = Color.FromArgb(0x27, 0x2C, 0x34),
            CenterBg = Color.FromArgb(0x26, 0x2B, 0x32),
            SecondaryBg = Color.FromArgb(0x2C, 0x31, 0x3A),
            SecondaryHoverBg = Color.FromArgb(0x39, 0x3F, 0x49),
            GlyphIdle = Color.FromArgb(0x8A, 0x93, 0x9E),
            HoverBg = Color.FromArgb(0x39, 0x3F, 0x49),
            Glow = Color.FromArgb(0x30, 0x00, 0x00, 0x00),

            OnBrand = Color.FromArgb(0xF8, 0xFA, 0xFC),
            OnHeader = Color.FromArgb(0xF8, 0xFA, 0xFC),
            Error = Color.FromArgb(0xFF, 0x9B, 0x93),
            RibbonPressed = Color.FromArgb(0x35, 0x3B, 0x45),
        };

        /// <summary>
        /// Claro con acentos verde/teal: superficies claras con tinte verde muy
        /// suave, encabezados y acciones en verde profundo.
        /// </summary>
        internal static readonly SoproTemaPaleta ClaroVerde = new()
        {
            Preset = SoproTemaPreset.ClaroVerde,
            Id = "claro-verde",
            Nombre = "Verde claro",

            Brand = Color.FromArgb(0x0F, 0x6B, 0x4F),
            BrandMd = Color.FromArgb(0x12, 0x80, 0x5F),
            BrandLt = Color.FromArgb(0x2E, 0x9E, 0x7A),
            BrandHover = Color.FromArgb(0x0B, 0x6B, 0x4F),

            Ink = Color.FromArgb(0x07, 0x1B, 0x14),
            Ink2 = Color.FromArgb(0x1D, 0x3A, 0x2F),
            Muted = Color.FromArgb(0x46, 0x69, 0x5B),

            BgBase = Color.FromArgb(0xDD, 0xF3, 0xE9),
            BgTint2 = Color.FromArgb(0xE7, 0xF7, 0xEF),

            Surface = Color.FromArgb(0xFF, 0xFF, 0xFF),
            SurfaceBorder = Color.FromArgb(0xC7, 0xE2, 0xD5),

            RowAlt = Color.FromArgb(0xF5, 0xFA, 0xF7),
            GridLine = Color.FromArgb(0xDC, 0xEB, 0xE4),
            SelBg = Color.FromArgb(0xCF, 0xEB, 0xDE),
            OnSelection = Color.FromArgb(0x07, 0x1B, 0x14),

            SidebarBg = Color.FromArgb(0xFA, 0xFC, 0xFB),
            SidebarHeaderBg = Color.FromArgb(0xF0, 0xF6, 0xF3),
            RibbonSeparator = Color.FromArgb(0xC8, 0xD8, 0xD0),
            Disabled = Color.FromArgb(0x6B, 0x7F, 0x76),

            HeaderDark = Color.FromArgb(0x13, 0x31, 0x29),
            HeaderBottom = Color.FromArgb(0x0A, 0x21, 0x1B),
            RibbonText = Color.FromArgb(0xE6, 0xF2, 0xEC),
            RibbonHover = Color.FromArgb(0x2F, 0x4A, 0x3F),
            RibbonActive = Color.FromArgb(0x0E, 0x7A, 0x57),
            RibbonActiveHover = Color.FromArgb(0x0B, 0x6B, 0x4F),
            RibbonDown = Color.FromArgb(0x0A, 0x5C, 0x42),
            RibbonActivePressed = Color.FromArgb(0x08, 0x4A, 0x36),
            RibbonDisabledIcon = Color.FromArgb(0x6E, 0x8A, 0x80),
            RibbonSeparatorDark = Color.FromArgb(0x47, 0x63, 0x5A),
            SidebarBg2 = Color.FromArgb(0xF0, 0xF7, 0xF3),
            SidebarHeaderBg2 = Color.FromArgb(0xE1, 0xEF, 0xE7),
            SidebarHairline = Color.FromArgb(0xD5, 0xE7, 0xDE),
            SidebarText = Color.FromArgb(0x2C, 0x4A, 0x3E),
            SidebarTextGroup = Color.FromArgb(0x0B, 0x2A, 0x20),
            SidebarActive = Color.FromArgb(0xCF, 0xEB, 0xDE),

            SubtitleText = Color.FromArgb(0xB7, 0xD2, 0xC6),
            HeaderBg = Color.FromArgb(0xF0, 0xF7, 0xF3),
            CenterBg = Color.FromArgb(0xF8, 0xFC, 0xFA),
            SecondaryBg = Color.FromArgb(0xF0, 0xF7, 0xF3),
            SecondaryHoverBg = Color.FromArgb(0xE1, 0xEF, 0xE7),
            GlyphIdle = Color.FromArgb(0x8A, 0x9A, 0x93),
            HoverBg = Color.FromArgb(0xE9, 0xF6, 0xEF),
            Glow = Color.FromArgb(0x18, 0x13, 0x31, 0x29),

            OnBrand = Color.FromArgb(0xFF, 0xFF, 0xFF),
            OnHeader = Color.FromArgb(0xF8, 0xFA, 0xFC),
            Error = Color.FromArgb(0xB4, 0x23, 0x18),
            RibbonPressed = Color.FromArgb(0xCF, 0xEB, 0xDE),
        };
    }
}
