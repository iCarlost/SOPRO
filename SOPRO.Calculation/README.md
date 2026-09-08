# Sopro.Calculation

Nucleo de calculo de SOPRO desacoplado del dominio y la UI. Reproduce con exactitud
la aritmetica del motor legacy (`MotorCalculoSopro`, `SOPRO.Application`) con
"precision de pantalla": lo que el usuario ve en pantalla es exactamente lo que se
procesa.

- **Cero dependencias runtime** (solo BCL).
- **Cero tipos SOPRO en la API publica** (nada de `SOPRO.Core`, `SOPRO.Application`,
  EF ni SQLite).
- **Distribucion:** paquete privado de preview, version `0.1.0`.
- Superficie publica minima (hallazgo 2): `CalculationPrecision`,
  `SoproCalculationEngine`, `DirectCostLine`, `PricePercentageInput`,
  `PriceBreakdown` y el enum `PercentageCalculationMode`. El slice de matrices
   agrega `MatrixGraphInput`, `MatrixNodeInput`, `MatrixComponentInput`,
   `MatrixGraphCalculator` y sus resultados inmutables. El slice de calendario
   agrega `WorkingCalendar`, `CalendarException`, `CalendarExceptionKind` y
   `WorkingCalendarCalculator` para operaciones deterministas de días hábiles,
   excepciones y fechas inclusivas/exclusivas. El slice de costo horario
  agrega `HourlyCostInput`, `HourlyCostBreakdown` y `HourlyCostCalculator`. El slice FSR
  agrega `RealSalaryFactorInput`, `RealSalaryFactorBreakdown`, `WorkShiftType` y
  `RealSalaryFactorCalculator`. Los ayudantes (`UnitPriceCalculator`,
  `AmountDistributor`) son internos.
- Invariante del grafo de matrices: los IDs de componente son unicos en todo el
  grafo (incluidos nodos no alcanzados desde la raiz); los IDs temporales de
  componentes sin guardar los asigna el adaptador de Application. Tipos de
  matriz/componente no definidos y combinaciones invalidas (auxiliar o
  material/maquinaria como `%MO`) se rechazan con excepcion.
- Hojas precalculadas: un `MatrixNodeInput` sin componentes puede traer un total de
  costo directo (`precomputedDirectCostTotal`, solo Basic o Crew) para consumir el
  costo almacenado de una auxiliar sin expandir su subarbol; el adaptador de
  Application las usa para preservar el contrato del calculador canonico. Se consume
  tal cual, incluidos costos negativos (como hacia el canonico). Los IDs negativos
  quedan reservados a las hojas sinteticas del adaptador; la raiz debe ser no negativa.

## API publica

Toda la API esta en ingles (hallazgo del dictamen N1): los metodos de
`SoproCalculationEngine` son `RoundQuantity/RoundAmount/RoundPercentage`,
`Multiply`, `SumQuantities/SumAmounts/SumDirectCost`,
`DistributeQuantity/DistributeAmount`, `CalculateUnitPrice` y
`CalculateAmountOverBase`; `Decimales...` de `CalculationPrecision` son
`QuantityDecimals/AmountDecimals/PercentageDecimals`.

## Contratos de mapeo (Fase N1, PLAN-01 §10)

| Dominio legacy | Paquete |
|---|---|
| `Proyecto` (DecimalesCantidad/Importe/Porcentaje) | `CalculationPrecision` (`QuantityDecimals`/`AmountDecimals`/`PercentageDecimals`) |
| `BudgetPercentageInput` | `PricePercentageInput` |
| `Proyecto.ModoCalculoPorcentajes` (`"SobreCD"`) | `PercentageCalculationMode` (mapeo: fachada N2; `"SobreCD"` casing-insensitive → `OverDirectCost`, resto → `Accumulative`) |
| `ConceptoPresupuesto` | `DirectCostLine` (`Quantity`/`UnitDirectCost`/`IsGrouping`/`HasMatrix`) |
| `DesglosePrecios` | `PriceBreakdown` |
| `MotorCalculoSopro` | `SoproCalculationEngine` |

`DirectCostLine.HasMatrix` debe mapear exactamente `ConceptoPresupuesto.MatrizId.HasValue`
durante compatibilidad: no la navegacion cargada (`Matriz != null`) ni un Id mayor que cero.

## Uso

