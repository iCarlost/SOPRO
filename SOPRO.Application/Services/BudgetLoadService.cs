using Microsoft.EntityFrameworkCore;
using Sopro.Calculation;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  BudgetLoadService — VERSIÓN CORREGIDA v1.0                            ║
    // ║                                                                         ║
    // ║  CAMBIOS RESPECTO A LA VERSIÓN ORIGINAL:                                ║
    // ║  [FIX-1] BuildRowDisplay: desglose Indirectos / Financiamiento /        ║
    // ║          Utilidad calculado con motor.CalcularPrecioUnitario().         ║
    // ║          Cada paso intermedio ya redondeado → columnas cuadran con PU.  ║
    // ║  [FIX-2] IVA y Total redondeados con motor.RedondearImporte().          ║
    // ║  [FIX-3] Eliminados métodos privados duplicados:                        ║
    // ║          MultiplyUsingDisplayPrecision() y RoundImporte() locales.      ║
    // ║          Toda aritmética delegada a MotorCalculoSopro.                  ║
    // ║  [FIX-4] FormatCantidad/Importe/Porcentaje: delegados al motor.         ║
    // ║                                                                         ║
    // ║  [N5-6] Aritmética delegada directo a SoproCalculationEngine            ║
    // ║         (SOPRO.Calculation), sin pasar por la fachada legacy:           ║
    // ║         CalcularPrecioUnitario → CalculateUnitPrice (con                 ║
    // ║         PricePercentageInput construido inline, mismo criterio que      ║
    // ║         BuildEnginePercentages: "SobreCD" sin importar mayúsculas →     ║
    // ║         OverDirectCost, resto → Accumulative), Multiplicar → Multiply,   ║
    // ║         RedondearImporte → RoundAmount (IVA y total). BudgetPercentage- ║
    // ║         Input deja de usarse en este servicio (se construye directo     ║
    // ║         el tipo del motor).                                             ║
    // ║  [N5-6] FormatCantidad/Importe/Porcentaje PERMANECEN en la fachada      ║
    // ║         por N0 fila 9 (el formato con cultura es rol sancionado de      ║
    // ║         MotorCalculoSopro; el motor no tiene formato por diseño).       ║
    // ║         La variable motor queda solo para esas llamadas de formato.     ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    public static class BudgetLoadService
    {
        public static BudgetGridLoadState BuildLoadState(SOPROContext context, Proyecto proyecto)
        {
            if (context == null)  throw new ArgumentNullException(nameof(context));
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));

            var state = new BudgetGridLoadState();
            state.ColumnDefinitions = BuildColumnDefinitions(context, proyecto,
                out bool usesTemporaryBaseColumns);
            state.UsesTemporaryBaseColumns = usesTemporaryBaseColumns;

            var conceptos = context.ConceptosPresupuesto
                .Include(c => c.Matriz)
                .Where(c => c.ProyectoId == proyecto.Id)
                .OrderBy(c => c.Orden)
                .ToList();

            foreach (var concepto in conceptos)
                state.RowDisplays.Add(BuildRowDisplay(proyecto, concepto));

            return state;
        }

        public static List<BudgetGridColumnDefinition> BuildColumnDefinitions(
            SOPROContext context, Proyecto proyecto, out bool usesTemporaryBaseColumns)
        {
            if (context == null)  throw new ArgumentNullException(nameof(context));
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));

            var definitions  = new List<BudgetGridColumnDefinition>();
            // Las definiciones de columna son snapshots de solo lectura para pintar y
            // para el Tag del grid: no deben quedar rastreadas ni exponer mutaciones
            // accidentales sobre el contexto compartido (las escrituras van por
            // BudgetColumnConfigurationService, con contexto propio).
            var todasColumnas = context.ColumnasPersonalizadas
                .AsNoTracking()
                .Where(c => c.ProyectoId == proyecto.Id)
                .OrderBy(c => c.Visible ? 0 : 1)
                .ThenBy(c => c.Orden)
                .ToList();

            var columnasVisibles = todasColumnas.Where(c => c.Visible).ToList();
            usesTemporaryBaseColumns = columnasVisibles.Count == 0;

            if (usesTemporaryBaseColumns)
            {
                definitions.AddRange(BuildTemporaryBaseColumnDefinitions());
                return definitions;
            }

            foreach (var col in todasColumnas)
            {
                definitions.Add(new BudgetGridColumnDefinition
                {
                    Name                = "col_" + col.NombreInterno,
                    HeaderText          = col.Nombre,
                    NombreInterno       = col.NombreInterno,
                    Width               = col.AnchoColumna,
                    IsVisible           = col.Visible,
                    IsReadOnly          = col.TipoColumna == TipoColumnaPersonalizada.Calculada,
                    IsTypeSelector      = col.NombreInterno == "Tipo",
                    IsFillColumn        = false,
                    AlignRight          = col.TipoDato == TipoDatoColumna.Moneda
                                       || col.TipoDato == TipoDatoColumna.Numerico
                                       || col.TipoDato == TipoDatoColumna.Porcentaje,
                    AlignCenter         = col.Alineacion == AlineacionColumna.Centro,
                    UseCalculatedBackColor = col.TipoColumna == TipoColumnaPersonalizada.Calculada,
                    DisplayIndex        = col.Orden,
                    TipoDato            = col.TipoDato,
                    SourceColumn        = col
                });
            }

            definitions.Add(new BudgetGridColumnDefinition
            {
                Name          = "colRelleno",
                HeaderText    = string.Empty,
                NombreInterno = "Relleno",
                IsReadOnly    = true,
                IsFillColumn  = true,
                TipoDato      = TipoDatoColumna.Texto
            });

            return definitions;
        }

        public static List<BudgetGridColumnDefinition> BuildTemporaryBaseColumnDefinitions()
        {
            return new List<BudgetGridColumnDefinition>
            {
                new() { Name = "col_Tipo",          HeaderText = "Tipo",        NombreInterno = "Tipo",          Width = 120, IsTypeSelector = true,  TipoDato = TipoDatoColumna.Texto,    SourceColumn = BuildTempColumn("Tipo",          "Tipo",          TipoDatoColumna.Texto,    false) },
                new() { Name = "col_Clave",         HeaderText = "Clave",       NombreInterno = "Clave",         Width = 100, TipoDato = TipoDatoColumna.Texto,                             SourceColumn = BuildTempColumn("Clave",         "Clave",         TipoDatoColumna.Texto,    false) },
                new() { Name = "col_Descripcion",   HeaderText = "Descripción", NombreInterno = "Descripcion",   Width = 300, TipoDato = TipoDatoColumna.Texto,                             SourceColumn = BuildTempColumn("Descripcion",   "Descripción",   TipoDatoColumna.Texto,    false) },
                new() { Name = "col_Unidad",        HeaderText = "Unidad",      NombreInterno = "Unidad",        Width = 80,  TipoDato = TipoDatoColumna.Texto,                             SourceColumn = BuildTempColumn("Unidad",        "Unidad",        TipoDatoColumna.Texto,    false) },
                new() { Name = "col_Cantidad",      HeaderText = "Cantidad",    NombreInterno = "Cantidad",      Width = 100, TipoDato = TipoDatoColumna.Numerico, AlignRight = true,        SourceColumn = BuildTempColumn("Cantidad",      "Cantidad",      TipoDatoColumna.Numerico, false) },
                new() { Name = "col_PrecioUnitario",HeaderText = "P.U.",        NombreInterno = "PrecioUnitario",Width = 120, TipoDato = TipoDatoColumna.Moneda,   AlignRight = true, IsReadOnly = true, UseCalculatedBackColor = true, SourceColumn = BuildTempColumn("PrecioUnitario","P.U.",          TipoDatoColumna.Moneda,   true) },
                new() { Name = "col_Importe",       HeaderText = "Importe",     NombreInterno = "Importe",       Width = 140, TipoDato = TipoDatoColumna.Moneda,   AlignRight = true, IsReadOnly = true, UseCalculatedBackColor = true, SourceColumn = BuildTempColumn("Importe",       "Importe",       TipoDatoColumna.Moneda,   true) }
            };
        }

        private static ColumnaPersonalizada BuildTempColumn(
            string nombreInterno, string nombre, TipoDatoColumna tipo, bool calculada)
        {
            return new ColumnaPersonalizada
            {
                NombreInterno = nombreInterno,
                Nombre        = nombre,
                TipoColumna   = calculada ? TipoColumnaPersonalizada.Calculada : TipoColumnaPersonalizada.Personal,
                TipoDato      = tipo
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        // CONSTRUCCIÓN DE FILA — NÚCLEO CORREGIDO
        // ════════════════════════════════════════════════════════════════════════

        public static BudgetGridRowDisplay BuildRowDisplay(Proyecto proyecto, ConceptoPresupuesto concepto)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (concepto == null) throw new ArgumentNullException(nameof(concepto));

            // ── Motor: instancia única por llamada, configuración del proyecto ───
            // [N5-6] Aritmética en el engine; la fachada solo para formato (N0 fila 9).
            var motor = new MotorCalculoSopro(proyecto);
            var engine = CalculationEngineFactory.FromProyecto(proyecto);

            var pctInput = BudgetPercentageInput.FromProyecto(proyecto).ToPricePercentage();

            var values = new Dictionary<string, object?>();

            if (concepto.EsAgrupador)
            {
                // ── Agrupadores: solo muestran el importe acumulado ───────────────
                values["Tipo"]           = GetTypeText(concepto.Nivel, concepto.EsAgrupador);
                values["Clave"]          = concepto.Clave        ?? string.Empty;
                values["Descripcion"]    = concepto.Descripcion  ?? string.Empty;
                values["Unidad"]         = string.Empty;
                values["Cantidad"]       = string.Empty;
                values["PrecioUnitario"] = string.Empty;
                values["Importe"]        = motor.FormatImporte(concepto.CostoDirectoTotal);

                // Columnas extendidas vacías para agrupadores
                values["PorcentajeIndirectos"]    = string.Empty;
                values["PorcentajeFinanciamiento"]= string.Empty;
                values["PorcentajeUtilidad"]      = string.Empty;
                values["CargosAdicionales"]       = string.Empty;
                values["Observaciones"]           = concepto.Notas ?? string.Empty;
                values["Indirectos"]              = string.Empty;
                values["Financiamiento"]          = string.Empty;
                values["Utilidad"]                = string.Empty;
                values["PrecioUnitarioFinal"]     = string.Empty;
                values["Subtotal"]                = string.Empty;
                values["IVA"]                     = string.Empty;
                values["Total"]                   = string.Empty;
                values["PrecioUnitarioLetra"]     = string.Empty;
                values["TotalLetra"]              = string.Empty;
                values["IncidenciaPorcentaje"]    = string.Empty;
                values["ImporteManoObra"]         = string.Empty;
            }
            else
            {
                // ── Conceptos hoja: desglose completo con precisión de pantalla ───

                // [FIX-1] Desglose con redondeo en cada paso intermedio visible.
                //         Garantía: CD + Indirectos + Financiamiento + Utilidad + Cargos == P.U.
                var desglose = engine.CalculateUnitPrice(concepto.CostoDirectoUnitario, pctInput);

                decimal pu      = desglose.UnitPrice;
                decimal importe = engine.Multiply(concepto.Cantidad, pu);

                // [FIX-2] IVA y Total redondeados con el motor usando tasa del proyecto
                decimal subtotal = importe;
                // PorcentajeIVA del proyecto (default 16 en el constructor de Proyecto).
                // Si el usuario lo puso en 0 = sin IVA. No aplicar fallback implícito.
                decimal tasaIva  = proyecto.PorcentajeIVA / 100m;
                decimal iva      = engine.RoundAmount(subtotal * tasaIva);
                decimal total    = engine.RoundAmount(subtotal + iva);

                decimal pctIndTotal = proyecto.PorcentajeIndirectosCentral
                                    + proyecto.PorcentajeIndirectosCampo;

                values["Tipo"]           = GetTypeText(concepto.Nivel, concepto.EsAgrupador);
                values["Clave"]          = concepto.Clave       ?? string.Empty;
                values["Descripcion"]    = concepto.Descripcion ?? string.Empty;
                values["Unidad"]         = concepto.Unidad      ?? string.Empty;
                values["Cantidad"]       = motor.FormatCantidad(concepto.Cantidad);
                values["PrecioUnitario"] = motor.FormatImporte(pu);
                values["Importe"]        = motor.FormatImporte(importe);

                values["PorcentajeIndirectos"]    = motor.FormatPorcentaje(pctIndTotal) + "%";
                values["PorcentajeFinanciamiento"]= motor.FormatPorcentaje(proyecto.PorcentajeFinanciamiento) + "%";
                values["PorcentajeUtilidad"]      = motor.FormatPorcentaje(proyecto.PorcentajeUtilidad) + "%";
                values["CargosAdicionales"]       = string.Empty;
                values["Observaciones"]           = concepto.Notas ?? string.Empty;

                // [FIX-1] Columnas de desglose: valores del DesglosePrecios ya redondeados
                values["Indirectos"]          = motor.FormatImporte(desglose.IndirectCosts);
                values["Financiamiento"]      = motor.FormatImporte(desglose.Financing);
                values["Utilidad"]            = motor.FormatImporte(desglose.Profit);
                values["PrecioUnitarioFinal"] = motor.FormatImporte(pu);
                values["Subtotal"]            = motor.FormatImporte(subtotal);
                values["IVA"]                 = motor.FormatImporte(iva);
                values["Total"]               = motor.FormatImporte(total);
                values["PrecioUnitarioLetra"] = BudgetPricingService.ConvertirALetras(pu);
                values["TotalLetra"]          = BudgetPricingService.ConvertirALetras(total);
                values["IncidenciaPorcentaje"]= string.Empty;
                values["ImporteManoObra"]     = string.Empty;
            }

            return new BudgetGridRowDisplay
            {
                Concepto             = concepto,
                ValuesByInternalName = values
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        // HELPERS DE TIPO DE FILA
        // ════════════════════════════════════════════════════════════════════════

        public static string GetTypeText(int nivel, bool esAgrupador)
        {
            if (!esAgrupador) return "Concepto";

            return nivel switch
            {
                0 => "Capitulo",
                1 => "Subcapitulo",
                2 => "Nivel 1",
                3 => "Nivel 2",
                4 => "Nivel 3",
                _ => "Nivel " + nivel
            };
        }

    }
}
