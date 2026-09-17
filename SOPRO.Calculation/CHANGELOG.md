# Changelog

All notable changes to `SOPRO.Calculation` are documented here.

## Unreleased

- The CD/CI preparation of the financing flow is now available as a pure,
  immutable calculation through `FinancingPreparationCalculator` (N7-17d).
  `PrepareBaseSchedule` distributes the concept direct cost across the program
  periods multiplying the programmed quantity by the visible unit price (unit
  price rounded first, result rounded afterwards, project amount precision) and
  absorbs each distribution residue in the last period whose programmed quantity
  or computed import is non-zero; the collected estimate is accumulated from
  `ImporteProgramado` rounding after each addition and never receives the direct
  cost residue. `ReconcileIndirectCost` replicates the legacy reconciliation of
  the official indirect-cost total across the periods: proportional distribution
  over the periods that have amount, or over the direct-cost base when the
  current total is zero (the last period absorbs the rounding residue), or the
  whole official total assigned to the last period when the base is zero too.
  The official indirect-cost total itself remains an application concern computed
  by the single budget reference-cost preview implementation; EF queries,
  filtering, grouping, ordering, mapping and persistence stay in
  `FinancingCalculationAdapter`.
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
- `ActivityNetworkActivityResult` exposes the effective `DurationWorkingDays` after
  dependency constraints, so consumers can persist the resized range like the legacy
  schedule did.
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
- Financing is now available as a pure, immutable calculation through
  `FinancingCalculator`. It reproduces the legacy `FinanciamientoCalculationService`
  arithmetic against the N7-17a characterization goldens before any production code
  delegates to it: advance basis (total budget with fallback to the accumulated
  base), accumulation base expressed as `FinancingBaseCalculationMode`
  (`Accumulative` = direct + indirect cost, `OverDirectCost` = direct cost),
  advance amortization capped by pending and collected amounts, per-period rate
  `Round(annualRate x days / 365, 8)`, interest `Round(balance x rate, 4)` accrued
  on negative balances (or on both signs in dual mode, charging the effective
  rate — TIIE + additional points — on balances against and the plain TIIE rate
  on balances in favor), percentage
  `Round(net / base x 100, 5)`, and the delay-suffix period construction. Precision
  is explicit through `FinancingPrecision` (`Legacy` factory mirrors the legacy
  widths: amounts project precision, fixed 8/6/4/5 for rate/amortization/
  interest/percentage), and EF, entities and persistence remain adapter
  responsibilities (N7-17c). The "estimated amount" of each input period is the
  scheduled collected estimate (`ImporteProgramado`); the distribution residue that
  reshapes direct cost does not rewrite collected estimates. Base-period row dates
  preserve the exact input `DateTime` values (the legacy persisted the original
  timestamps); delay-suffix rows are still built at midnight.

## 0.1.0 - 2026-08-22

- Initial preview of the standalone calculation engine.
- Screen-precision rounding, multiplication and amount distribution.
- Accumulative and over-direct-cost percentage cascades.
- Zero runtime dependencies and no SOPRO domain types in the public API.
