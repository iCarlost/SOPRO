# Sopro.Calculation

Nucleo de calculo de SOPRO desacoplado del dominio y la UI. Reproduce con exactitud
la aritmetica del motor legacy (`MotorCalculoSopro`, `SOPRO.Application`) con
"precision de pantalla": lo que el usuario ve en pantalla es exactamente lo que se
procesa.

- **Cero dependencias runtime** (solo BCL).
- **Cero tipos SOPRO en la API publica** (nada de `SOPRO.Core`, `SOPRO.Application`,
  EF ni SQLite).
- Superficie publica minima: solo las 8 clases del arbol.

## Contratos de mapeo (Fase N1, PLAN-01 §10)

| Dominio legacy | Paquete |
|---|---|
| `Proyecto` (DecimalesCantidad/Importe/Porcentaje) | `CalculationPrecision` |
| `BudgetPercentageInput` | `PricePercentageInput` |
| `Proyecto.ModoCalculoPorcentajes` (`"SobreCD"`) | `PercentageCalculationMode` (+ `PercentageCalculationModes.Parse`) |
| `ConceptoPresupuesto` | `DirectCostLine` |
| `DesglosePrecios` | `PriceBreakdown` |
| `MotorCalculoSopro` | `SoproCalculationEngine`, `UnitPriceCalculator`, `AmountDistributor` |

`DirectCostLine.HasMatrix` debe mapear exactamente `ConceptoPresupuesto.MatrizId.HasValue`
durante compatibilidad: no la navegacion cargada (`Matriz != null`) ni un Id mayor que cero.

## Uso

```csharp
using Sopro.Calculation;

var motor = new SoproCalculationEngine(new CalculationPrecision(decimalesCantidad: 2,
                                                                decimalesImporte: 2,
                                                                decimalesPorcentaje: 4));

decimal importe = motor.Multiplicar(652m, 13.3875m);            // 8730.28

var desglose = motor.CalcularPrecioUnitario(1000m, new PricePercentageInput
{
    IndirectosCentral = 5m,
    IndirectosCampo = 5m,
    Financiamiento = 6m,
    Utilidad = 8m,
    CargosAdicionales = 3m,
    ModoCalculoPorcentajes = PercentageCalculationModes.Parse(proyecto.ModoCalculoPorcentajes),
});

IReadOnlyList<decimal> partes = motor.DistribuirImporte(1000m, new[] { 33m, 33m, 34m });
```

## Semantica preservada (decisiones N0, `N0-TABLA-DECISIONES-DIVERGENCIAS.md`)

- Decimales negativos se normalizan a cero (fila 7).
- `Multiplicar` redondea el P.U. pero no la cantidad (fila 2).
- Indirectos central + campo se suman antes del redondeo monetario (fila 3).
- `PriceBreakdown.IndirectosCentral` prorratea con precision fija de 6 decimales
  y `AwayFromZero`; `IndirectosCampo = Indirectos - IndirectosCentral` (fila 4).
- Distribuciones: residuo en el ultimo periodo, que puede quedar negativo (filas 1, 5);
  pesos negativos aceptados (fila 10); nulo/vacio devuelve vacio y suma de pesos cero
  devuelve ceros (fila 11).
- Modo: solo `"SobreCD"` (casing-insensitive) es SobreCD; `null`/desconocidos son
  Acumulables (fila 6).
- Sumas: coleccion nula devuelve cero; cada elemento se redondea antes de acumular
  (filas 12, 14).
- El formato de cultura NO vive aqui: queda en la fachada legacy (fila 9).
- El reloj no participa en ninguna aritmetica (fila 0 / manifiesto §4).

## Verificacion (Gate N1)

- `dotnet test SOPRO.sln` — suite completa (incluye diferenciales legacy vs nuevo).
- Cobertura del nucleo: 95% lineas / 90% ramas (medida con coverlet XPlat).
- Consumidor fuera de la solucion: `dotnet pack` a un feed local + app de consola
  externa que restaura desde ese feed.