```csharp
using Sopro.Calculation;

var motor = new SoproCalculationEngine(new CalculationPrecision(quantityDecimals: 2,
                                                                amountDecimals: 2,
                                                                percentageDecimals: 4));

decimal importe = motor.Multiply(652m, 13.3875m);            // 8730.28

var desglose = motor.CalculateUnitPrice(1000m, new PricePercentageInput
{
    CentralIndirectsPercentage = 5m,
    FieldIndirectsPercentage = 5m,
    FinancingPercentage = 6m,
    ProfitPercentage = 8m,
    AdditionalChargesPercentage = 3m,
    Mode = PercentageCalculationMode.Accumulative,
});

IReadOnlyList<decimal> partes = motor.DistributeAmount(1000m, new[] { 33m, 33m, 34m });

var calendario = new Sopro.Calculation.Calendar.WorkingCalendar
{
    Monday = true,
    Tuesday = true,
    Wednesday = true,
    Thursday = true,
    Friday = true,
    Exceptions = new[]
    {
        new Sopro.Calculation.Calendar.CalendarException
        {
            Date = new DateTime(2026, 1, 1),
            Kind = Sopro.Calculation.Calendar.CalendarExceptionKind.NonWorking
        }
    }
};

DateTime fin = Sopro.Calculation.Calendar.WorkingCalendarCalculator.CalculateFinishDate(
    calendario, new DateTime(2026, 1, 5), 5)!.Value;
```

## Semantica preservada (decisiones N0, `N0-TABLA-DECISIONES-DIVERGENCIAS.md`)

- Decimales negativos se normalizan a cero (fila 7).
- `Multiply` redondea el P.U. pero no la cantidad (fila 2).
- Indirectos central + campo se suman antes del redondeo monetario (fila 3).
- `PriceBreakdown.CentralIndirectCosts` prorratea con precision fija de 6 decimales
  y `AwayFromZero`; `FieldIndirectCosts = IndirectCosts - CentralIndirectCosts` (fila 4).
- Distribuciones: residuo en el ultimo periodo, que puede quedar negativo (filas 1, 5);
  pesos negativos aceptados (fila 10); nulo/vacio devuelve vacio y suma de pesos cero
  devuelve ceros (fila 11).
- Modo: solo `"SobreCD"` (casing-insensitive) es SobreCD; `null`/desconocidos son
  Acumulables (fila 6).
- Sumas: coleccion nula devuelve cero; cada elemento se redondea antes de acumular
  (filas 12, 14).
- El formato de cultura NO vive aqui: queda en la fachada legacy (fila 9).
- El reloj no participa en ninguna aritmetica (fila 0 / manifiesto §4).
- `WorkingCalendarCalculator` recibe todos sus datos, copia defensivamente las
  excepciones y rechaza calendarios sin ningún día laborable. Los extremos de fecha
  se procesan sin incrementar más allá de `DateTime.MaxValue`/`MinValue`.
- El costo horario usa `HourlyCostCalculator` con entradas y resultados escalares
  inmutables; la fecha de calculo y la persistencia quedan fuera del motor.
- `AverageValue` y `RealSalary` se conservan con divisores no positivos para la
  presentacion legacy. El overflow se propaga en todos los casos.
- FSR usa entradas y desglose escalares inmutables en
  `Sopro.Calculation.Labor.RealSalaryFactorCalculator`; no parsea JSON, no aplica
  defaults y no formatea valores. `Semester` es 1-based; los adaptadores legacy deben
  convertir el indice persistido/UI con `+1`.
- Una jornada fuera de `WorkShiftType` conserva el fallback legacy nocturno. El motor
  puro propaga division entre cero y overflow; el adaptador de Application conserva
  por separado su contrato `salario <= 0`/error -> `null`.
- Los topes anuales, comparaciones estrictas, tasas para salario ajustado `<= 1` y
  valores negativos de horas se conservan sin validacion nueva. El desglose expone
  tambien `MedicalBenefitsInKindContribution` como la suma legacy `AE + AF` usada por
  AE-2(C), aunque su etiqueta historica sea anomala.

## Verificacion (Gate N1)

- `dotnet test SOPRO.sln` — suite completa (incluye diferenciales legacy vs nuevo).
- Cobertura del nucleo: 95% lineas / 90% ramas (medida con coverlet XPlat).
- Consumidor fuera de la solucion: `dotnet pack` a un feed local + app de consola
  externa que restaura desde ese feed.

## Empaquetado privado (Gate N6)

Desde la raiz del repositorio:

```powershell
dotnet restore SOPRO.Calculation/SOPRO.Calculation.csproj
dotnet build SOPRO.Calculation/SOPRO.Calculation.csproj -c Release --no-restore
dotnet pack SOPRO.Calculation/SOPRO.Calculation.csproj -c Release --no-build --output .artifacts/packages
```

El paquete contiene este README, el changelog, la licencia MIT declarada en sus
metadatos, documentacion XML y simbolos portables. La publicacion en feeds publicos
queda fuera de alcance de N6.
