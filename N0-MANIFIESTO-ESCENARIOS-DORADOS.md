# N0: Manifiesto de Escenarios Dorados

**Fase:** N0 — Congelar comportamiento (PLAN-01-DESACOPLAMIENTO-NUCLEO-APLICACION.md, §9, acciones 2-4)
**Estado:** Gate N0 aprobado (10/10 pasadas del conjunto crítico); en integración como PR N0 independiente
**Objetivo:** congelar los escenarios de referencia que deben permanecer inmutables durante el desacoplamiento del motor.

---

## 1. Escenario sintético oficial: CasoOficial001

Construido por `SOPRO.Tests\TestInfrastructure\SoproCalculationScenarioBuilder.CreateBaseBudgetScenario` sobre una SQLite temporal (`TestDbFactory.CreateContext`).

| Concepto | Valor congelado | Evidencia |
|---|---|---|
| Matriz APU-001 "Concreto simple" | `CostoDirecto = 100.00` | `SuiteValidacionOficialSoproTests.CasoOficial001_PresupuestoBase...` |
| Concepto C-001 | Cantidad `10.00`, CD total `1,000.00` | Ídem |
| Explosión materiales | Cemento `600.00` | `CasoOficial001_Explosion...` |
| Explosión MO normal | Oficial `300.00` | Ídem |
| Explosión MO `%MO` | Cabo `30.00` | Ídem |
| Explosión herramienta `%MO` | `30.00` | Ídem |
| Explosión maquinaria | Revolvedora `40.00` | Ídem |
| Explosión CD total | `1,000.00` | Ídem |
| Programa de insumos | Suma importes = `1,000.00` | `CasoOficial001_ProgramaInsumos...` |
| Curva S último periodo | ImporteAcumulado `1,000.00`, CantidadAcumulada `10.00`, % `100.0000` | `CasoOficial001_CurvaS...` |

Composición del APU: material (1×$60) + oficial (1×$30) + cabo `%MO` (0.10×$30=$3) + herramienta `%MO` (0.10×$30=$3) + revolvedora (1×$4) = `100.00`.

---

## 2. Escenario real congelado: ***REMOVED***

Snapshot canónico: `SOPRO.Tests\TestData\***REMOVED***.db`, abierto en copia de solo lectura por `***REMOVED***.OpenReadOnlyCopy`.

| Concepto | Valor congelado | Evidencia |
|---|---|---|
| Configuración | Acumulables, Decimales 4/2/4 | `***REMOVED***_DebeConservarConfiguracionYTotalesBase` |
| Dimensiones | 11 conceptos, 6 terminales, 7 matrices, 11 componentes | Ídem |
| CD terminales | `***REMOVED***` | Ídem |
| Importe terminales | `***REMOVED***` | Ídem |
| Capítulo PRELIMINARES | `***REMOVED***` | `***REMOVED***_DebeConservarTotalesPorCapitulo` |
| Capítulo LIMPIEZA DE ***REMOVED*** | `***REMOVED***` | Ídem |
| Capítulo ACARREOS | `***REMOVED***` | Ídem |
| Capítulo LIMPIEZA | `***REMOVED***` | Ídem |
| Explosión | CD presupuesto = CD total = `***REMOVED***` | `***REMOVED***_ExplosionDebeReconciliar...` |
| Programa de obra | 12 periodos, 11 actividades, 6 terminales, 55 distribuciones; suma importe `***REMOVED***`, cantidad `49,102.53`, % `600.00` | `***REMOVED***_ProgramaObraDebeConservar...` |
| Programa de insumos | Suma importes = `***REMOVED***` | `***REMOVED***_ProgramaInsumosDebeReconciliar...` |
| Financiamiento | Anticipo 30%, 13 filas, AnticipoRecibido `***REMOVED***`, Egresos `***REMOVED***`, Estimación `***REMOVED***`, Saldo mínimo `***REMOVED***` | `***REMOVED***_FinanciamientoDebeConservarFlujoCongelado` |
| Snapshot semántico ampliado | 112 líneas normalizadas en 11 secciones: `PR` proyecto (modo, precisiones 4/2/4, porcentajes y FSR), `C` conceptos (11, con padre canónico, Orden, Unidad y matriz asignada), `M` matrices (7), `X` componentes (11, con insumo/cantidad/rendimiento/importe), `IM`/`IMO`/`IMAQ`/`IHER` insumos base (9), `A` actividades terminales (6), `P` periodos (12), `D` distribuciones (55) | `***REMOVED***_SnapshotSemanticoNormalizado_DebeMantenerseCongelado` |
| Sensibilidad del snapshot | Mutar un precio de insumo altera la sección `IM`; mutar el importe persistido del componente altera la sección `X` (ambas mutaciones se aplican y verifican explícitamente) | `***REMOVED***_SnapshotSemantico_DetectaMutacionDePrecioEnInsumo` |

Regla de normalización de snapshots: nunca excluir valores económicos, jerarquía, precisión ni flags funcionales; normalizar solo IDs técnicos, timestamps, rutas y orden de filas.

