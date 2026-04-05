using Microsoft.EntityFrameworkCore;
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
            var todasColumnas = context.ColumnasPersonalizadas
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
            var motor = new MotorCalculoSopro(proyecto);

            var pctInput = new BudgetPercentageInput
            {
                IndirectosCentral      = proyecto.PorcentajeIndirectosCentral,
                IndirectosCampo        = proyecto.PorcentajeIndirectosCampo,
                Financiamiento         = proyecto.PorcentajeFinanciamiento,
                Utilidad               = proyecto.PorcentajeUtilidad,
                CargosAdicionales      = proyecto.PorcentajeCargosAdicionales,
                ModoCalculoPorcentajes = proyecto.ModoCalculoPorcentajes ?? "Acumulables"
            };

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
                values["Observaciones"]           = string.Empty;
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
                var desglose = motor.CalcularPrecioUnitario(concepto.CostoDirectoUnitario, pctInput);

                decimal pu      = desglose.PrecioUnitario;
                decimal importe = motor.Multiplicar(concepto.Cantidad, pu);

                // [FIX-2] IVA y Total redondeados con el motor usando tasa del proyecto
                decimal subtotal = importe;
                // PorcentajeIVA del proyecto (default 16 en el constructor de Proyecto).
                // Si el usuario lo puso en 0 = sin IVA. No aplicar fallback implícito.
                decimal tasaIva  = proyecto.PorcentajeIVA / 100m;
                decimal iva      = motor.RedondearImporte(subtotal * tasaIva);
                decimal total    = motor.RedondearImporte(subtotal + iva);

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
                values["Observaciones"]           = string.Empty;

                // [FIX-1] Columnas de desglose: valores del DesglosePrecios ya redondeados
                values["Indirectos"]          = motor.FormatImporte(desglose.Indirectos);
                values["Financiamiento"]      = motor.FormatImporte(desglose.Financiamiento);
                values["Utilidad"]            = motor.FormatImporte(desglose.Utilidad);
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

        // ════════════════════════════════════════════════════════════════════════
        // HELPERS DE CONVERSIÓN A LETRAS (sin cambios)
        // ════════════════════════════════════════════════════════════════════════

        // NOTA: ConvertirALetras y ConvertirEnteroALetras se delegan a
        //       BudgetPricingService.ConvertirALetras() — no se duplica aquí.
        //       Si BudgetPricingService no los exporta como public, mover
        //       los métodos a una clase utilitaria compartida TextoHelper.

        // [FIX-3] Eliminados métodos privados duplicados:
        //   private static decimal MultiplyUsingDisplayPrecision(...)  ← ELIMINADO
        //   private static decimal RoundImporte(...)                   ← ELIMINADO
        //   private static string FormatCantidad(...)                  ← ELIMINADO (usa motor)
        //   private static string FormatImporte(...)                   ← ELIMINADO (usa motor)
        //   private static string FormatPorcentaje(...)                ← ELIMINADO (usa motor)
        //
        // Estos eran copias parciales de funcionalidad ahora en MotorCalculoSopro.
        // El motor es la única fuente de verdad.

        // ConvertirEnteroALetras y ConvertirGrupo se mantienen como helpers locales
        // solo porque ConvertirALetras en BudgetPricingService tiene implementación
        // diferente. Si se unifican, eliminar también estos.

        private static string ConvertirEnteroALetras(long numero)
        {
            if (numero == 0) return "CERO";
            if (numero == 1) return "UN";

            string resultado = string.Empty;
            if (numero >= 1_000_000_000_000)
            {
                long billones = numero / 1_000_000_000_000;
                resultado += ConvertirGrupo((int)billones) + " ";
                resultado += billones == 1 ? "BILLÓN " : "BILLONES ";
                numero %= 1_000_000_000_000;
            }
            if (numero >= 1_000_000)
            {
                long millones = numero / 1_000_000;
                resultado += ConvertirGrupo((int)millones) + " ";
                resultado += millones == 1 ? "MILLÓN " : "MILLONES ";
                numero %= 1_000_000;
            }
            if (numero >= 1_000)
            {
                int miles = (int)(numero / 1_000);
                resultado += miles == 1 ? "MIL " : ConvertirGrupo(miles) + " MIL ";
                numero %= 1_000;
            }
            if (numero > 0)
                resultado += ConvertirGrupo((int)numero);

            return resultado.Trim();
        }

        private static string ConvertirGrupo(int numero)
        {
            string[] unidades    = { "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE" };
            string[] decenas     = { "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE" };
            string[] decenasBase = { "", "", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA" };
            string[] centenas    = { "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS" };

            string resultado = string.Empty;
            int centenasNum  = numero / 100;
            int resto        = numero % 100;

            if (centenasNum > 0)
            {
                if (numero == 100) return "CIEN";
                resultado += centenas[centenasNum] + " ";
            }
            if (resto >= 10 && resto < 20)
            {
                resultado += decenas[resto - 10];
            }
            else
            {
                int decenasNum  = resto / 10;
                int unidadesNum = resto % 10;
                if (decenasNum > 0)
                {
                    if (decenasNum == 2 && unidadesNum > 0)
                        resultado += "VEINTI" + unidades[unidadesNum];
                    else
                    {
                        resultado += decenasBase[decenasNum];
                        if (unidadesNum > 0) resultado += " Y " + unidades[unidadesNum];
                    }
                }
                else if (unidadesNum > 0)
                {
                    resultado += unidades[unidadesNum];
                }
            }
            return resultado.Trim();
        }
    }
}
