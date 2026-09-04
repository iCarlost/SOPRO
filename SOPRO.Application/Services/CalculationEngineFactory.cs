using System;
using Sopro.Calculation;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    /// <summary>
    /// [N7-8] Single construction point for project-scoped calculation engines.
    /// Replaces the ~20 inline `new SoproCalculationEngine(proyecto.DecimalesCantidad,
    /// proyecto.DecimalesImporte, proyecto.DecimalesPorcentaje)` copies (plus two
    /// private BuildEngine helpers). Nullable-proyecto fallbacks at the call sites
    /// keep their own defaults verbatim ((2,2,4) or (4,2,4)); only the non-null
    /// branch delegates here.
    /// </summary>
    public static class CalculationEngineFactory
    {
        /// <summary>
        /// Creates an engine from the project's screen-precision configuration.
        /// </summary>
        public static SoproCalculationEngine FromProyecto(Proyecto proyecto)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            return new SoproCalculationEngine(
                proyecto.DecimalesCantidad,
                proyecto.DecimalesImporte,
                proyecto.DecimalesPorcentaje);
        }
    }
}
