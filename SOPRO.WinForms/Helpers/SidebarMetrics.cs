using System;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Métricas lógicas del sidebar (#193) a 96 DPI, escaladas con la misma
    /// regla que FormProyecto (techo de px*dpi/96). Código puro, testeable
    /// sin UI.
    /// </summary>
    public static class SidebarMetrics
    {
        internal const int AltoFilaBase = 34;
        internal const int AltoFilaColapsadaBase = 32;
        internal const int AltoHeaderBase = 36;
        internal const int MargenLateralBase = 12;
        internal const int SangriaNivelBase = 16;
        internal const int AcolchadoPildoraVerticalBase = 3;
        internal const int AcolchadoPildoraHorizontalBase = 10;
        internal const int RadioPildoraBase = 8;
        internal const int AnchoChevronBase = 8;
        internal const int AltoChevronBase = 5;
        internal const int MargenChevronBase = 10;
        internal const int LadoBotonColapsoBase = 22;
        internal const int MargenBotonColapsoBase = 6;

        internal static int Escalar(int pixelesBase, int dpi)
        {
            var dpiSeguro = dpi > 0 ? dpi : 96;
            return (int)Math.Ceiling(pixelesBase * dpiSeguro / 96d);
        }

        public static SidebarEscala Crear(int dpi) => new(dpi);
    }

    /// <summary>Métricas del sidebar ya escaladas a un DPI concreto.</summary>
    public readonly struct SidebarEscala
    {
        public SidebarEscala(int dpi)
        {
            var escalar = SidebarMetrics.Escalar;
            Dpi = dpi > 0 ? dpi : 96;
            AltoFila = escalar(SidebarMetrics.AltoFilaBase, Dpi);
            AltoFilaColapsada = escalar(SidebarMetrics.AltoFilaColapsadaBase, Dpi);
            AltoHeader = escalar(SidebarMetrics.AltoHeaderBase, Dpi);
            MargenLateral = escalar(SidebarMetrics.MargenLateralBase, Dpi);
            SangriaNivel = escalar(SidebarMetrics.SangriaNivelBase, Dpi);
            AcolchadoPildoraVertical = escalar(SidebarMetrics.AcolchadoPildoraVerticalBase, Dpi);
            AcolchadoPildoraHorizontal = escalar(SidebarMetrics.AcolchadoPildoraHorizontalBase, Dpi);
            RadioPildora = escalar(SidebarMetrics.RadioPildoraBase, Dpi);
            AnchoChevron = escalar(SidebarMetrics.AnchoChevronBase, Dpi);
            AltoChevron = escalar(SidebarMetrics.AltoChevronBase, Dpi);
            MargenChevron = escalar(SidebarMetrics.MargenChevronBase, Dpi);
            LadoBotonColapso = escalar(SidebarMetrics.LadoBotonColapsoBase, Dpi);
            MargenBotonColapso = escalar(SidebarMetrics.MargenBotonColapsoBase, Dpi);
        }

        public int Dpi { get; }
        public int AltoFila { get; }
        public int AltoFilaColapsada { get; }
        public int AltoHeader { get; }
        public int MargenLateral { get; }
        public int SangriaNivel { get; }
        public int AcolchadoPildoraVertical { get; }
        public int AcolchadoPildoraHorizontal { get; }
        public int RadioPildora { get; }
        public int AnchoChevron { get; }
        public int AltoChevron { get; }
        public int MargenChevron { get; }
        public int LadoBotonColapso { get; }
        public int MargenBotonColapso { get; }

        /// <summary>X de la píldora de una hoja según su nivel (1 = hija de sección).</summary>
        public int SangriaHoja(int nivel)
            => MargenLateral + Math.Max(0, nivel - 1) * SangriaNivel;
    }
}
