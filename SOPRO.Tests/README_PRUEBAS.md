# SOPRO.Tests

Proyecto inicial de pruebas unitarias para SOPRO.

## Objetivo

Proteger la lógica de cálculo sin tocar la interfaz WinForms.

Este proyecto debe probar principalmente:

- `SOPRO.Application.Services.MotorCalculoSopro`
- `SOPRO.Application.Services.MatrixComponentCalculationService`
- Servicios de explosión de insumos
- Servicios de programación / curva S
- Servicios de financiamiento

## Regla de arquitectura

`SOPRO.Tests` referencia:

- `SOPRO.Core`
- `SOPRO.Application`

No debe depender de `SOPRO.WinForms` salvo que en el futuro se haga una prueba muy específica de integración visual, lo cual no es recomendable para esta etapa.

## Cómo ejecutar

Desde Visual Studio:

1. Abrir `SOPRO.sln`.
2. Menú `Prueba`.
3. Abrir `Explorador de pruebas`.
4. Clic en `Ejecutar todas las pruebas`.

Desde consola:

```bash
dotnet test
```

## Regla práctica para liberar versión

Antes de generar instalador:

1. Compilar solución completa.
2. Ejecutar todas las pruebas.
3. Si una prueba falla, no liberar hasta revisar si cambió una regla de cálculo o si la prueba debe ajustarse por una decisión consciente.

## Segunda batería agregada

Se agregaron pruebas de integración ligera con SQLite temporal para validar servicios que dependen de datos persistidos, sin tocar WinForms:

- `ProgramacionCalculationServiceTests`
  - Valida calendario laboral lunes-viernes.
  - Valida cálculo de fecha final y días hábiles inclusivos.

- `ProgramacionCurvaSServiceTests`
  - Valida acumulados financieros de Curva S.
  - Valida cantidades acumuladas.
  - Valida que importes, cantidades y porcentajes respeten la precisión configurada del proyecto.

- `FinanciamientoCalculationServiceTests`
  - Valida que los egresos del flujo sean CD + CI.
  - Valida que se agregue el período adicional por desfase de cobro.
  - Valida saldos acumulados negativos e interés financiero resultante.

Estas pruebas usan `TestDbFactory`, que crea una base SQLite temporal por prueba y la elimina/recrea en cada ejecución.

## Tercera batería agregada

Se agregaron pruebas de reconciliación completas para proteger el flujo de dinero de SOPRO:

- `ExplosionInsumosServiceTests`
  - Valida que la explosión de insumos cuadre contra el Costo Directo del presupuesto.
  - Valida que `%MO` se trate como porcentaje sobre mano de obra, no como insumo normal.

- `ProgramacionInsumosServiceTests`
  - Valida la distribución de materiales por periodo.
  - Valida el comportamiento de `%MO` dentro del programa de insumos.

- `ReconciliacionCostoDirectoTests`
  - Valida que Presupuesto, Explosión de Insumos y Programa de Insumos lleguen al mismo Costo Directo.

Estas pruebas usan una base SQLite temporal y datos reales de entidades SOPRO, sin tocar WinForms.

## Cuarta batería agregada

Se agregaron pruebas para proteger:

- `BudgetPreviewCalculationService`: cálculo acumulable y cálculo `SobreCD`.
- Vista previa de presupuesto basada en conceptos reales del escenario de prueba.
- `UtilidadCalculationService`: modo directo y modo asistido, incluyendo ISR/PTU.
- `FsrCalculationService`: validación de entradas inválidas y cálculo base del FSR.

También se corrigió la expectativa de la prueba de `%MO` en explosión: el precio unitario inferido para `%MO` corresponde al importe base de mano de obra acumulada, no al importe final del insumo porcentual.

## Quinta batería de pruebas

Se agregaron pruebas de blindaje para:

- APU con básico y cuadrilla dentro de la misma matriz.
- Base de mano de obra formada por MO normal + cuadrillas.
- `%MO` calculado sobre la base completa de mano de obra.
- Herramienta `%MO` separada del total de mano de obra, pero considerada en el resumen.
- Casos extremos de precisión de pantalla con 2 y 4 decimales.
- Distribución de cantidades con ajuste de residuo.
- Suma de importes visibles antes de totalizar.

## Sexta batería agregada

Incluye pruebas de blindaje para `MatrixCostAdjustmentService`:

- cálculo de base ajustable y costo mínimo;
- reajuste por monto objetivo solo en materiales;
- reajuste por monto objetivo solo en mano de obra;
- rechazo de objetivos menores al mínimo alcanzable;
- rechazo de factores negativos sin modificar cantidades.

## Suite de validación oficial SOPRO

Además de las pruebas unitarias por servicio, se agregó una primera suite de regresión oficial en:

- `Services/Regresion/SuiteValidacionOficialSoproTests.cs`
- `Services/Invariantes/SoproCalculationInvariantTests.cs`
- `Services/EdgeCases/MatrixCalculationEdgeCaseTests.cs`

### Objetivo

Estas pruebas congelan un caso base oficial de SOPRO y validan que el mismo costo directo cierre en:

1. Presupuesto
2. Explosión de insumos
3. Programa de insumos
4. Curva S

La idea es que, cuando SOPRO crezca, estas pruebas funcionen como alarma antes de liberar una versión.

### Cómo usar esta suite

Antes de generar instalador:

1. Compilar la solución.
2. Abrir `Prueba > Explorador de pruebas`.
3. Ejecutar todas las pruebas.
4. Si una prueba falla, no liberar hasta revisar si:
   - cambió correctamente la regla de negocio, o
   - se rompió una regla existente.

### Siguiente evolución recomendada

Cuando se tenga un escenario estable de SOPRO, se puede crear un caso de regresión con sus totales:

- Costo directo total
- Total de materiales
- Total de mano de obra
- Total de herramienta
- Total de maquinaria
- Total de básicos
- Curva S final
- Financiamiento final

Ese caso debe quedar congelado como prueba de regresión institucional.

## Suite de regresión con proyecto sintético

Se agregó el archivo de prueba:

`Services/RegresionSintetica/ProyectoSinteticoRegressionTests.cs`

Este bloque usa una copia del proyecto sintético (100% sintético, sin datos reales):

`TestData/proyecto-sintetico-vial-demo.db`

No modifica el archivo original; cada prueba crea una copia temporal. La finalidad es congelar resultados del escenario representativo para detectar regresiones antes de liberar versiones nuevas.

Valores congelados principales:

- Costo directo terminal: 4,454,847.92
- Importe total terminal: 5,457,326.28
- Conceptos terminales: 6
- Matrices: 7
- Componentes de matriz: 11
- Periodos del programa: 12
- Distribuciones del programa: 55
