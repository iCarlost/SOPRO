using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using System;
using System.Collections.Generic;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Fábrica PURA de las definiciones de columna predeterminadas del presupuesto.
    /// No conoce contextos ni escribe: entrega DTOs disconnected que el formulario
    /// envía al servicio de Application antes de pintar el grid.
    /// </summary>
    public static class ColumnasPresupuestoHelper
    {
        /// <summary>
        /// 23 columnas predeterminadas: las 7 base visibles, las 16 adicionales
        /// ocultas y las 23 con su orden canónico (-7..16).
        /// </summary>
        public static IReadOnlyList<BudgetColumnDefinitionDto> CrearDefinicionesPredeterminadas()
        {
            return new List<BudgetColumnDefinitionDto>
            {
                // ============================================
                // COLUMNAS BASE DEL SISTEMA (siempre visibles)
                // ============================================
                Columna("Tipo", "Tipo", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Texto,
                    ancho: 120, visible: true, orden: -7, alineacion: AlineacionColumna.Centro),
                Columna("Clave", "Clave", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Texto,
                    ancho: 100, visible: true, orden: -6, alineacion: AlineacionColumna.Izquierda),
                Columna("Descripción", "Descripcion", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Texto,
                    ancho: 300, visible: true, orden: -5, alineacion: AlineacionColumna.Izquierda),
                Columna("Unidad", "Unidad", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Texto,
                    ancho: 80, visible: true, orden: -4, alineacion: AlineacionColumna.Centro),
                Columna("Cantidad", "Cantidad", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Numerico,
                    ancho: 100, visible: true, orden: -3, alineacion: AlineacionColumna.Derecha,
                    formatoNumerico: "N2"),
                Columna("P.U.", "PrecioUnitario", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 120, visible: true, orden: -2, alineacion: AlineacionColumna.Derecha,
                    formula: "CostoDirectoUnitario", formatoNumerico: "C4"),
                Columna("Importe", "Importe", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 140, visible: true, orden: -1, alineacion: AlineacionColumna.Derecha,
                    formula: "Cantidad * CostoDirectoUnitario", formatoNumerico: "C2"),

                // ============================================
                // COLUMNAS ADICIONALES (ocultas por defecto)
                // ============================================
                Columna("% Indirectos", "PorcentajeIndirectos", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Porcentaje,
                    ancho: 90, visible: false, orden: 1, alineacion: AlineacionColumna.Derecha,
                    formatoNumerico: "0.00"),
                Columna("Indirectos", "Indirectos", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 110, visible: false, orden: 2, alineacion: AlineacionColumna.Derecha,
                    formula: "CostoDirecto * (PorcentajeIndirectos / 100)", formatoNumerico: "C2"),
                Columna("% Financiamiento", "PorcentajeFinanciamiento", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Porcentaje,
                    ancho: 110, visible: false, orden: 3, alineacion: AlineacionColumna.Derecha,
                    formatoNumerico: "0.00"),
                Columna("Financiamiento", "Financiamiento", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 110, visible: false, orden: 4, alineacion: AlineacionColumna.Derecha,
                    formula: "(CostoDirecto + Indirectos) * (PorcentajeFinanciamiento / 100)", formatoNumerico: "C2"),
                Columna("% Utilidad", "PorcentajeUtilidad", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Porcentaje,
                    ancho: 90, visible: false, orden: 5, alineacion: AlineacionColumna.Derecha,
                    formatoNumerico: "0.00"),
                Columna("Utilidad", "Utilidad", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 110, visible: false, orden: 6, alineacion: AlineacionColumna.Derecha,
                    formula: "(CostoDirecto + Indirectos + Financiamiento) * (PorcentajeUtilidad / 100)", formatoNumerico: "C2"),
                Columna("Cargos Adicionales", "CargosAdicionales", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Moneda,
                    ancho: 130, visible: false, orden: 7, alineacion: AlineacionColumna.Derecha,
                    formatoNumerico: "C2"),
                Columna("P.U. Final", "PrecioUnitarioFinal", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 120, visible: false, orden: 8, alineacion: AlineacionColumna.Derecha,
                    formula: "CostoDirecto + Indirectos + Financiamiento + Utilidad + CargosAdicionales", formatoNumerico: "C4"),
                Columna("Subtotal", "Subtotal", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 120, visible: false, orden: 9, alineacion: AlineacionColumna.Derecha,
                    formula: "Cantidad * PrecioUnitarioFinal", formatoNumerico: "C2"),
                Columna("IVA", "IVA", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 110, visible: false, orden: 10, alineacion: AlineacionColumna.Derecha,
                    formula: "Subtotal * 0.16", formatoNumerico: "C2"),
                Columna("Total", "Total", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 130, visible: false, orden: 11, alineacion: AlineacionColumna.Derecha,
                    formula: "Subtotal + IVA", formatoNumerico: "C2"),
                Columna("Incidencia %", "IncidenciaPorcentaje", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Porcentaje,
                    ancho: 100, visible: false, orden: 12, alineacion: AlineacionColumna.Derecha,
                    formula: "(Importe / TotalPresupuesto) * 100", formatoNumerico: "0.00"),
                Columna("Observaciones", "Observaciones", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Texto,
                    ancho: 200, visible: false, orden: 13, alineacion: AlineacionColumna.Izquierda),
                Columna("Importe M.O.", "ImporteManoObra", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 120, visible: false, orden: 14, alineacion: AlineacionColumna.Derecha,
                    formula: "ImporteManoObraAPU * Cantidad", formatoNumerico: "C2"),
                Columna("P.U. en Letra", "PrecioUnitarioLetra", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Texto,
                    ancho: 250, visible: false, orden: 15, alineacion: AlineacionColumna.Izquierda,
                    formula: "NumeroALetra(PrecioUnitarioFinal)"),
                Columna("Total en Letra", "TotalLetra", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Texto,
                    ancho: 300, visible: false, orden: 16, alineacion: AlineacionColumna.Izquierda,
                    formula: "NumeroALetra(Total)")
            };
        }

        /// <summary>Definición disconnected para el alta de una columna personal.</summary>
        public static BudgetColumnDefinitionDto CrearDefinicionPersonal(string nombre)
        {
            return new BudgetColumnDefinitionDto
            {
                Nombre = nombre ?? string.Empty,
                NombreInterno = string.Empty,
                TipoColumna = TipoColumnaPersonalizada.Personal,
                TipoDato = TipoDatoColumna.Texto,
                AnchoColumna = 100,
                Visible = true
            };
        }

        private static BudgetColumnDefinitionDto Columna(
            string nombre,
            string nombreInterno,
            TipoColumnaPersonalizada tipoColumna,
            TipoDatoColumna tipoDato,
            int ancho,
            bool visible,
            int orden,
            AlineacionColumna alineacion,
            string formula = "",
            string formatoNumerico = "")
        {
            return new BudgetColumnDefinitionDto
            {
                Nombre = nombre,
                NombreInterno = nombreInterno,
                TipoColumna = tipoColumna,
                TipoDato = tipoDato,
                AnchoColumna = ancho,
                Visible = visible,
                Orden = orden,
                Alineacion = alineacion,
                Formula = formula,
                FormatoNumerico = formatoNumerico
            };
        }

        /// <summary>
        /// Columnas cuyo valor por fila NO se carga desde la entidad ni se persiste
        /// con ella: los porcentajes derivados del proyecto (% Indirectos,
        /// % Financiamiento, % Utilidad) y CargosAdicionales. Ponerlas en solo
        /// lectura evita ediciones que el autoguardado no puede mapear.
        /// </summary>
        public static bool EsColumnaSinPersistenciaPorFila(string? nombreInterno)
        {
            return string.Equals(nombreInterno, "PorcentajeIndirectos", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nombreInterno, "PorcentajeFinanciamiento", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nombreInterno, "PorcentajeUtilidad", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nombreInterno, "CargosAdicionales", StringComparison.OrdinalIgnoreCase);
        }
    }
}
