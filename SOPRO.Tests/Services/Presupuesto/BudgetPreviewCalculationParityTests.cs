using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Presupuesto;

[TestClass]
public class BudgetPreviewCalculationParityTests
{
    // ── N5-3: el servicio migró de MotorCalculoSopro a SoproCalculationEngine.
    //    Paridad exacta contra referencias compuestas con las primitivas de la
    //    FACHADA (oráculo diferencial), baterías deterministas con semilla fija,
    //    y el caso de CostoDirectoReferencia no nulo sin conceptos.

    [TestMethod]
    public void BuildPreview_ParidadConFachada_BateriaAleatoriaConSemilla()
    {
        var rnd = new Random(20260817);

        for (int iter = 0; iter < 300; iter++)
        {
            using var context = TestDbFactory.CreateContext();

            var proyecto = CrearProyecto(rnd.Next(0, 5), rnd.Next(0, 5), rnd.Next(0, 7));
            context.Proyectos.Add(proyecto);
            context.SaveChanges();

            var matriz = new Matriz
            {
                ProyectoId = proyecto.Id,
                Clave = $"APU-{iter}",
                Descripcion = string.Empty,
                Unidad = "pza",
                Tipo = TipoMatriz.APU,
                Notas = string.Empty
            };
            context.Matrices.Add(matriz);
            context.SaveChanges();

            int nConceptos = rnd.Next(1, 7);
            var conceptos = new List<ConceptoPresupuesto>();
            for (int i = 0; i < nConceptos; i++)
            {
                var concepto = new ConceptoPresupuesto
                {
                    ProyectoId = proyecto.Id,
                    Clave = $"C-{iter}-{i}",
                    Descripcion = string.Empty,
                    Unidad = "pza",
                    Cantidad = ValorCantidad(rnd),
                    MatrizId = matriz.Id,
                    CostoDirectoUnitario = ValorPrecio(rnd),
                    CostoDirectoTotal = 0m,
                    PrecioUnitario = 0m,
                    ImporteTotal = 0m,
                    Nivel = 1,
                    Orden = i + 1,
                    EsAgrupador = false,
                    ColumnasPersonalizadasJSON = string.Empty,
                    Notas = string.Empty
                };
                conceptos.Add(concepto);
                context.ConceptosPresupuesto.Add(concepto);
            }
            context.SaveChanges();

            var input = CrearInputAleatorio(rnd);
            var servicio = BudgetPreviewCalculationService.BuildPreview(context, proyecto, input);
            var referencia = BuildPreviewReferenciaConFachada(conceptos, proyecto, input);

            CompararResultados(servicio, referencia, $"iter={iter}");
        }
    }

    [TestMethod]
    public void BuildPreviewFromReferenceCost_ParidadConFachada_BateriaAleatoriaConSemilla()
    {
        var rnd = new Random(20260817);

        for (int iter = 0; iter < 800; iter++)
        {
            var cd = ValorPrecio(rnd);
            var input = CrearInputAleatorio(rnd);
            var proyecto = rnd.Next(0, 10) < 3
                ? null
                : CrearProyecto(rnd.Next(0, 5), rnd.Next(0, 5), rnd.Next(0, 7));

            var servicio = BudgetPreviewCalculationService.BuildPreviewFromReferenceCost(cd, input, proyecto);
            var referencia = BuildPreviewReferenciaConFachada(cd, input, proyecto);

            CompararResultados(servicio, referencia, $"iter={iter}");
        }
    }

    [TestMethod]
    public void BuildPreview_SinConceptos_CostoDirectoReferenciaNoNulo_DebeUsarReferenciaComoBase()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(2, 2, 4);
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var input = new BudgetPercentageInput
        {
            CostoDirectoReferencia = 1234.567m,
            IndirectosCentral = 5m,
            IndirectosCampo = 5m,
            Financiamiento = 2m,
            Utilidad = 8m,
            CargosAdicionales = 3m,
            ModoCalculoPorcentajes = "Acumulables"
        };

        var result = BudgetPreviewCalculationService.BuildPreview(context, proyecto, input);

