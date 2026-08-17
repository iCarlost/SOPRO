using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services;

[TestClass]
public class MatrixComponentCalculationParityTests
{
    // ── N5-2: el servicio migró de MotorCalculoSopro a SoproCalculationEngine.
    //    Estos tests prueban paridad exacta contra una referencia compuesta con
    //    las primitivas de la FACHADA (oráculo diferencial del paquete) y fijan
    //    valores dorados adicionales fuera de la configuración estándar (2/2/4).

    [TestMethod]
    public void Recalculate_ParidadConFachada_BateriaAleatoriaConSemilla()
    {
        var rnd = new Random(20260817);

        for (int decimales = 0; decimales <= 4; decimales++)
        {
            for (int iter = 0; iter < 200; iter++)
            {
                var entidades = CrearEntidadesAleatorias(rnd);

                var servicio = entidades.Select(ClonarComponente).ToList();
                var referencia = entidades.Select(ClonarComponente).ToList();

                var totalsServicio = MatrixComponentCalculationService.Recalculate(servicio, decimales);
                var totalsReferencia = RecalcularConFachada(referencia, decimales);

                for (int i = 0; i < entidades.Count; i++)
                {
                    Assert.AreEqual(referencia[i].Importe, servicio[i].Importe,
                        $"dec={decimales} iter={iter} componente={i}");
                }

                Assert.AreEqual(totalsReferencia.TotalMaterial, totalsServicio.TotalMaterial, $"dec={decimales} iter={iter} TotalMaterial");
                Assert.AreEqual(totalsReferencia.BaseManoObra, totalsServicio.BaseManoObra, $"dec={decimales} iter={iter} BaseManoObra");
                Assert.AreEqual(totalsReferencia.TotalManoObra, totalsServicio.TotalManoObra, $"dec={decimales} iter={iter} TotalManoObra");
                Assert.AreEqual(totalsReferencia.TotalMaquinaria, totalsServicio.TotalMaquinaria, $"dec={decimales} iter={iter} TotalMaquinaria");
                Assert.AreEqual(totalsReferencia.TotalBasicos, totalsServicio.TotalBasicos, $"dec={decimales} iter={iter} TotalBasicos");
                Assert.AreEqual(totalsReferencia.TotalHerramientas, totalsServicio.TotalHerramientas, $"dec={decimales} iter={iter} TotalHerramientas");
                Assert.AreEqual(totalsReferencia.TotalManoObraResumen, totalsServicio.TotalManoObraResumen, $"dec={decimales} iter={iter} TotalManoObraResumen");
                Assert.AreEqual(totalsReferencia.CostoDirectoTotal, totalsServicio.CostoDirectoTotal, $"dec={decimales} iter={iter} CostoDirectoTotal");
            }
        }
    }

    [TestMethod]
    public void Recalculate_DecimalesCero_MaterialYPorcentajeMO_Dorado()
    {
        var material = new Material { Clave = "MAT-1", PrecioUnitario = 13.3875m };
        var oficial = new ManoDeObra { Clave = "MO-1", Unidad = "jor", SalarioReal = 500m };
        var caboOficio = new ManoDeObra { Clave = "MO-2", Unidad = "%MO" };

        var componentes = new List<ComponenteMatriz>
        {
            new() { TipoComponente = TipoComponenteMatriz.Material, Material = material, Cantidad = 652m },
            new() { TipoComponente = TipoComponenteMatriz.ManoDeObra, ManoDeObra = oficial, Cantidad = 2m },
            new() { TipoComponente = TipoComponenteMatriz.ManoDeObra, ManoDeObra = caboOficio, Cantidad = 0.10m }
        };

        var totales = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte: 0);

