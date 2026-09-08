# Changelog

All notable changes to `SOPRO.Calculation` are documented here.

## Unreleased

- Working calendar operations are now available through the pure
  `WorkingCalendarCalculator`, with immutable weekly patterns, defensively copied
  exceptions, explicit inclusive/exclusive date semantics, deterministic date
  sanitization, fail-fast validation for calendars without working days, and direct
  indexed lookup for finite exception-only calendars.
- Activity network and critical-path operations are now available through the pure
  `ActivityNetworkCalculator`. The calculator supports FS, SS, FF and SF relations,
  working-day lags, early/late dates, slack and critical-path membership; it rejects
  cycles, duplicate IDs, unknown references and undefined dependency types while
  preserving the legacy mixed start/finish range rule.
- Mexican real salary factor (FSR) is now available as a pure, immutable scalar
  calculation through `RealSalaryFactorCalculator`, including the complete report
  breakdown and the legacy AE-2(C) `AE + AF` aggregate.
- FSR preserves year/semester caps, strict contribution-limit branches, the night-shift
  fallback for unknown enum values, non-positive day guards, and arithmetic exception
  propagation. JSON parsing, defaults, formatting and nullable service behavior remain
  adapter responsibilities.
- Hourly machinery cost is now available as a pure, immutable calculation in the
  standalone package through `HourlyCostCalculator`; Core, WinForms and the PDF/Excel
  cost-hour reports delegate to it.
- Non-positive denominator compatibility preserves visible intermediate values and
  only suppresses dependent divisions; overflow propagates for all inputs.
- Matrix graph: `MatrixNodeInput` supports optional precomputed leaf totals
  (`precomputedDirectCostTotal`) for auxiliary nodes whose components are not expanded
  in the graph (the stored auxiliary cost is consumed exactly as the canonical
  single-matrix calculator contract required, negative costs included). Leaves must
  have no components and must be Basic or Crew. The original three-parameter
  constructor is preserved as an overload for binary compatibility.
- `SOPRO.Application.MatrixComponentCalculationService` now delegates all matrix
  component arithmetic to `MatrixGraphCalculator` through `MatrixGraphSnapshotAdapter`
  (single implementation, N7-1b).

## 0.1.0 - 2026-08-22

- Initial private preview of the standalone calculation engine.
- Screen-precision rounding, multiplication and amount distribution.
- Accumulative and over-direct-cost percentage cascades.
- Zero runtime dependencies and no SOPRO domain types in the public API.
