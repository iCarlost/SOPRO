# Changelog

All notable changes to `SOPRO.Calculation` are documented here.

## Unreleased

- Matrix graph: `MatrixNodeInput` supports optional precomputed leaf totals
  (`precomputedDirectCostTotal`) for auxiliary nodes whose components are not expanded
  in the graph (the stored auxiliary cost is consumed exactly as the canonical
  single-matrix calculator contract required). Leaves must have no components, must be
  Basic or Crew, and must carry a non-negative total.
- `SOPRO.Application.MatrixComponentCalculationService` now delegates all matrix
  component arithmetic to `MatrixGraphCalculator` through `MatrixGraphSnapshotAdapter`
  (single implementation, N7-1b).

## 0.1.0 - 2026-08-22

- Initial private preview of the standalone calculation engine.
- Screen-precision rounding, multiplication and amount distribution.
- Accumulative and over-direct-cost percentage cascades.
- Zero runtime dependencies and no SOPRO domain types in the public API.
