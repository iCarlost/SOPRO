using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Programacion;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.RegresionSintetica;

[TestClass]
public class ProyectoSinteticoRegressionTests
{
    private static decimal Round2(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    [TestMethod]
    public void ProyectoSintetico_DebeConservarConfiguracionYTotalesBase()
    {
        using var context = ProyectoSinteticoFixture.OpenReadOnlyCopy();

        var proyecto = context.Proyectos.AsNoTracking().Single();
        var conceptos = context.ConceptosPresupuesto.AsNoTracking().ToList();
        var terminales = conceptos.Where(c => !c.EsAgrupador).ToList();

        Assert.AreEqual("PROYECTO SINTETICO VIAL DEMO", proyecto.Nombre);
        Assert.AreEqual("Acumulables", proyecto.ModoCalculoPorcentajes);
        Assert.AreEqual(4, proyecto.DecimalesCantidad);
        Assert.AreEqual(2, proyecto.DecimalesImporte);
        Assert.AreEqual(4, proyecto.DecimalesPorcentaje);

        Assert.AreEqual(11, conceptos.Count);
        Assert.AreEqual(6, terminales.Count);
        Assert.AreEqual(7, context.Matrices.AsNoTracking().Count());
        Assert.AreEqual(11, context.ComponentesMatriz.AsNoTracking().Count());

        Assert.AreEqual(4454847.92m, terminales.Sum(c => c.CostoDirectoTotal));
        Assert.AreEqual(5457326.28m, terminales.Sum(c => c.ImporteTotal));
        Assert.AreEqual(4454847.92m, terminales.Sum(c => Round2(c.Cantidad * c.CostoDirectoUnitario)));
        Assert.AreEqual(5457326.28m, terminales.Sum(c => Round2(c.Cantidad * c.PrecioUnitario)));
    }

    [TestMethod]
    public void ProyectoSintetico_DebeConservarTotalesPorCapitulo()
    {
        using var context = ProyectoSinteticoFixture.OpenReadOnlyCopy();

        var capitulos = context.ConceptosPresupuesto
            .AsNoTracking()
            .Where(c => c.EsAgrupador && c.Nivel == 1)
            .OrderBy(c => c.Orden)
            .ToList();

        Assert.AreEqual(4, capitulos.Count);

        Assert.AreEqual("CAPITULO PRELIMINARES", capitulos[0].Descripcion);
        Assert.AreEqual(281983.52m, capitulos[0].ImporteTotal);

        Assert.AreEqual("CAPITULO LIMPIEZA DE TERRENO", capitulos[1].Descripcion);
        Assert.AreEqual(1001979.87m, capitulos[1].ImporteTotal);

        Assert.AreEqual("CAPITULO ACARREOS", capitulos[2].Descripcion);
        Assert.AreEqual(3716486.89m, capitulos[2].ImporteTotal);

        Assert.AreEqual("CAPITULO LIMPIEZA", capitulos[3].Descripcion);
        Assert.AreEqual(456876.00m, capitulos[3].ImporteTotal);

        Assert.AreEqual(5457326.28m, capitulos.Sum(c => c.ImporteTotal));
    }

    [TestMethod]
    public void ProyectoSintetico_ExplosionDebeReconciliarContraCostoDirectoDelPresupuesto()
    {
        using var context = ProyectoSinteticoFixture.OpenReadOnlyCopy();
        var proyecto = context.Proyectos.AsNoTracking().Single();

        var result = new ExplosionInsumosService().Calculate(context, proyecto.Id, "Todos");

        Assert.IsTrue(result.TieneConceptos);
        Assert.AreEqual(4454847.92m, result.CostoDirectoPresupuesto);
        Assert.AreEqual(4454847.92m, result.CostoDirectoTotal);
        Assert.AreEqual(result.CostoDirectoPresupuesto, result.CostoDirectoTotal);

        Assert.IsTrue(result.Materiales.Count > 0);
        Assert.IsTrue(result.Maquinaria.Count > 0);
        Assert.IsTrue(result.ManoObra.Count > 0);
        Assert.IsTrue(result.Herramientas.Count > 0);
    }

    [TestMethod]
    public void ProyectoSintetico_ProgramaObraDebeConservarDistribucionOficial()
    {
        using var context = ProyectoSinteticoFixture.OpenReadOnlyCopy();

        var programa = context.ProgramasObra.AsNoTracking().Single(p => p.Activo);
        var actividades = context.ActividadesProgramadas
            .AsNoTracking()
            .Where(a => a.ProgramaObraId == programa.Id)
            .ToList();
        var terminales = actividades.Where(a => !a.EsResumen).ToList();
        var distribuciones = context.DistribucionesPeriodo
            .AsNoTracking()
            .ToList();

        Assert.AreEqual("Programa Base", programa.Nombre);
        Assert.AreEqual(12, context.PeriodosPrograma.AsNoTracking().Count(p => p.ProgramaObraId == programa.Id));
        Assert.AreEqual(11, actividades.Count);
        Assert.AreEqual(6, terminales.Count);
        Assert.AreEqual(55, distribuciones.Count);

        Assert.AreEqual(5457326.28m, terminales.Sum(a => a.ImporteTotal));
        Assert.AreEqual(5457326.28m, distribuciones.Sum(d => d.ImporteProgramado));
        Assert.AreEqual(49102.53m, distribuciones.Sum(d => d.CantidadProgramada));
        Assert.AreEqual(600.00m, distribuciones.Sum(d => d.PorcentajeProgramado));
    }

    [TestMethod]
    public void ProyectoSintetico_ProgramaInsumosDebeReconciliarContraCostoDirecto()
    {
        using var context = ProyectoSinteticoFixture.OpenReadOnlyCopy();
        var proyecto = context.Proyectos.AsNoTracking().Single();
        var service = new ProgramacionInsumosService();

        var materiales = service.Build(context, proyecto, ProgramaInsumoTipo.Materiales);
        var manoObra = service.Build(context, proyecto, ProgramaInsumoTipo.ManoDeObra);
        var maquinaria = service.Build(context, proyecto, ProgramaInsumoTipo.Maquinaria);
        var herramienta = service.Build(context, proyecto, ProgramaInsumoTipo.Herramienta);

        var totalInsumos = materiales.Rows.Sum(r => r.ImporteTotal)
            + manoObra.Rows.Sum(r => r.ImporteTotal)
            + maquinaria.Rows.Sum(r => r.ImporteTotal)
            + herramienta.Rows.Sum(r => r.ImporteTotal);

        Assert.AreEqual(12, materiales.Periodos.Count);
        Assert.AreEqual("Programa Base", materiales.NombrePrograma);
        Assert.AreEqual(4454847.92m, totalInsumos);
    }

    [TestMethod]
    public void ProyectoSintetico_FinanciamientoDebeConservarFlujoCongelado()
    {
        using var context = ProyectoSinteticoFixture.OpenReadOnlyCopy();

        var config = context.ConfiguracionesFinanciamiento.AsNoTracking().Single();
        var filas = context.FilasFlujoCajaFinanciamiento
            .AsNoTracking()
            .OrderBy(f => f.NumeroPeriodo)
            .ToList();

        Assert.AreEqual(30.0m, config.PorcentajeAnticipo);
        Assert.AreEqual(13, filas.Count);

        Assert.AreEqual(1637197.87m, filas[0].AnticipoRecibido);
        Assert.AreEqual(4890977.49m, filas.Sum(f => f.Egresos));
        Assert.AreEqual(5457326.28m, filas.Sum(f => f.EstimacionCobrada));
        Assert.AreEqual(0.00m, filas.Sum(f => f.InteresPeriodo));
        Assert.AreEqual(89674.34m, filas.Min(f => f.SaldoAcumulado));
    }

    [TestMethod]
    public void ProyectoSintetico_CopiaInmutable_DebeRechazarEscrituras()
    {
        using var context = ProyectoSinteticoFixture.OpenReadOnlyCopy();

        var config = context.ConfiguracionesFinanciamiento.Single();
        config.PorcentajeAnticipo = 50m;

        try
        {
            context.SaveChanges();
            Assert.Fail("Se esperaba que la copia de solo lectura rechazara la escritura.");
        }
        catch (Exception ex)
        {
            Assert.IsTrue(
                ex.GetBaseException().Message.Contains("readonly", StringComparison.OrdinalIgnoreCase)
                || ex.GetBaseException().Message.Contains("read-only", StringComparison.OrdinalIgnoreCase),
                $"Se esperaba error de base de datos de solo lectura, pero fue: {ex.GetBaseException().Message}");
        }
    }

    [TestMethod]
    public void ProyectoSintetico_SnapshotSemanticoNormalizado_DebeMantenerseCongelado()
    {
        using var context = ProyectoSinteticoFixture.OpenReadOnlyCopy();

        var actual = BuildSemanticLines(context);

        const string esperado =
@"PR|PROYECTO SINTETICO VIAL DEMO|Acumulables|4|2|4|6.0|3.79|0.3|8.0|3.0|16.0|1.6543
C||OBRA: REPAVIMENTACION SINTETICA DEMO||0|||0.0|0.0|0.0|5457326.28|5457326.28|True|0
C||CAPITULO PRELIMINARES||1|||0.0|0.0|0.0|281983.52|281983.52|True|1
C||CAPITULO LIMPIEZA DE TERRENO||4|||0.0|0.0|0.0|1001979.87|1001979.87|True|1
C||CAPITULO ACARREOS||6|||0.0|0.0|0.0|3716486.89|3716486.89|True|1
C||CAPITULO LIMPIEZA||9|||0.0|0.0|0.0|456876.00|456876.00|True|1
C|1|CONCEPTO SINTETICO C-1||2|SERV|M-1|1.0|168523.88|206436.44|168523.88|206436.44|False|5
C|2|CONCEPTO SINTETICO C-2||3|SERV|M-2|2.0|30836.33|37773.54|61672.66|75547.08|False|5
C|3|CONCEPTO SINTETICO C-3||5|M3|M-3|2660.1|307.51|376.67|818007.35|1001979.87|False|5
C|4|CONCEPTO SINTETICO C-4||7|M3-KM|M-4|3458.13|317.31|388.71|1097299.23|1344209.71|False|5
C|5|CONCEPTO SINTETICO C-5||8|M3-KM|M-5|34581.3|56.0|68.6|1936552.80|2372277.18|False|5
C|6|CONCEPTO SINTETICO C-6||10|M2|M-6|8400.0|44.38|54.39|372792.00|456876.00|False|5
M|CU001.|MATRIZ SINTETICA CU-1|6220.2|Cuadrilla
M|M-1|MATRIZ SINTETICA M-1|168523.88|APU
M|M-2|MATRIZ SINTETICA M-2|30836.33|APU
M|M-3|MATRIZ SINTETICA M-3|307.51|APU
M|M-4|MATRIZ SINTETICA M-4|317.31|APU
M|M-5|MATRIZ SINTETICA M-5|56.0|APU
M|M-6|MATRIZ SINTETICA M-6|44.38|APU
X|CU001.|1|ManoDeObra|MO:MO002|1.0|0.0|5504.59
X|CU001.|2|ManoDeObra|MO:MO001|0.1|0.0|550.48
X|CU001.|3|Herramienta|HER:HERR|0.03|0.0|165.13
X|M-1|1|Material|MAT:CM-1|1.0|0.0|84048.09
X|M-1|2|Maquinaria|MAQ:RETRO235|5.47884|0.18252|84475.79
X|M-2|1|Maquinaria|MAQ:RETRO235|1.99995|0.50001|30836.33
X|M-3|1|Maquinaria|MAQ:RETRO416|0.05863|17.05611|307.51
X|M-4|0|Maquinaria|MAQ:CARGADOR|0.02326|42.99226|205.31
X|M-4|1|Material|MAT:VARIO001|1.0|0.0|112.0
X|M-5|0|Material|MAT:VARIO002|1.0|0.0|56.0
X|M-6|1|Auxiliar|AUX:CU001.|0.00714|0.0|44.38
IM|CM-1|MATERIAL SINTETICO CM-1|84048.09
IM|VARIO001|MATERIAL SINTETICO VARIO001|112.0
IM|VARIO002|MATERIAL SINTETICO VARIO002|56.0
IMO|MO001|MANO DE OBRA SINTETICA MO001|0.0|True
IMO|MO002|MANO DE OBRA SINTETICA MO002|5504.600535|False
IMAQ|CARGADOR|MAQUINARIA SINTETICA CARGADOR|8825.74
IMAQ|RETRO235|MAQUINARIA SINTETICA RETRO235|15418.55
IMAQ|RETRO416|MAQUINARIA SINTETICA RETRO416|5244.96
IHER|HERR|HERRAMIENTA SINTETICA HERR|0.0|True
A|1|1.0|206436.44|206436.44|206436.44
A|2|2.0|37773.54|75547.08|75547.08
A|3|2660.1|376.67|1001979.87|1001979.87
A|4|3458.13|388.71|1344209.71|1344209.71
A|5|34581.3|68.6|2372277.18|2372277.18
A|6|8400.0|54.39|456876.00|456876.00
P|1|Quincena 01 - 16/04 a 30/04|2026-04-16|2026-04-30
P|2|Quincena 02 - 01/05 a 15/05|2026-05-01|2026-05-15
P|3|Quincena 03 - 16/05 a 31/05|2026-05-16|2026-05-31
P|4|Quincena 04 - 01/06 a 15/06|2026-06-01|2026-06-15
P|5|Quincena 05 - 16/06 a 30/06|2026-06-16|2026-06-30
P|6|Quincena 06 - 01/07 a 15/07|2026-07-01|2026-07-15
P|7|Quincena 07 - 16/07 a 31/07|2026-07-16|2026-07-31
P|8|Quincena 08 - 01/08 a 15/08|2026-08-01|2026-08-15
P|9|Quincena 09 - 16/08 a 31/08|2026-08-16|2026-08-31
P|10|Quincena 10 - 01/09 a 15/09|2026-09-01|2026-09-15
P|11|Quincena 11 - 16/09 a 30/09|2026-09-16|2026-09-30
P|12|Quincena 12 - 01/10 a 15/10|2026-10-01|2026-10-15
D|1|1|0.0083|0.83|1713.42
D|1|2|0.0917|9.17|18930.22
D|1|3|0.0833|8.33|17196.16
D|1|4|0.0917|9.17|18930.22
D|1|5|0.0917|9.17|18930.22
D|1|6|0.0917|9.17|18930.22
D|1|7|0.1|10.0|20643.64
D|1|8|0.0833|8.33|17196.16
D|1|9|0.0917|9.17|18930.22
D|1|10|0.0917|9.17|18930.22
D|1|11|0.0917|9.17|18930.22
D|1|12|0.0832|8.32|17175.52
D|2|1|0.0167|0.835|630.82
D|2|2|0.1833|9.165|6923.89
D|2|3|0.1667|8.335|6296.85
D|2|4|0.1833|9.165|6923.89
D|2|5|0.1833|9.165|6923.89
D|2|6|0.1833|9.165|6923.89
D|2|7|0.2|10.0|7554.71
D|2|8|0.1667|8.335|6296.85
D|2|9|0.1833|9.165|6923.89
D|2|10|0.1833|9.165|6923.89
D|2|11|0.1833|9.165|6923.89
D|2|12|0.1668|8.34|6300.62
D|3|1|44.335|1.6667|16700.00
D|3|2|487.685|18.3333|183695.98
D|3|3|443.35|16.6667|166996.98
D|3|4|487.685|18.3333|183695.98
D|3|5|487.685|18.3333|183695.98
D|3|6|487.685|18.3333|183695.98
D|3|7|221.675|8.3334|83498.97
D|4|7|403.4485|11.6667|156824.91
D|4|8|576.355|16.6667|224035.40
D|4|9|633.9905|18.3333|246438.00
D|4|10|633.9905|18.3333|246438.00
D|4|11|633.9905|18.3333|246438.00
D|4|12|576.355|16.6667|224035.40
D|5|7|4034.485|11.6667|276766.46
D|5|8|5763.55|16.6667|395380.32
D|5|9|6339.905|18.3333|434916.69
D|5|10|6339.905|18.3333|434916.69
D|5|11|6339.905|18.3333|434916.69
D|5|12|5763.55|16.6667|395380.33
D|6|1|70.0|0.8333|3807.15
D|6|2|770.0|9.1667|41880.45
D|6|3|700.0|8.3333|38072.85
D|6|4|770.0|9.1667|41880.45
D|6|5|770.0|9.1667|41880.45
D|6|6|770.0|9.1667|41880.45
D|6|7|840.0|10.0|45687.60
D|6|8|700.0|8.3333|38072.85
D|6|9|770.0|9.1667|41880.45
D|6|10|770.0|9.1667|41880.45
D|6|11|770.0|9.1667|41880.45
D|6|12|700.0|8.3332|38072.40";

        var esperadoNormalizado = esperado.Replace("\r\n", "\n").Split('\n');
        var actualNormalizado = actual.ToArray();

        Assert.AreEqual(esperadoNormalizado.Length, actualNormalizado.Length,
            "La cardinalidad del snapshot semántico cambió. Revisar si se agregaron/eliminaron conceptos, matrices, componentes, insumos, actividades, periodos o distribuciones.");

        for (var i = 0; i < esperadoNormalizado.Length; i++)
        {
            Assert.AreEqual(esperadoNormalizado[i], actualNormalizado[i],
                $"Fila {i} del snapshot semántico normalizado difiere.");
        }
    }

    [TestMethod]
    public void ProyectoSintetico_SnapshotSemantico_DetectaMutacionDePrecioEnInsumo()
    {
        // Prueba de sensibilidad del snapshot: si un valor económico muta, la firma
        // semántica debe cambiar. Esto garantiza que el snapshot no es un falso
        // verde que ignore cambios de datos.
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_sintetico_mutation_{Guid.NewGuid():N}.db");
        try
        {
            var sourcePath = Path.Combine(AppContext.BaseDirectory, "TestData", "proyecto-sintetico-vial-demo.db");
            File.Copy(sourcePath, dbPath, overwrite: true);
            File.SetAttributes(dbPath, FileAttributes.Normal);

            List<string> firmaOriginal;
            using (var context = new SOPROContext(dbPath))
                firmaOriginal = BuildSemanticLines(context);

            using (var context = new SOPROContext(dbPath))
            {
                // Mutación IM: precio del insumo (84048.09 → 84049.22).
                var material = context.Materiales.Single(m => m.Clave == "CM-1");
                material.PrecioUnitario = 84048.09m + 1.13m;

                // Mutación X: importe persistido del componente (el valor almacenado
                // cambia sin recalcular; la firma X debe reflejarlo igualmente).
                var componente = context.ComponentesMatriz.Single(c => c.Matriz.Clave == "M-1" && c.Orden == 1);
                componente.Importe = 84049.22m;
                context.SaveChanges();
            }

            List<string> firmaMutada;
            using (var context = new SOPROContext(dbPath))
                firmaMutada = BuildSemanticLines(context);

            var lineaIMMutada = firmaMutada.Single(l => l.StartsWith("IM|CM-1|"));
            var lineaXComponente = firmaMutada.Single(l => l.StartsWith("X|M-1|1|Material|MAT:CM-1|"));

            Assert.AreNotEqual(string.Join("\n", firmaOriginal), string.Join("\n", firmaMutada),
                "Las mutaciones deben alterar la firma semántica.");
            Assert.IsTrue(lineaIMMutada.Contains("84049.22"),
                $"La mutación debe reflejarse en la sección IM: {lineaIMMutada}");
            Assert.IsTrue(lineaXComponente.Contains("84049.22"),
                $"La mutación debe reflejarse en la sección X (importe del componente persistido): {lineaXComponente}");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    private static void DeleteQuietly(string dbPath)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (var intento = 0; intento < 3; intento++)
        {
            try
            {
                if (File.Exists(dbPath))
                {
                    File.SetAttributes(dbPath, FileAttributes.Normal);
                    File.Delete(dbPath);
                }
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(150);
            }
        }
    }

    private static List<string> BuildSemanticLines(SOPROContext context)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var actual = new List<string>();

        // Sección PR: proyecto (modo de cálculo, precisiones, porcentajes y FSR).
        var proyecto = context.Proyectos.AsNoTracking().Single();
        actual.Add(string.Join("|",
            "PR", proyecto.Nombre ?? "", proyecto.ModoCalculoPorcentajes ?? "",
            proyecto.DecimalesCantidad, proyecto.DecimalesImporte, proyecto.DecimalesPorcentaje,
            proyecto.PorcentajeIndirectosCentral.ToString(inv), proyecto.PorcentajeIndirectosCampo.ToString(inv),
            proyecto.PorcentajeFinanciamiento.ToString(inv), proyecto.PorcentajeUtilidad.ToString(inv),
            proyecto.PorcentajeCargosAdicionales.ToString(inv), proyecto.PorcentajeIVA.ToString(inv),
            proyecto.FactorSalarioReal.ToString(inv)));

        // Sección C: conceptos con jerarquía completa (padre canónico, Orden, Unidad,
        // matriz asignada) y economía. Una reparentación o renumeración que mantenga
        // los totales YA NO puede seguir verde: estos campos se serializan.
        var conceptos = context.ConceptosPresupuesto
            .AsNoTracking()
            .Include(c => c.Padre)
            .Include(c => c.Matriz)
            .OrderBy(c => c.Nivel).ThenBy(c => c.Orden).ThenBy(c => c.Clave)
            .ToList();
        foreach (var c in conceptos)
        {
            var padreCanonico = c.Padre == null ? ""
                : string.IsNullOrWhiteSpace(c.Padre.Clave)
                    ? $"D:{c.Padre.Descripcion}"   // agrupadores sin clave → descripción canónica
                    : $"K:{c.Padre.Clave}";
            actual.Add(string.Join("|",
                "C", c.Clave ?? "", c.Descripcion ?? "", padreCanonico, c.Orden, c.Unidad ?? "",
                c.Matriz?.Clave ?? "",
                c.Cantidad.ToString(inv), c.CostoDirectoUnitario.ToString(inv),
                c.PrecioUnitario.ToString(inv), c.CostoDirectoTotal.ToString(inv),
                c.ImporteTotal.ToString(inv), c.EsAgrupador, c.Nivel));
        }

        // Sección M: matrices (APUs, básicos y cuadrillas) con su C.D. congelado.
        var matrices = context.Matrices
            .AsNoTracking()
            .OrderBy(m => m.Clave)
            .ToList();
        foreach (var m in matrices)
        {
            actual.Add(string.Join("|",
                "M", m.Clave ?? "", m.Descripcion ?? "",
                m.CostoDirecto.ToString(inv), m.Tipo.ToString()));
        }

        // Sección X: componentes de matriz (tipo, insumo referenciado, cantidad,
        // rendimiento e importe individual congelado).
        var componentes = context.ComponentesMatriz
            .AsNoTracking()
            .Include(c => c.Matriz)
            .Include(c => c.Material)
            .Include(c => c.ManoDeObra)
            .Include(c => c.Maquinaria)
            .Include(c => c.Herramienta)
            .Include(c => c.Auxiliar)
            .OrderBy(c => c.Matriz.Clave).ThenBy(c => c.Orden)
            .ToList();
        foreach (var c in componentes)
        {
            var referencia = c.TipoComponente switch
            {
                SOPRO.Core.Entities.TipoComponenteMatriz.Material => $"MAT:{c.Material?.Clave}",
                SOPRO.Core.Entities.TipoComponenteMatriz.ManoDeObra => $"MO:{c.ManoDeObra?.Clave}",
                SOPRO.Core.Entities.TipoComponenteMatriz.Maquinaria => $"MAQ:{c.Maquinaria?.Clave}",
                SOPRO.Core.Entities.TipoComponenteMatriz.Herramienta => $"HER:{c.Herramienta?.Clave}",
                SOPRO.Core.Entities.TipoComponenteMatriz.Auxiliar => $"AUX:{c.Auxiliar?.Clave}",
                _ => "?"
            };
            actual.Add(string.Join("|",
                "X", c.Matriz?.Clave ?? "", c.Orden, c.TipoComponente, referencia,
                c.Cantidad.ToString(inv), c.Rendimiento.ToString(inv), c.Importe.ToString(inv)));
        }

        // Sección IM/IMO/IMAQ/IHER: insumos base con sus valores económicos.
        foreach (var m in context.Materiales.AsNoTracking().OrderBy(x => x.Clave).ToList())
            actual.Add(string.Join("|", "IM", m.Clave ?? "", m.Descripcion ?? "", m.PrecioUnitario.ToString(inv)));

        foreach (var m in context.ManoDeObra.AsNoTracking().OrderBy(x => x.Clave).ToList())
            actual.Add(string.Join("|", "IMO", m.Clave ?? "", m.Descripcion ?? "", m.SalarioReal.ToString(inv), m.EsPorcentajeMO));

        foreach (var m in context.Maquinaria.AsNoTracking().OrderBy(x => x.Clave).ToList())
            actual.Add(string.Join("|", "IMAQ", m.Clave ?? "", m.Descripcion ?? "", m.CostoHorario.ToString(inv)));

        foreach (var h in context.Herramientas.AsNoTracking().OrderBy(x => x.Clave).ToList())
            actual.Add(string.Join("|", "IHER", h.Clave ?? "", h.Descripcion ?? "", h.PrecioUnitario.ToString(inv), h.EsPorcentajeMO));

        // Sección A: actividades terminales del programa (cantidad, P.U. e importes).
        foreach (var a in context.ActividadesProgramadas.AsNoTracking()
            .Where(a => !a.EsResumen).OrderBy(a => a.Orden).ToList())
        {
            actual.Add(string.Join("|", "A", a.Clave, a.CantidadTotal.ToString(inv),
                a.PrecioUnitario.ToString(inv), a.ImporteTotal.ToString(inv), a.ImporteProgramado.ToString(inv)));
        }

        // Sección P: periodos del programa (etiqueta y fechas).
        foreach (var p in context.PeriodosPrograma.AsNoTracking().OrderBy(p => p.NumeroPeriodo).ToList())
        {
            actual.Add(string.Join("|", "P", p.NumeroPeriodo, p.Etiqueta ?? "",
                p.FechaInicio.ToString("yyyy-MM-dd", inv), p.FechaFin.ToString("yyyy-MM-dd", inv)));
        }

        // Sección D: distribuciones por actividad/periodo (cantidad, % e importe).
        var distribuciones = context.DistribucionesPeriodo
            .AsNoTracking()
            .Include(d => d.ActividadProgramada)
            .Include(d => d.PeriodoPrograma)
            .OrderBy(d => d.ActividadProgramada.Orden).ThenBy(d => d.PeriodoPrograma.NumeroPeriodo)
            .ToList();
        foreach (var d in distribuciones)
        {
            actual.Add(string.Join("|", "D", d.ActividadProgramada?.Clave, d.PeriodoPrograma?.NumeroPeriodo,
                d.CantidadProgramada.ToString(inv), d.PorcentajeProgramado.ToString(inv), d.ImporteProgramado.ToString(inv)));
        }

        return actual;
    }
}