---

## 3. Golden FSR (Factor de Salario Real)

`SOPRO.Tests\Services\ManoObra\FsrCalculationServiceTests` — parámetros base (SM 248.93, jornada 8 h, 2026).

| Salario nominal | Factor dorado |
|---|---|
| 250.00 | `1.8631328690438915292144254962` |
| 500.00 | `1.7308240339418507128878948841` |
| 800.00 | `1.6828680219556658279750256267` |

Entradas inválidas (`null`, JSON vacío/inválido, salario ≤ 0) devuelven `null`.

---

## 4. Fixtures y determinismo (acción 10)

- **Cultura:** los formatos del motor consultan `CurrentCulture` en cada llamada; los tests de formato usan `CultureScope` con cultura clonada/controlada (`MotorCalculoSoproFormattingTests`) o culturas con separadores realmente distintos (`es-MX` coma de millar / `de-DE` coma decimal) para detectar un cacheo de cultura (`MotorCalculoSoproEdgeCaseTests`).
- **BD:** `TestDbFactory` crea una SQLite temporal única por prueba (`%TEMP%\sopro_tests_{guid}.db`), con `EnsureDeleted` + `EnsureCreated`. Las pruebas de persistencia cierran el contexto y reabren la misma ruta (`%TEMP%\sopro_persist_{guid}.db`).
- **Proyecto real:** cada test opera sobre una copia de solo lectura en `%TEMP%` (atributo `ReadOnly` + `Mode=ReadOnly`); el `.db` fuente es inmutable y cualquier escritura accidental falla (`***REMOVED***_CopiaInmutable_DebeRechazarEscrituras`).
- **Precisión y orden:** los escenarios fijan DecimalesCantidad/Importe/Porcentaje y porcentajes de proyecto; el motor de la fachada usa `MidpointRounding.AwayFromZero`.
- **Reloj:** el reloj NO es fuente de determinismo económico. `FechaModificacion` se escribe con `DateTime.Now` en `RecalculoGlobalService` (conceptos hoja, agrupadores y actividades) y es un campo de auditoría: ningún escenario dorado congelado depende del valor del reloj, solo de la aritmética del motor.

---

## 5. Golden Costo Horario (legacy Core)

`SOPRO.Tests\Services\CostoHorario\MaquinariaCostoHorarioLegacyTests` — caracteriza directamente `Maquinaria.CalcularCostoHorario()` (fórmula canónica legacy; `FormCalculoCostoHorario.Recalcular` duplica la misma aritmética, ver fila 19 de la tabla de divergencias).

| Caso | Total congelado |
|---|---|
| Caso de referencia (Vm 690,000; Ve 12,000; Hea 1,600; Vn 3,000; Va 5,000; Ht 8; Gh 10×20; Ah 1×40; Sn 300×1.60) | `448.26091666666666666666666667` |
| Ve = 0 → dep y mant = 0 | `311.249325` |
| Hea = 0 → inv y seguros = 0 | `311.79` |
| Ht = 0 → operación = 0 | `258.539325` |
| Vn = Va = 0 → llantas y piezas = 0 | `314.039325` |

Mutaciones caracterizadas: `EsCostoCalculado = true` y `FechaCalculoCosto` dentro de la ventana de ejecución (campos de auditoría; el reloj no participa en la aritmética).

---

## 6. Dueños y estado

| Escenario | Propietario | Estado |
|---|---|---|
| CasoOficial001 (sintético) | ***REMOVED*** | Congelado |
| ***REMOVED*** | ***REMOVED*** + QA contable | Congelado |
| Goldens FSR | ***REMOVED*** | Congelado |
| Golden Costo Horario (legacy Core) | ***REMOVED*** | Congelado |

La congelación se considera aprobada cuando estos valores pasan en 10 ejecuciones consecutivas del conjunto crítico (Gate N0). Definición del conjunto y resultado en la sección 7.

---

## 7. Conjunto crítico del Gate N0 y resultado

Composición (34 tests en 6 clases; filtro `FullyQualifiedName~` por clase):

| Clase | Tests | Escenario |
|---|---|---|
| `SuiteValidacionOficialSoproTests` | 4 | CasoOficial001 sintético |
| `***REMOVED******REMOVED***RegressionTests` | 10 | Proyecto real + snapshot semántico |
| `RecalculoGlobalServiceTests` | 7 | Determinismo y corrupción/restauración del recálculo |
| `PersistenciaRecalculoYPropagacionTests` | 3 | Persistencia al reabrir la BD |
| `FsrCalculationServiceTests` | 4 | Goldens FSR |
| `MaquinariaCostoHorarioLegacyTests` | 6 | Golden costo horario |

Comando reproducible: `dotnet test SOPRO.sln --no-restore --no-build --filter "<clases anteriores unidas con |>"`.

Resultado: 10 ejecuciones consecutivas el 2026-08-11 (34/34 superados, 0 errores, 0 omitidos en cada una; ~3 s por pasada). Resultados idénticos en las 10 pasadas → determinismo confirmado.