        // P.U. visible 13 (sin decimales); 652 × 13 = 8476.
        Assert.AreEqual(8476m, componentes[0].Importe);
        // MO: 2 × 500 = 1000; %MO: R0(0.10 × 1000) = 100.
        Assert.AreEqual(1000m, componentes[1].Importe);
        Assert.AreEqual(100m, componentes[2].Importe);
        Assert.AreEqual(1000m, totales.BaseManoObra);
        Assert.AreEqual(1100m, totales.TotalManoObra);
        Assert.AreEqual(9576m, totales.CostoDirectoTotal);
    }

    [TestMethod]
    public void Recalculate_DecimalesTres_MaterialYHerramientaPorcentajeMO_Dorado()
    {
        var material = new Material { Clave = "MAT-1", PrecioUnitario = 13.3875m };
        var oficial = new ManoDeObra { Clave = "MO-1", Unidad = "jor", SalarioReal = 500m };
        var herramientaMenor = new Herramienta { Clave = "HER-1", Unidad = "%MO" };

        var componentes = new List<ComponenteMatriz>
        {
            new() { TipoComponente = TipoComponenteMatriz.Material, Material = material, Cantidad = 652m },
            new() { TipoComponente = TipoComponenteMatriz.ManoDeObra, ManoDeObra = oficial, Cantidad = 2m },
            new() { TipoComponente = TipoComponenteMatriz.Herramienta, Herramienta = herramientaMenor, Cantidad = 0.03m }
        };

        var totales = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte: 3);

        // P.U. visible 13.388; 652 × 13.388 = 8728.976.
        Assert.AreEqual(8728.976m, componentes[0].Importe);
        // MO: 2 × 500 = 1000; herramienta: 0.03 × 1000 = 30.
        Assert.AreEqual(1000m, componentes[1].Importe);
        Assert.AreEqual(30m, componentes[2].Importe);
        Assert.AreEqual(1000m, totales.BaseManoObra);
        Assert.AreEqual(30m, totales.TotalHerramientas);
        Assert.AreEqual(9758.976m, totales.CostoDirectoTotal);
    }

    // ── Referencia compuesta con la fachada (mismo algoritmo, primitivas legacy) ──

    private static MatrixComponentTotals RecalcularConFachada(IList<ComponenteMatriz> componentes, int decimalesImporte)
    {
        var motor = new MotorCalculoSopro(decimalesImporte, decimalesImporte, 4);

        foreach (var comp in componentes)
        {
            switch (comp.TipoComponente)
            {
                case TipoComponenteMatriz.Material:
                    if (comp.Material != null)
                        comp.Importe = motor.Multiplicar(comp.Cantidad, comp.Material.PrecioUnitario);
                    break;

                case TipoComponenteMatriz.Maquinaria:
                    if (comp.Maquinaria != null)
                        comp.Importe = motor.Multiplicar(comp.Cantidad, comp.Maquinaria.CostoHorario);
                    break;

                case TipoComponenteMatriz.Auxiliar:
                    if (comp.Auxiliar != null)
                        comp.Importe = motor.Multiplicar(comp.Cantidad, comp.Auxiliar.CostoDirecto);
                    break;
            }
        }

        var baseManoObra = 0m;
        foreach (var comp in componentes)
        {
            if (comp.TipoComponente == TipoComponenteMatriz.ManoDeObra && comp.ManoDeObra != null)
            {
                if (!comp.ManoDeObra.EsPorcentajeMO)
                {
                    comp.Importe = motor.Multiplicar(comp.Cantidad, comp.ManoDeObra.SalarioReal);
                    baseManoObra = motor.RedondearImporte(baseManoObra + comp.Importe);
                }
            }
            else if (comp.TipoComponente == TipoComponenteMatriz.Auxiliar && comp.Auxiliar?.Tipo == TipoMatriz.Cuadrilla)
            {
                baseManoObra = motor.RedondearImporte(baseManoObra + comp.Importe);
            }
        }

        foreach (var comp in componentes)
        {
            if (comp.TipoComponente == TipoComponenteMatriz.ManoDeObra && comp.ManoDeObra != null)
            {
                if (comp.ManoDeObra.EsPorcentajeMO)
                    comp.Importe = motor.CalcularImporteSobreBase(comp.Cantidad, baseManoObra);
            }
            else if (comp.TipoComponente == TipoComponenteMatriz.Herramienta && comp.Herramienta != null)
            {
                comp.Importe = comp.Herramienta.EsPorcentajeMO
                    ? motor.CalcularImporteSobreBase(comp.Cantidad, baseManoObra)
                    : motor.Multiplicar(comp.Cantidad, comp.Herramienta.PrecioUnitario);
            }
        }

        var totalHerramientaPorcentajeMo = motor.SumarImportes(
            componentes
                .Where(c => c.TipoComponente == TipoComponenteMatriz.Herramienta && c.Herramienta?.EsPorcentajeMO == true)
                .Select(c => c.Importe));

        var totals = new MatrixComponentTotals
        {
            TotalMaterial = motor.SumarImportes(componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Material).Select(c => c.Importe)),
            BaseManoObra = motor.RedondearImporte(baseManoObra),
            TotalManoObra = motor.SumarImportes(
                componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.ManoDeObra ||
                                       (c.TipoComponente == TipoComponenteMatriz.Auxiliar && c.Auxiliar?.Tipo == TipoMatriz.Cuadrilla))
                           .Select(c => c.Importe)),
            TotalMaquinaria = motor.SumarImportes(componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria).Select(c => c.Importe)),
            TotalBasicos = motor.SumarImportes(
                componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar && c.Auxiliar?.Tipo != TipoMatriz.Cuadrilla)
                           .Select(c => c.Importe)),
            TotalHerramientas = motor.SumarImportes(componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Herramienta).Select(c => c.Importe)),
            TotalManoObraResumen = 0
        };

        totals.TotalManoObraResumen = motor.RedondearImporte(totals.TotalManoObra + totalHerramientaPorcentajeMo);
        totals.CostoDirectoTotal = motor.RedondearImporte(
            totals.TotalMaterial + totals.TotalManoObra + totals.TotalMaquinaria + totals.TotalBasicos + totals.TotalHerramientas);
        return totals;
    }

    // ── Generador determinista ─────────────────────────────────────────────────

    private static List<ComponenteMatriz> CrearEntidadesAleatorias(Random rnd)
    {
        var componentes = new List<ComponenteMatriz>();

        int nMateriales = rnd.Next(0, 4);
        for (int i = 0; i < nMateriales; i++)
        {
            componentes.Add(new ComponenteMatriz
            {
                TipoComponente = TipoComponenteMatriz.Material,
                Material = new Material { Clave = $"MAT-{rnd.Next(1000)}", PrecioUnitario = ValorPrecio(rnd) },
                Cantidad = ValorCantidad(rnd)
            });
        }

        int nMaquinaria = rnd.Next(0, 3);
        for (int i = 0; i < nMaquinaria; i++)
        {
            componentes.Add(new ComponenteMatriz
            {
                TipoComponente = TipoComponenteMatriz.Maquinaria,
                Maquinaria = new Maquinaria { Clave = $"MAQ-{rnd.Next(1000)}", CostoHorario = ValorPrecio(rnd) },
                Cantidad = ValorCantidad(rnd)
            });
        }

        int nAuxiliares = rnd.Next(0, 3);
        for (int i = 0; i < nAuxiliares; i++)
        {
            var esCuadrilla = rnd.Next(0, 2) == 0;
            componentes.Add(new ComponenteMatriz
            {
                TipoComponente = TipoComponenteMatriz.Auxiliar,
                Auxiliar = new Matriz
                {
                    Clave = $"AUX-{rnd.Next(1000)}",
                    Tipo = esCuadrilla ? TipoMatriz.Cuadrilla : TipoMatriz.Basico,
                    CostoDirecto = ValorPrecio(rnd)
                },
                Cantidad = ValorCantidad(rnd)
            });
        }

        int nManoObra = rnd.Next(1, 4);
        for (int i = 0; i < nManoObra; i++)
        {
            var esPorcentaje = rnd.Next(0, 2) == 0;
            componentes.Add(new ComponenteMatriz
            {
                TipoComponente = TipoComponenteMatriz.ManoDeObra,
                ManoDeObra = new ManoDeObra
                {
                    Clave = $"MO-{rnd.Next(1000)}",
                    Unidad = esPorcentaje ? "%MO" : "jor",
                    SalarioReal = esPorcentaje ? 0m : ValorPrecio(rnd)
                },
                Cantidad = esPorcentaje ? ValorPorcentaje(rnd) : ValorCantidad(rnd)
            });
        }

        int nHerramientas = rnd.Next(0, 3);
        for (int i = 0; i < nHerramientas; i++)
        {
            var esPorcentaje = rnd.Next(0, 2) == 0;
            componentes.Add(new ComponenteMatriz
            {
                TipoComponente = TipoComponenteMatriz.Herramienta,
                Herramienta = new Herramienta
                {
                    Clave = $"HER-{rnd.Next(1000)}",
                    Unidad = esPorcentaje ? "%MO" : "pza",
                    PrecioUnitario = esPorcentaje ? 0m : ValorPrecio(rnd)
                },
                Cantidad = esPorcentaje ? ValorPorcentaje(rnd) : ValorCantidad(rnd)
            });
        }

        return componentes;
    }

    private static ComponenteMatriz ClonarComponente(ComponenteMatriz original) => new()
    {
        TipoComponente = original.TipoComponente,
        Material = original.Material,
        Maquinaria = original.Maquinaria,
        Auxiliar = original.Auxiliar,
        ManoDeObra = original.ManoDeObra,
        Herramienta = original.Herramienta,
        Cantidad = original.Cantidad
    };

    private static decimal ValorPrecio(Random rnd)
        => rnd.Next(1, 100_000) / 1000m;

    private static decimal ValorCantidad(Random rnd)
        => rnd.Next(1, 100_000) / 1000m;

    private static decimal ValorPorcentaje(Random rnd)
        => rnd.Next(5, 750) / 1000m;
}