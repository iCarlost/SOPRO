using System;
using System.Collections.Generic;
using System.Linq;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MOrientation = MigraDoc.DocumentObjectModel.Orientation;

namespace SOPRO.WinForms.Services
{
    internal static class ReportPageLayoutHelper
    {
        private const double LetterPortraitWidthCm = 21.59;
        private const double LetterLandscapeWidthCm = 27.94;

        public static MOrientation DetermineAutoOrientation(IEnumerable<(string key, int widthPx)> columns, double leftMarginCm = 1.0, double rightMarginCm = 1.0)
        {
            var list = columns?.ToList() ?? new List<(string key, int widthPx)>();
            if (list.Count == 0)
                return MOrientation.Portrait;

            var requiredCm = list.Sum(c => EstimateMinimumWidthCm(c.key, c.widthPx));
            var portraitContentCm = LetterPortraitWidthCm - leftMarginCm - rightMarginCm;

            return requiredCm <= portraitContentCm * 0.98
                ? MOrientation.Portrait
                : MOrientation.Landscape;
        }

        public static double GetLetterContentWidthCm(Section section)
        {
            var left = section.PageSetup.LeftMargin.Centimeter;
            var right = section.PageSetup.RightMargin.Centimeter;
            var pageWidth = section.PageSetup.Orientation == MOrientation.Landscape
                ? LetterLandscapeWidthCm
                : LetterPortraitWidthCm;
            return Math.Max(8.0, pageWidth - left - right);
        }

        public static double GetContentWidthCm(Section section)
        {
            var left = section.PageSetup.LeftMargin.Centimeter;
            var right = section.PageSetup.RightMargin.Centimeter;
            var pageWidth = ResolvePageWidthCm(section);
            return Math.Max(8.0, pageWidth - left - right);
        }

        public static void AddHeaderFooterColumns(Table table, Section section, int count = 3)
        {
            var contentWidth = GetContentWidthCm(section);
            var each = contentWidth / Math.Max(1, count);
            for (int i = 0; i < count; i++)
                table.AddColumn(Unit.FromCentimeter(each));
        }


        private static double ResolvePageWidthCm(Section section)
        {
            double width = section.PageSetup.PageWidth.Centimeter;
            double height = section.PageSetup.PageHeight.Centimeter;

            if (width <= 0.01 || height <= 0.01)
            {
                (width, height) = section.PageSetup.PageFormat switch
                {
                    PageFormat.Letter => (21.59, 27.94),
                    PageFormat.Legal => (21.59, 35.56),
                    PageFormat.A4 => (21.0, 29.7),
                    PageFormat.A3 => (29.7, 42.0),
                    _ => (21.59, 27.94)
                };
            }

            if (section.PageSetup.Orientation == MOrientation.Landscape && width < height)
                (width, height) = (height, width);
            else if (section.PageSetup.Orientation == MOrientation.Portrait && width > height)
                (width, height) = (height, width);

            return width;
        }

        private static double EstimateMinimumWidthCm(string key, int widthPx)
        {
            key = (key ?? string.Empty).Trim().ToLowerInvariant();

            if (key.Contains("descripcion")) return Math.Max(6.8, widthPx / 50.0);
            if (key.Contains("formula")) return Math.Max(5.8, widthPx / 55.0);
            if (key.Contains("operacion")) return Math.Max(4.8, widthPx / 58.0);
            if (key.Contains("cantidad")) return Math.Max(2.1, widthPx / 62.0);
            if (key.Contains("precio") || key.Contains("unitario") || key.Contains("importe")) return Math.Max(2.5, widthPx / 58.0);
            if (key.Contains("total")) return Math.Max(2.5, widthPx / 58.0);
            if (key.Contains("porcentaje")) return Math.Max(2.2, widthPx / 60.0);
            if (key.Contains("unidad")) return Math.Max(1.8, widthPx / 64.0);
            if (key.Contains("clave") || key.Contains("tipo") || key.Contains("origen")) return Math.Max(1.6, widthPx / 70.0);
            if (key.Contains("rendimiento")) return Math.Max(2.2, widthPx / 60.0);

            return Math.Clamp(widthPx / 60.0, 1.4, 4.0);
        }
    }
}
