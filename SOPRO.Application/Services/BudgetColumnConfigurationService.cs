using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Application.Services
{
    /// <summary>
    /// Casos de uso de configuración de columnas del presupuesto (ADR-0001). TODA
    /// mutación de <see cref="ColumnaPersonalizada"/> se ejecuta sobre un contexto
    /// NUEVO por operación creado por <see cref="IProjectDbContextFactory"/>, dentro
    /// de una transacción corta y con exactamente un SaveChanges; la vista nunca
    /// escribe sobre su contexto compartido (no se filtran cambios ajenos, p. ej.
    /// un Concepto pendiente). Las definiciones llegan como DTOs disconnected: este
    /// servicio no conoce la fábrica de la UI.
    /// </summary>
    public static class BudgetColumnConfigurationService
    {
        private static readonly HashSet<string> NombresLegacy = new(StringComparer.OrdinalIgnoreCase)
        {
            "Tipo",
            "Clave",
            "Descripcion",
            "Unidad",
            "Cantidad",
            "PrecioUnitario",
            "Importe",
            "PorcentajeIndirectos",
            "Indirectos",
            "PorcentajeFinanciamiento",
            "Financiamiento",
            "PorcentajeUtilidad",
            "Utilidad",
            "PorcentajeCargosAdicionales",
            "CargosAdicionales",
            "PrecioUnitarioFinal",
            "ImporteFinal"
        };

        /// <summary>
        /// Lectura de solo lectura con contexto propio: no rastrea ninguna columna en
        /// el contexto compartido de la vista.
        /// </summary>
        public static IReadOnlyList<BudgetColumnSnapshot> LoadColumns(
            IProjectDbContextFactory factory,
            string databasePath,
            int projectId)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));

            using var context = factory.Create(databasePath);
            return ReadColumns(context, projectId);
        }

        /// <summary>
        /// Bootstrap/aseguramiento de columnas predeterminadas. Conserva el
        /// comportamiento histórico: un set inicial legacy completo (17) se reemplaza
        /// por las 23 definiciones; el resto se completa add-only preservando anchos,
        /// orden y formato de las columnas del usuario, y se eliminan solo los extras
        /// legacy no oficiales. Si no hay nada que hacer no escribe.
        /// </summary>
        public static BudgetColumnConfigurationResult EnsureColumns(
            IProjectDbContextFactory factory,
            string databasePath,
            int projectId,
            IReadOnlyList<BudgetColumnDefinitionDto> definitions)
        {
            var validationError = ValidateDefinitions(definitions, out var defs);
            if (validationError != null)
                return NotCommitted(BudgetOperationResult.Failed(validationError));

            return Execute(factory, databasePath, projectId, context =>
                StageEnsureColumns(context, projectId, defs));
        }

        /// <summary>
        /// Seed de columnas del ciclo de vida del proyecto (equivalente exacto al
        /// comportamiento histórico de <c>EnsureBudgetColumns</c>): si el proyecto ya
        /// tiene la columna base <c>Tipo</c> no se toca nada (preserva la configuración
        /// del usuario); si no la tiene, se elimina la configuración vieja del proyecto
        /// y se siembran las definiciones recibidas (17 legacy) en la MISMA transacción
        /// y el MISMO SaveChanges, sobre contexto propio. El contexto de sesión que
        /// termina compartiéndose no escribe ni confirma entidades ajenas.
        /// </summary>
        public static BudgetColumnConfigurationResult EnsureSeedColumns(
            IProjectDbContextFactory factory,
            string databasePath,
            int projectId,
            IReadOnlyList<BudgetColumnDefinitionDto> definitions)
        {
            var validationError = ValidateDefinitions(definitions, out var defs);
            if (validationError != null)
                return NotCommitted(BudgetOperationResult.Failed(validationError));

            return Execute(factory, databasePath, projectId, context =>
                StageEnsureSeedColumns(context, projectId, defs));
        }

        /// <summary>
        /// Definiciones legacy (17) con las que el ciclo de vida sembraba el
        /// presupuesto antes de que la vista lo abra; la vista las convierte luego a
        /// su set de 23. Fábrica pura, sin I/O.
        /// </summary>
        public static IReadOnlyList<BudgetColumnDefinitionDto> CrearDefinicionesLegacyPresupuesto()
        {
            return new List<BudgetColumnDefinitionDto>
            {
                Columna("Tipo", "Tipo", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Texto,
                    ancho: 120, visible: true, orden: -7, alineacion: AlineacionColumna.Centro),
                Columna("Clave", "Clave", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Texto,
                    ancho: 100, visible: true, orden: -6, alineacion: AlineacionColumna.Izquierda),
                Columna("Descripción", "Descripcion", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Texto,
                    ancho: 300, visible: true, orden: -5, alineacion: AlineacionColumna.Izquierda),
                Columna("Unidad", "Unidad", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Texto,
                    ancho: 80, visible: true, orden: -4, alineacion: AlineacionColumna.Centro),
                Columna("Cantidad", "Cantidad", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Numerico,
                    ancho: 100, visible: true, orden: -3, alineacion: AlineacionColumna.Derecha,
                    formatoNumerico: "N2"),
                Columna("P.U.", "PrecioUnitario", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 120, visible: true, orden: -2, alineacion: AlineacionColumna.Derecha,
                    formula: "CostoDirectoUnitario", formatoNumerico: "C4"),
                Columna("Importe", "Importe", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 140, visible: true, orden: -1, alineacion: AlineacionColumna.Derecha,
                    formula: "Cantidad * CostoDirectoUnitario", formatoNumerico: "C2"),
                Columna("% Indirectos", "PorcentajeIndirectos", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Porcentaje,
                    ancho: 90, visible: false, orden: 1, alineacion: AlineacionColumna.Derecha,
                    formatoNumerico: "0.00"),
                Columna("Indirectos", "Indirectos", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 110, visible: false, orden: 2, alineacion: AlineacionColumna.Derecha,
                    formula: "CostoDirecto * (PorcentajeIndirectos / 100)", formatoNumerico: "C2"),
                Columna("% Financiamiento", "PorcentajeFinanciamiento", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Porcentaje,
                    ancho: 110, visible: false, orden: 3, alineacion: AlineacionColumna.Derecha,
                    formatoNumerico: "0.00"),
                Columna("Financiamiento", "Financiamiento", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 110, visible: false, orden: 4, alineacion: AlineacionColumna.Derecha,
                    formula: "(CostoDirecto + Indirectos) * (PorcentajeFinanciamiento / 100)", formatoNumerico: "C2"),
                Columna("% Utilidad", "PorcentajeUtilidad", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Porcentaje,
                    ancho: 90, visible: false, orden: 5, alineacion: AlineacionColumna.Derecha,
                    formatoNumerico: "0.00"),
                Columna("Utilidad", "Utilidad", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 110, visible: false, orden: 6, alineacion: AlineacionColumna.Derecha,
                    formula: "(CostoDirecto + Indirectos + Financiamiento) * (PorcentajeUtilidad / 100)", formatoNumerico: "C2"),
                Columna("% Cargos Adicionales", "PorcentajeCargosAdicionales", TipoColumnaPersonalizada.Personal, TipoDatoColumna.Porcentaje,
                    ancho: 130, visible: false, orden: 7, alineacion: AlineacionColumna.Derecha,
                    formatoNumerico: "0.00"),
                Columna("Cargos Adicionales", "CargosAdicionales", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 140, visible: false, orden: 8, alineacion: AlineacionColumna.Derecha,
                    formula: "(CostoDirecto + Indirectos + Financiamiento + Utilidad) * (PorcentajeCargosAdicionales / 100)", formatoNumerico: "C2"),
                Columna("Precio Unitario Final", "PrecioUnitarioFinal", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 140, visible: false, orden: 9, alineacion: AlineacionColumna.Derecha,
                    formula: "CostoDirecto + Indirectos + Financiamiento + Utilidad + CargosAdicionales", formatoNumerico: "C4"),
                Columna("Importe Final", "ImporteFinal", TipoColumnaPersonalizada.Calculada, TipoDatoColumna.Moneda,
                    ancho: 140, visible: false, orden: 10, alineacion: AlineacionColumna.Derecha,
                    formula: "Cantidad * PrecioUnitarioFinal", formatoNumerico: "C2")
            };
        }

        private static BudgetColumnDefinitionDto Columna(
            string nombre,
            string nombreInterno,
            TipoColumnaPersonalizada tipoColumna,
            TipoDatoColumna tipoDato,
            int ancho,
            bool visible,
            int orden,
            AlineacionColumna alineacion,
            string formula = "",
            string formatoNumerico = "")
        {
            return new BudgetColumnDefinitionDto
            {
                Nombre = nombre,
                NombreInterno = nombreInterno,
                TipoColumna = tipoColumna,
                TipoDato = tipoDato,
                AnchoColumna = ancho,
                Visible = visible,
                Orden = orden,
                Alineacion = alineacion,
                Formula = formula,
                FormatoNumerico = formatoNumerico
            };
        }

        /// <summary>Persiste el ancho de UNA columna del proyecto.</summary>
        public static BudgetColumnConfigurationResult UpdateWidth(
            IProjectDbContextFactory factory,
            string databasePath,
            int projectId,
            int columnId,
            int width)
        {
            if (width <= 0)
                return NotCommitted(BudgetOperationResult.Failed("El ancho de la columna debe ser mayor que cero."));

            return Execute(factory, databasePath, projectId, context =>
            {
                if (!ProjectExists(context, projectId))
                    return Conflict("El proyecto ya no existe en la base; la vista debe recargarse.");

                var columna = context.ColumnasPersonalizadas.FirstOrDefault(c => c.Id == columnId);
                if (columna == null || columna.ProyectoId != projectId)
                    return Conflict($"La columna {columnId} ya no existe en el proyecto {projectId}; la vista debe recargarse.");

                if (columna.AnchoColumna == width)
                    return NoChanges(context, projectId);

                columna.AnchoColumna = width;
                return CommittedAfterSave(context, projectId);
            });
        }

        /// <summary>
        /// Persiste un lote completo de orden de columnas en UNA sola escritura: si
        /// alguna columna no pertenece al proyecto, el lote se rechaza completo y no
        /// se escribe nada.
        /// </summary>
        public static BudgetColumnConfigurationResult UpdateOrder(
            IProjectDbContextFactory factory,
            string databasePath,
            int projectId,
            IReadOnlyList<BudgetColumnOrderEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return NotCommitted(BudgetOperationResult.Failed("El lote de orden está vacío; no se guardó nada."));

            var ids = new HashSet<int>();
            foreach (var entry in entries)
            {
                if (entry.ColumnId <= 0)
                    return NotCommitted(BudgetOperationResult.Failed("El lote de orden contiene una columna inválida; no se guardó nada."));
                if (!ids.Add(entry.ColumnId))
                    return NotCommitted(BudgetOperationResult.Failed("El lote de orden contiene columnas repetidas; no se guardó nada."));
            }

            return Execute(factory, databasePath, projectId, context =>
            {
                if (!ProjectExists(context, projectId))
                    return Conflict("El proyecto ya no existe en la base; la vista debe recargarse.");

                var columnas = context.ColumnasPersonalizadas
                    .Where(c => c.ProyectoId == projectId)
                    .ToList();

                var porId = columnas.ToDictionary(c => c.Id);
                foreach (var entry in entries)
                {
                    if (!porId.ContainsKey(entry.ColumnId))
                        return Conflict(
                            $"La columna {entry.ColumnId} ya no existe en el proyecto {projectId}; la vista debe recargarse.");
                }

                bool huboCambios = false;
                foreach (var entry in entries)
                {
                    var columna = porId[entry.ColumnId];
                    if (columna.Orden == entry.Orden)
                        continue;

                    columna.Orden = entry.Orden;
                    huboCambios = true;
                }

                return huboCambios
                    ? CommittedAfterSave(context, projectId)
                    : NoChanges(context, projectId);
            });
        }

        /// <summary>Aplica el formato individual completo a UNA columna del proyecto.</summary>
        public static BudgetColumnConfigurationResult ApplyColumnFormat(
            IProjectDbContextFactory factory,
            string databasePath,
            int projectId,
            int columnId,
            BudgetColumnFormatDto format)
        {
            var formatError = ValidateFormat(format);
            if (formatError != null)
                return NotCommitted(BudgetOperationResult.Failed(formatError));

            return Execute(factory, databasePath, projectId, context =>
            {
                if (!ProjectExists(context, projectId))
                    return Conflict("El proyecto ya no existe en la base; la vista debe recargarse.");

                var columna = context.ColumnasPersonalizadas.FirstOrDefault(c => c.Id == columnId);
                if (columna == null || columna.ProyectoId != projectId)
                    return Conflict($"La columna {columnId} ya no existe en el proyecto {projectId}; la vista debe recargarse.");

                ApplyFullFormat(columna, format);
                return CommittedAfterSave(context, projectId);
            });
        }

        /// <summary>
        /// Aplica un formato global a TODAS las columnas del proyecto (nunca a otros
        /// proyectos) en una sola escritura. El color de fondo no se copia.
        /// </summary>
        public static BudgetColumnConfigurationResult ApplyGlobalColumnFormat(
            IProjectDbContextFactory factory,
            string databasePath,
            int projectId,
            BudgetColumnFormatDto format,
            BudgetGlobalColumnFormatScope scope)
        {
            var formatError = ValidateFormat(format);
            if (formatError != null)
                return NotCommitted(BudgetOperationResult.Failed(formatError));
            if (!Enum.IsDefined(scope))
                return NotCommitted(BudgetOperationResult.Failed("El alcance del formato global no es válido."));

            return Execute(factory, databasePath, projectId, context =>
            {
                if (!ProjectExists(context, projectId))
                    return Conflict("El proyecto ya no existe en la base; la vista debe recargarse.");

                var columnas = context.ColumnasPersonalizadas
                    .Where(c => c.ProyectoId == projectId)
                    .ToList();

                if (columnas.Count == 0)
                    return NoChanges(context, projectId);

                foreach (var columna in columnas)
                {
                    columna.NombreFuente = NormalizeFontName(format.NombreFuente);
                    columna.TamanoFuente = format.TamanoFuente;
                    columna.Negrita = format.Negrita;
                    columna.Cursiva = format.Cursiva;
                    columna.ColorFuente = NormalizeColor(format.ColorFuente, "#000000");

                    if (scope == BudgetGlobalColumnFormatScope.Full)
                    {
                        columna.Alineacion = format.Alineacion;
                        columna.WrapTexto = format.WrapTexto;
                        columna.AlineacionVertical = format.AlineacionVertical;
                    }

                    columna.FechaModificacion = DateTime.Now;
                }

                return CommittedAfterSave(context, projectId);
            });
        }

        /// <summary>
        /// Alta de una columna personal. Normaliza nombre y nombre interno (derivado
        /// del nombre cuando no se informa), valida longitudes y unicidad normalizada
        /// (OrdinalIgnoreCase, igual que el aseguramiento) y coloca la columna al final.
        /// </summary>
        public static BudgetColumnConfigurationResult AddColumn(
            IProjectDbContextFactory factory,
            string databasePath,
            int projectId,
            BudgetColumnDefinitionDto definition)
        {
            if (definition == null)
                return NotCommitted(BudgetOperationResult.Failed("La definición de la columna es requerida."));

            var nombre = (definition.Nombre ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nombre))
                return NotCommitted(BudgetOperationResult.Failed("El nombre de la columna es obligatorio."));
            if (nombre.Length > 100)
                return NotCommitted(BudgetOperationResult.Failed("El nombre de la columna no puede exceder 100 caracteres."));

            var nombreInterno = (definition.NombreInterno ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nombreInterno))
                nombreInterno = "Col_" + new string(nombre.Where(char.IsLetterOrDigit).ToArray());
            if (string.IsNullOrWhiteSpace(nombreInterno))
                return NotCommitted(BudgetOperationResult.Failed("No se pudo derivar el nombre interno de la columna."));
            if (nombreInterno.Length > 50)
                return NotCommitted(BudgetOperationResult.Failed("El nombre interno de la columna no puede exceder 50 caracteres."));

            return Execute(factory, databasePath, projectId, context =>
            {
                if (!ProjectExists(context, projectId))
                    return Conflict("El proyecto ya no existe en la base; la vista debe recargarse.");

                var existentes = context.ColumnasPersonalizadas
                    .Where(c => c.ProyectoId == projectId)
                    .ToList();

                if (existentes.Any(c => string.Equals(c.Nombre ?? string.Empty, nombre, StringComparison.OrdinalIgnoreCase)))
                    return Conflict($"Ya existe una columna con el nombre {nombre} en el proyecto.");

                if (existentes.Any(c => string.Equals(c.NombreInterno ?? string.Empty, nombreInterno, StringComparison.OrdinalIgnoreCase)))
                    return Conflict($"Ya existe una columna con el nombre interno {nombreInterno} en el proyecto.");

                var columna = definition.ToEntity(projectId);
                columna.Nombre = nombre;
                columna.NombreInterno = nombreInterno;
                columna.Orden = (existentes.Max(c => (int?)c.Orden) ?? 0) + 1;
                columna.FechaCreacion = DateTime.Now;
                columna.FechaModificacion = DateTime.Now;

                context.ColumnasPersonalizadas.Add(columna);
                return CommittedAfterSave(context, projectId);
            });
        }

        /// <summary>Renombra una columna existente; el nombre interno no cambia.</summary>
        public static BudgetColumnConfigurationResult RenameColumn(
            IProjectDbContextFactory factory,
            string databasePath,
            int projectId,
            int columnId,
            string nombre)
        {
            var nuevoNombre = (nombre ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nuevoNombre))
                return NotCommitted(BudgetOperationResult.Failed("El nombre de la columna es obligatorio."));
            if (nuevoNombre.Length > 100)
                return NotCommitted(BudgetOperationResult.Failed("El nombre de la columna no puede exceder 100 caracteres."));

            return Execute(factory, databasePath, projectId, context =>
            {
                if (!ProjectExists(context, projectId))
                    return Conflict("El proyecto ya no existe en la base; la vista debe recargarse.");

                var columna = context.ColumnasPersonalizadas.FirstOrDefault(c => c.Id == columnId);
                if (columna == null || columna.ProyectoId != projectId)
                    return Conflict($"La columna {columnId} ya no existe en el proyecto {projectId}; la vista debe recargarse.");

                if (string.Equals(columna.Nombre ?? string.Empty, nuevoNombre, StringComparison.Ordinal))
                    return NoChanges(context, projectId);

                bool duplicado = context.ColumnasPersonalizadas.Any(c =>
                    c.ProyectoId == projectId
                    && c.Id != columnId
                    && c.Nombre != null
                    && c.Nombre.ToUpper() == nuevoNombre.ToUpper());

                if (duplicado)
                    return Conflict($"Ya existe una columna con el nombre {nuevoNombre} en el proyecto.");

                columna.Nombre = nuevoNombre;
                columna.FechaModificacion = DateTime.Now;
                return CommittedAfterSave(context, projectId);
            });
        }

        /// <summary>
        /// Cambia la visibilidad de una columna. Al hacer visible una columna oculta
        /// con una posición de inserción conocida, la coloca a la derecha de la
        /// columna en foco y desplaza el resto, en el mismo único guardado.
        /// </summary>
        public static BudgetColumnConfigurationResult SetColumnVisibility(
            IProjectDbContextFactory factory,
            string databasePath,
            int projectId,
            int columnId,
            bool visible,
            int? insertionIndex = null)
        {
            return Execute(factory, databasePath, projectId, context =>
            {
                if (!ProjectExists(context, projectId))
                    return Conflict("El proyecto ya no existe en la base; la vista debe recargarse.");

                var columnas = context.ColumnasPersonalizadas
                    .Where(c => c.ProyectoId == projectId)
                    .ToList();

                var columna = columnas.FirstOrDefault(c => c.Id == columnId);
                if (columna == null)
                    return Conflict($"La columna {columnId} ya no existe en el proyecto {projectId}; la vista debe recargarse.");

                bool huboCambios = false;

                if (visible && !columna.Visible && insertionIndex.HasValue && insertionIndex.Value >= 0)
                {
                    var ordenadas = columnas
                        .OrderBy(c => c.Visible ? 0 : 1)
                        .ThenBy(c => c.Orden)
                        .ThenBy(c => c.Id)
                        .ToList();

                    var enFoco = ordenadas.Skip(insertionIndex.Value).FirstOrDefault();
                    int nuevoOrden = enFoco != null
                        ? enFoco.Orden + 1
                        : (columnas.Max(c => (int?)c.Orden) ?? 0) + 1;

                    foreach (var otra in columnas.Where(c => c.Orden >= nuevoOrden && c.Id != columna.Id))
                    {
                        otra.Orden++;
                        huboCambios = true;
                    }

                    if (columna.Orden != nuevoOrden)
                        huboCambios = true;

                    columna.Orden = nuevoOrden;
                }

                if (columna.Visible != visible)
                {
                    columna.Visible = visible;
                    huboCambios = true;
                }

                return huboCambios
                    ? CommittedAfterSave(context, projectId)
                    : NoChanges(context, projectId);
            });
        }

        /// <summary>Elimina una columna del proyecto.</summary>
        public static BudgetColumnConfigurationResult DeleteColumn(
            IProjectDbContextFactory factory,
            string databasePath,
            int projectId,
            int columnId)
        {
            return Execute(factory, databasePath, projectId, context =>
            {
                if (!ProjectExists(context, projectId))
                    return Conflict("El proyecto ya no existe en la base; la vista debe recargarse.");

                var columna = context.ColumnasPersonalizadas.FirstOrDefault(c => c.Id == columnId);
                if (columna == null || columna.ProyectoId != projectId)
                    return Conflict($"La columna {columnId} ya no existe en el proyecto {projectId}; la vista debe recargarse.");

                context.ColumnasPersonalizadas.Remove(columna);
                return CommittedAfterSave(context, projectId);
            });
        }

        /// <summary>
        /// Reset explícito a las columnas predeterminadas: borra las columnas del
        /// proyecto con ExecuteDelete y crea las 23 definiciones en la MISMA
        /// transacción y el MISMO SaveChanges, en orden único bajo el índice
        /// (ProyectoId, NombreInterno). La confirmación al usuario es responsabilidad
        /// de la UI y debe ocurrir ANTES de invocar este método.
        /// </summary>
        public static BudgetColumnConfigurationResult ResetToDefaults(
            IProjectDbContextFactory factory,
            string databasePath,
            int projectId,
            IReadOnlyList<BudgetColumnDefinitionDto> definitions)
        {
            var validationError = ValidateDefinitions(definitions, out var defs);
            if (validationError != null)
                return NotCommitted(BudgetOperationResult.Failed(validationError));

            return Execute(factory, databasePath, projectId, context =>
            {
                if (!ProjectExists(context, projectId))
                    return Conflict("El proyecto ya no existe en la base; la vista debe recargarse.");

                context.ColumnasPersonalizadas
                    .Where(c => c.ProyectoId == projectId)
                    .ExecuteDelete();

                context.ColumnasPersonalizadas.AddRange(defs.Select(d => d.ToEntity(projectId)));
                return CommittedAfterSave(context, projectId);
            });
        }

        private static BudgetWriteStep<IReadOnlyList<BudgetColumnSnapshot>> StageEnsureSeedColumns(
            SOPROContext context,
            int projectId,
            IReadOnlyList<BudgetColumnDefinitionDto> defs)
        {
            if (!ProjectExists(context, projectId))
                return Conflict("El proyecto ya no existe en la base; la vista debe recargarse.");

            // Misma condición de corte que el seed histórico: la existencia de la
            // columna base Tipo significa que el usuario ya tiene configuración.
            bool tieneColumnaBase = context.ColumnasPersonalizadas
                .Any(c => c.ProyectoId == projectId && c.NombreInterno == "Tipo");

            if (tieneColumnaBase)
                return NoChanges(context, projectId);

            context.ColumnasPersonalizadas
                .Where(c => c.ProyectoId == projectId)
                .ExecuteDelete();

            context.ColumnasPersonalizadas.AddRange(defs.Select(d => d.ToEntity(projectId)));
            return CommittedAfterSave(context, projectId);
        }

        private static BudgetWriteStep<IReadOnlyList<BudgetColumnSnapshot>> StageEnsureColumns(
            SOPROContext context,
            int projectId,
            IReadOnlyList<BudgetColumnDefinitionDto> defs)
        {
            if (!ProjectExists(context, projectId))
                return Conflict("El proyecto ya no existe en la base; la vista debe recargarse.");

            var existentes = context.ColumnasPersonalizadas
                .Where(c => c.ProyectoId == projectId)
                .ToList();

            if (existentes.Count == 0)
            {
                context.ColumnasPersonalizadas.AddRange(defs.Select(d => d.ToEntity(projectId)));
                return CommittedAfterSave(context, projectId);
            }

            if (EsSetInicialLegacyPresupuesto(existentes))
            {
                context.ColumnasPersonalizadas
                    .Where(c => c.ProyectoId == projectId)
                    .ExecuteDelete();

                foreach (var eliminada in existentes)
                    context.Entry(eliminada).State = EntityState.Detached;

                context.ColumnasPersonalizadas.AddRange(defs.Select(d => d.ToEntity(projectId)));
                return CommittedAfterSave(context, projectId);
            }

            var oficiales = new HashSet<string>(
                defs.Select(d => d.NombreInterno),
                StringComparer.OrdinalIgnoreCase);

            var extrasLegacy = existentes
                .Where(c => !oficiales.Contains(c.NombreInterno ?? string.Empty))
                .Where(c => EsColumnaLegacyNoOficial(c.NombreInterno))
                .ToList();

            bool soloTieneExtrasLegacyNoOficiales = extrasLegacy.Count > 0
                && existentes.All(c => oficiales.Contains(c.NombreInterno ?? string.Empty)
                    || EsColumnaLegacyNoOficial(c.NombreInterno));

            bool huboEliminaciones = false;

            if (soloTieneExtrasLegacyNoOficiales)
            {
                var ids = extrasLegacy.Select(c => c.Id).ToList();
                context.ColumnasPersonalizadas
                    .Where(c => c.ProyectoId == projectId && ids.Contains(c.Id))
                    .ExecuteDelete();

                foreach (var eliminada in extrasLegacy)
                    context.Entry(eliminada).State = EntityState.Detached;

                huboEliminaciones = true;

                existentes = context.ColumnasPersonalizadas
                    .Where(c => c.ProyectoId == projectId)
                    .ToList();
            }

            var existentesPorNombre = new HashSet<string>(
                existentes.Select(c => c.NombreInterno ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            var faltantes = defs
                .Where(d => !existentesPorNombre.Contains(d.NombreInterno))
                .ToList();

            if (faltantes.Count == 0)
            {
                // ExecuteDelete es DML inmediato: la ruta debe confirmarse con el
                // SaveChanges del ejecutor. NoChanges (sin SaveChanges) solo es válido
                // si no hubo ninguna escritura en esta operación.
                return huboEliminaciones
                    ? CommittedAfterSave(context, projectId)
                    : NoChanges(context, projectId);
            }

            context.ColumnasPersonalizadas.AddRange(faltantes.Select(d => d.ToEntity(projectId)));
            return CommittedAfterSave(context, projectId);
        }

        private static BudgetColumnConfigurationResult Execute(
            IProjectDbContextFactory factory,
            string databasePath,
            int projectId,
            Func<SOPROContext, BudgetWriteStep<IReadOnlyList<BudgetColumnSnapshot>>> stage)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            if (projectId <= 0)
                return NotCommitted(BudgetOperationResult.Failed("El proyecto indicado no es válido; no se guardó nada."));

            var execution = BudgetWriteExecutor.Execute(
                factory,
                databasePath,
                useTransaction: true,
                stage);

            return new BudgetColumnConfigurationResult(
                execution.Result, execution.Confirmed, execution.Diagnostic);
        }

        private static BudgetColumnConfigurationResult NotCommitted(BudgetOperationResult result)
            => new(result, null, null);

        private static BudgetWriteStep<IReadOnlyList<BudgetColumnSnapshot>> Conflict(string message)
            => BudgetWriteStep<IReadOnlyList<BudgetColumnSnapshot>>.Abort(
                BudgetOperationResult.Conflict(message));

        private static BudgetWriteStep<IReadOnlyList<BudgetColumnSnapshot>> NoChanges(
            SOPROContext context, int projectId)
            => BudgetWriteStep<IReadOnlyList<BudgetColumnSnapshot>>.NoChanges(
                ReadColumns(context, projectId));

        private static BudgetWriteStep<IReadOnlyList<BudgetColumnSnapshot>> CommittedAfterSave(
            SOPROContext context, int projectId)
            => BudgetWriteStep<IReadOnlyList<BudgetColumnSnapshot>>.CommitAfterSave(
                () => ReadColumns(context, projectId),
                () => Array.Empty<int>());

        private static IReadOnlyList<BudgetColumnSnapshot> ReadColumns(SOPROContext context, int projectId)
        {
            return context.ColumnasPersonalizadas
                .AsNoTracking()
                .Where(c => c.ProyectoId == projectId)
                .OrderBy(c => c.Visible ? 0 : 1)
                .ThenBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList()
                .Select(BudgetColumnSnapshot.FromEntity)
                .ToArray();
        }

        private static bool ProjectExists(SOPROContext context, int projectId)
            => context.Proyectos.AsNoTracking().Any(p => p.Id == projectId);

        private static void ApplyFullFormat(ColumnaPersonalizada columna, BudgetColumnFormatDto format)
        {
            columna.NombreFuente = NormalizeFontName(format.NombreFuente);
            columna.TamanoFuente = format.TamanoFuente;
            columna.Negrita = format.Negrita;
            columna.Cursiva = format.Cursiva;
            columna.Alineacion = format.Alineacion;
            columna.ColorFondo = NormalizeColor(format.ColorFondo, "#FFFFFF");
            columna.ColorFuente = NormalizeColor(format.ColorFuente, "#000000");
            columna.WrapTexto = format.WrapTexto;
            columna.AlineacionVertical = format.AlineacionVertical;
            columna.FechaModificacion = DateTime.Now;
        }

        private static string NormalizeFontName(string? nombreFuente)
            => string.IsNullOrWhiteSpace(nombreFuente) ? "Segoe UI" : nombreFuente.Trim();

        private static string NormalizeColor(string? color, string fallback)
            => string.IsNullOrWhiteSpace(color) ? fallback : color.Trim();

        private static string? ValidateFormat(BudgetColumnFormatDto? format)
        {
            if (format == null)
                return "El formato de la columna es requerido.";

            if (format.TamanoFuente <= 0 || format.TamanoFuente > 200)
                return "El tamaño de fuente no es válido.";

            if (!Enum.IsDefined(format.Alineacion))
                return "La alineación de la columna no es válida.";

            if (format.AlineacionVertical < 0 || format.AlineacionVertical > 2)
                return "La alineación vertical de la columna no es válida.";

            if (!string.IsNullOrEmpty(format.NombreFuente) && format.NombreFuente.Length > 100)
                return "El nombre de la fuente no es válido.";

            return null;
        }

        /// <summary>
        /// Valida las definiciones que entrega la fábrica de la vista: no vacías,
        /// nombres obligatorios y acotados, y sin duplicados normalizados
        /// (OrdinalIgnoreCase, misma semántica de comparación que el aseguramiento).
        /// </summary>
        private static string? ValidateDefinitions(
            IReadOnlyList<BudgetColumnDefinitionDto>? definitions,
            out IReadOnlyList<BudgetColumnDefinitionDto> defs)
        {
            defs = Array.Empty<BudgetColumnDefinitionDto>();

            if (definitions == null || definitions.Count == 0)
                return "No se recibieron definiciones de columnas; no se guardó nada.";

            var nombresInternos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var definition in definitions)
            {
                if (definition == null)
                    return "Las definiciones de columnas contienen un elemento nulo; no se guardó nada.";

                var nombre = (definition.Nombre ?? string.Empty).Trim();
                var nombreInterno = (definition.NombreInterno ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(nombreInterno))
                    return "Toda columna requiere nombre y nombre interno; no se guardó nada.";

                if (nombre.Length > 100)
                    return "El nombre de una columna no puede exceder 100 caracteres; no se guardó nada.";

                if (nombreInterno.Length > 50)
                    return "El nombre interno de una columna no puede exceder 50 caracteres; no se guardó nada.";

                if (definition.AnchoColumna <= 0)
                    return "El ancho de una columna debe ser mayor que cero; no se guardó nada.";

                if (!nombresInternos.Add(nombreInterno) || !nombres.Add(nombre))
                    return "Las definiciones de columnas contienen nombres duplicados; no se guardó nada.";
            }

            defs = definitions;
            return null;
        }

        private static bool EsSetInicialLegacyPresupuesto(IReadOnlyList<ColumnaPersonalizada> existentes)
        {
            if (existentes.Count != 17)
                return false;

            return existentes.All(c => NombresLegacy.Contains(c.NombreInterno ?? string.Empty));
        }

        private static bool EsColumnaLegacyNoOficial(string? nombreInterno)
        {
            return string.Equals(nombreInterno, "PorcentajeCargosAdicionales", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nombreInterno, "ImporteFinal", StringComparison.OrdinalIgnoreCase);
        }
    }
}
