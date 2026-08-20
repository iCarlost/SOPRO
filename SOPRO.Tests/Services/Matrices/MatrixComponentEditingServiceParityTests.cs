using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Matrices;

[TestClass]
public class MatrixComponentEditingServiceParityTests
{
    // ── N5-8: UpdateUnitPrice migró su única operación del motor de
    //    MotorCalculoSopro a SoproCalculationEngine.Multiply.
    //    Paridad exacta contra referencia compuesta con la fachada
    //    (oráculo diferencial) + dorados. SyncRendimiento conserva
    //    Math.Round(1/cantidad, 5) — no es operación del motor.

    [TestMethod]
    public void UpdateUnitPrice_ParidadConFachada_BateriaAleatoriaConSemilla()
    {
        var rnd = new Random(1123581321);

        for (int iter = 0; iter < 600; iter++)
        {
            int decimalesImporte = rnd.Next(0, 7);
            decimal cantidad = Valor(rnd);
            decimal precio = Valor(rnd);

            var componente = new ComponenteMatriz
            {
                TipoComponente = TipoComponenteMatriz.Material,
                Cantidad = cantidad,
                Material = new Material { Clave = $"MAT-{iter}", PrecioUnitario = 0m }
            };
            var componenteRef = new ComponenteMatriz
            {
                TipoComponente = TipoComponenteMatriz.Material,
                Cantidad = cantidad,
                Material = new Material { Clave = $"MAT-{iter}", PrecioUnitario = 0m }
            };

            string texto = iter % 10 == 0
                ? "$" + precio.ToString("N2") + " MXN"
                : precio.ToString();

            var resultado = MatrixComponentEditingService.UpdateUnitPrice(componente, texto, decimalesImporte);
            var resultadoRef = UpdateUnitPriceConFachada(componenteRef, texto, decimalesImporte);

            Assert.AreEqual(resultadoRef.Success, resultado.Success, $"iter={iter} Success");
            Assert.AreEqual(resultadoRef.ErrorMessage, resultado.ErrorMessage, $"iter={iter} ErrorMessage");
            Assert.AreEqual(resultadoRef.RequiresRecalculation, resultado.RequiresRecalculation, $"iter={iter} RequiresRecalculation");
            if (!resultadoRef.Success)
                continue;

            Assert.AreEqual(componenteRef.Importe, componente.Importe, $"iter={iter} Importe");
            Assert.AreEqual(componenteRef.Material!.PrecioUnitario, componente.Material!.PrecioUnitario, $"iter={iter} PrecioUnitario");
        }
    }

    [TestMethod]
    public void UpdateUnitPrice_NoMaterial_DebeFallarSinTocarImporte()
    {
        foreach (var tipo in new[]
        {
            TipoComponenteMatriz.ManoDeObra,
            TipoComponenteMatriz.Maquinaria,
            TipoComponenteMatriz.Auxiliar,
            TipoComponenteMatriz.Herramienta
        })
        {
            var componente = new ComponenteMatriz { TipoComponente = tipo, Cantidad = 10m, Importe = 99m };

            var resultado = MatrixComponentEditingService.UpdateUnitPrice(componente, "150", 2);

            Assert.IsFalse(resultado.Success, $"{tipo} debería fallar");
            Assert.AreEqual(99m, componente.Importe, $"{tipo} no debe tocar Importe");
        }
    }

    [TestMethod]
    public void UpdateUnitPrice_Dorado_RedondeaPrecioVisibleAntesDeMultiplicar()
    {
        var componente = new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            Cantidad = 13.3875m,
            Material = new Material { Clave = "MAT-1", PrecioUnitario = 0m }
        };

        // decimalesImporte 2: P.U. visible R2(124.8225) = 124.82;
        // importe R2(13.3875 × 124.82) = R2(1671.02775) = 1671.03
        var resultado = MatrixComponentEditingService.UpdateUnitPrice(componente, "124.8225", 2);

        Assert.IsTrue(resultado.Success);
        Assert.AreEqual(124.8225m, componente.Material!.PrecioUnitario, "El P.U. crudo se conserva en el material");
        Assert.AreEqual(1671.03m, componente.Importe);
    }

    [TestMethod]
    public void UpdateUnitPrice_Dorado_CeroDecimales()
    {
        var componente = new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            Cantidad = 10m,
            Material = new Material { Clave = "MAT-1", PrecioUnitario = 0m }
        };

        // decimalesImporte 0: R0(15.49) = 15; 10 × 15 = 150
        var resultado = MatrixComponentEditingService.UpdateUnitPrice(componente, "15.49", 0);

        Assert.IsTrue(resultado.Success);
        Assert.AreEqual(150m, componente.Importe);
    }

    [TestMethod]
    public void UpdateUnitPrice_TextoInvalidoONegativo_DebeFallar()
    {
        var componente = new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            Cantidad = 10m,
            Material = new Material { Clave = "MAT-1", PrecioUnitario = 0m }
        };

        Assert.IsFalse(MatrixComponentEditingService.UpdateUnitPrice(componente, "abc", 2).Success);
        Assert.IsFalse(MatrixComponentEditingService.UpdateUnitPrice(componente, "-5", 2).Success);
        Assert.IsFalse(MatrixComponentEditingService.UpdateUnitPrice(componente, string.Empty, 2).Success);
        Assert.AreEqual(0m, componente.Importe, "Ningún fallo debe tocar Importe");
    }

    [TestMethod]
    public void SyncRendimiento_Dorado_PrecisionFijaCincoConservada()
    {
        // No es operación del motor: precisión fija 5 (contrato de captura).
        var maquinaria = new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Maquinaria,
            Cantidad = 3m
        };
        MatrixComponentEditingService.SyncRendimiento(maquinaria);
        Assert.AreEqual(0.33333m, maquinaria.Rendimiento);

        var material = new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Material, Cantidad = 3m };
        MatrixComponentEditingService.SyncRendimiento(material);
        Assert.AreEqual(0m, material.Rendimiento);
    }

    // ── Referencia compuesta con la fachada (código pre-migración) ────────────

    private static MatrixComponentEditResult UpdateUnitPriceConFachada(
        ComponenteMatriz component, string rawValue, int decimalesImporte)
    {
        if (component.TipoComponente != TipoComponenteMatriz.Material || component.Material == null)
            return MatrixComponentEditResult.Fail("Solo los materiales permiten edición directa del precio unitario.");

        if (!TryParseDecimal(rawValue, out var unitPrice) || unitPrice < 0)
            return MatrixComponentEditResult.Fail("El precio unitario debe ser un número mayor o igual a cero.");

        component.Material.PrecioUnitario = unitPrice;
        var motor = new MotorCalculoSopro(decimalesImporte, decimalesImporte, 4);
        component.Importe = motor.Multiplicar(component.Cantidad, unitPrice);
        return MatrixComponentEditResult.Ok();
    }

    private static bool TryParseDecimal(string? rawValue, out decimal value)
    {
        var clean = (rawValue ?? string.Empty)
            .Replace("$", string.Empty)
            .Replace(",", string.Empty)
            .Trim();

        return decimal.TryParse(clean, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(clean, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.CurrentCulture, out value);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static decimal Valor(Random rnd)
        => rnd.Next(1, 1_000_000) / 1000m;
}