        // Referencia con 3 decimales redondeada a 2: CD 1234.57 → OC 61.73 → Campo 61.73
        // → sub1 1358.03 → Fin 27.16 → sub2 1385.19 → Util 110.82 → sub3 1496.01 → Cargos 44.88 → PU 1540.89
        Assert.IsFalse(result.CalculadoDesdeConceptos);
        Assert.AreEqual(0, result.ConceptosProcesados);
        Assert.AreEqual(1234.57m, result.CostoDirecto);
        Assert.AreEqual(61.73m, result.MontoIndirectosCentral);
        Assert.AreEqual(61.73m, result.MontoIndirectosCampo);
        Assert.AreEqual(1358.03m, result.Subtotal1);
        Assert.AreEqual(27.16m, result.MontoFinanciamiento);
        Assert.AreEqual(1385.19m, result.Subtotal2);
        Assert.AreEqual(110.82m, result.MontoUtilidad);
        Assert.AreEqual(1496.01m, result.Subtotal3);
        Assert.AreEqual(44.88m, result.MontoCargosAdicionales);
        Assert.AreEqual(1540.89m, result.PrecioUnitarioFinal);

        Assert.AreEqual(
            BudgetPreviewCalculationService.BuildPreviewFromReferenceCost(1234.567m, input, proyecto).PrecioUnitarioFinal,
            result.PrecioUnitarioFinal,
            "El delegado directo de referencia debe coincidir con el resultado del preview.");
    }

    // ── Referencias compuestas con la fachada (mismo algoritmo, primitivas legacy) ──

    private static BudgetPercentagePreviewResult BuildPreviewReferenciaConFachada(
        IList<ConceptoPresupuesto> conceptos, Proyecto proyecto, BudgetPercentageInput input)
    {
        var motor = new MotorCalculoSopro(proyecto);

        decimal totalCd     = 0m;
        decimal totalOc     = 0m;
        decimal totalCampo  = 0m;
        decimal totalSub1   = 0m;
        decimal totalFin    = 0m;
        decimal totalSub2   = 0m;
        decimal totalUtil   = 0m;
        decimal totalSub3   = 0m;
        decimal totalCargos = 0m;
        decimal totalFinal  = 0m;

        foreach (var concepto in conceptos)
        {
            decimal cdUnit   = motor.RedondearImporte(concepto.CostoDirectoUnitario);
            decimal cantidad = concepto.Cantidad;

            var desglose = motor.CalcularPrecioUnitario(cdUnit, input);

            totalCd     += motor.Multiplicar(cantidad, cdUnit);
            totalOc     += motor.Multiplicar(cantidad, desglose.IndirectosCentral);
            totalCampo  += motor.Multiplicar(cantidad, desglose.IndirectosCampo);
            totalSub1   += motor.Multiplicar(cantidad, desglose.Subtotal1);
            totalFin    += motor.Multiplicar(cantidad, desglose.Financiamiento);
            totalSub2   += motor.Multiplicar(cantidad, desglose.Subtotal2);
            totalUtil   += motor.Multiplicar(cantidad, desglose.Utilidad);
            totalSub3   += motor.Multiplicar(cantidad, desglose.Subtotal3);
            totalCargos += motor.Multiplicar(cantidad, desglose.CargosAdicionales);
            totalFinal  += motor.Multiplicar(cantidad, desglose.PrecioUnitario);
        }

        return new BudgetPercentagePreviewResult
        {
            CostoDirecto           = totalCd,
            MontoIndirectosCentral = totalOc,
            MontoIndirectosCampo   = totalCampo,
            Subtotal1              = totalSub1,
            MontoFinanciamiento    = totalFin,
            Subtotal2              = totalSub2,
            MontoUtilidad          = totalUtil,
            Subtotal3              = totalSub3,
            MontoCargosAdicionales = totalCargos,
            PrecioUnitarioFinal    = totalFinal,
            CalculadoDesdeConceptos = true,
            ConceptosProcesados    = conceptos.Count
        };
    }

    private static BudgetPercentagePreviewResult BuildPreviewReferenciaConFachada(
        decimal costoDirecto, BudgetPercentageInput input, Proyecto? proyecto)
    {
        var motor = proyecto != null ? new MotorCalculoSopro(proyecto) : null;

        decimal R(decimal v) => motor != null ? motor.RedondearImporte(v) : v;

        decimal mOC, mCampo, sub1, mFin, sub2, mUtil, sub3, mCarg, puFinal;
        decimal cd = R(costoDirecto);

        bool sobreCD = string.Equals(input.ModoCalculoPorcentajes, "SobreCD",
                                     StringComparison.OrdinalIgnoreCase);
        if (!sobreCD)
        {
            mOC    = R(cd * (input.IndirectosCentral / 100m));
            mCampo = R(cd * (input.IndirectosCampo   / 100m));
            sub1   = R(cd + mOC + mCampo);
            mFin   = R(sub1 * (input.Financiamiento / 100m));
            sub2   = R(sub1 + mFin);
            mUtil  = R(sub2 * (input.Utilidad / 100m));
            sub3   = R(sub2 + mUtil);
            mCarg  = R(sub3 * (input.CargosAdicionales / 100m));
            puFinal = R(sub3 + mCarg);
        }
        else
        {
            mOC    = R(cd * (input.IndirectosCentral  / 100m));
            mCampo = R(cd * (input.IndirectosCampo    / 100m));
            mFin   = R(cd * (input.Financiamiento     / 100m));
            mUtil  = R(cd * (input.Utilidad           / 100m));
            mCarg  = R(cd * (input.CargosAdicionales  / 100m));
            sub1   = R(cd + mOC + mCampo);
            sub2   = R(sub1 + mFin);
            sub3   = R(sub2 + mUtil);
            puFinal = R(sub3 + mCarg);
        }

        return new BudgetPercentagePreviewResult
        {
            CostoDirecto           = cd,
            MontoIndirectosCentral = mOC,
            MontoIndirectosCampo   = mCampo,
            Subtotal1              = sub1,
            MontoFinanciamiento    = mFin,
            Subtotal2              = sub2,
            MontoUtilidad          = mUtil,
            Subtotal3              = sub3,
            MontoCargosAdicionales = mCarg,
            PrecioUnitarioFinal    = puFinal,
            CalculadoDesdeConceptos = false,
            ConceptosProcesados    = 0
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void CompararResultados(BudgetPercentagePreviewResult servicio, BudgetPercentagePreviewResult referencia, string tag)
    {
        Assert.AreEqual(referencia.CostoDirecto, servicio.CostoDirecto, $"{tag} CostoDirecto");
        Assert.AreEqual(referencia.MontoIndirectosCentral, servicio.MontoIndirectosCentral, $"{tag} MontoIndirectosCentral");
        Assert.AreEqual(referencia.MontoIndirectosCampo, servicio.MontoIndirectosCampo, $"{tag} MontoIndirectosCampo");
        Assert.AreEqual(referencia.Subtotal1, servicio.Subtotal1, $"{tag} Subtotal1");
        Assert.AreEqual(referencia.MontoFinanciamiento, servicio.MontoFinanciamiento, $"{tag} MontoFinanciamiento");
        Assert.AreEqual(referencia.Subtotal2, servicio.Subtotal2, $"{tag} Subtotal2");
        Assert.AreEqual(referencia.MontoUtilidad, servicio.MontoUtilidad, $"{tag} MontoUtilidad");
        Assert.AreEqual(referencia.Subtotal3, servicio.Subtotal3, $"{tag} Subtotal3");
        Assert.AreEqual(referencia.MontoCargosAdicionales, servicio.MontoCargosAdicionales, $"{tag} MontoCargosAdicionales");
        Assert.AreEqual(referencia.PrecioUnitarioFinal, servicio.PrecioUnitarioFinal, $"{tag} PrecioUnitarioFinal");
        Assert.AreEqual(referencia.CalculadoDesdeConceptos, servicio.CalculadoDesdeConceptos, $"{tag} CalculadoDesdeConceptos");
        Assert.AreEqual(referencia.ConceptosProcesados, servicio.ConceptosProcesados, $"{tag} ConceptosProcesados");
    }

    private static BudgetPercentageInput CrearInputAleatorio(Random rnd) => new()
    {
        CostoDirectoReferencia = rnd.Next(0, 10) < 4 ? ValorPrecio(rnd) : 0m,
        IndirectosCentral = rnd.Next(0, 1500) / 100m,
        IndirectosCampo = rnd.Next(0, 1500) / 100m,
        Financiamiento = rnd.Next(0, 500) / 100m,
        Utilidad = rnd.Next(0, 2000) / 100m,
        CargosAdicionales = rnd.Next(0, 500) / 100m,
        ModoCalculoPorcentajes = rnd.Next(0, 4) switch
        {
            0 => "Acumulables",
            1 => "SobreCD",
            2 => "sobrecd",
            _ => null!
        }
    };

    private static Proyecto CrearProyecto(int decQty, int decAmt, int decPct) => new()
    {
        Nombre = "Proyecto preview paridad",
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
        FechaInicio = new DateTime(2026, 1, 1),
        FechaTermino = new DateTime(2026, 1, 31),
        PlazoEjecucion = 31,
        DecimalesCantidad = decQty,
        DecimalesImporte = decAmt,
        DecimalesPorcentaje = decPct
    };

    private static decimal ValorPrecio(Random rnd)
        => rnd.Next(1, 1_000_000) / 1000m;

    private static decimal ValorCantidad(Random rnd)
        => rnd.Next(1, 100_000) / 1000m;
}