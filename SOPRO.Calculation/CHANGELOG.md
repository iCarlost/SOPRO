# Changelog

All notable changes to `SOPRO.Calculation` are documented here.

## Unreleased

- Hourly machinery cost is now available as a pure, immutable calculation in the
  standalone package through `HourlyCostCalculator`; Core, WinForms and the PDF/Excel
  cost-hour reports delegate to it.
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
