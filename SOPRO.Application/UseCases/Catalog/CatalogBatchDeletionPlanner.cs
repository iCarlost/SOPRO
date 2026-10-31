using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.UseCases.Catalog;

/// <summary>
/// Plan de una eliminación en lote: la selección normalizada y el conjunto exacto
/// de dependencias que la operación va a tocar (componentes, matrices directas,
/// cierre ascendente, hojas y agrupadores del presupuesto). Es el resultado de
/// una lectura sin seguimiento: no muta nada.
/// </summary>
internal sealed class CatalogBatchDeletionPlan
{
    public CatalogBatchDeletionPlan(
        CatalogItemKind kind,
        List<int> ids,
        List<int> componentIds,
        List<int> directMatrixIds,
        List<int> closureMatrixIds,
        List<int> affectedConceptIds,
        int affectedAggregatorCount,
        string fingerprint)
    {
        Kind = kind;
        Ids = ids;
        ComponentIds = componentIds;
        DirectMatrixIds = directMatrixIds;
        ClosureMatrixIds = closureMatrixIds;
        AffectedConceptIds = affectedConceptIds;
        AffectedAggregatorCount = affectedAggregatorCount;
        Fingerprint = fingerprint;
    }

    public CatalogItemKind Kind { get; }

    public List<int> Ids { get; }

    public List<int> ComponentIds { get; }

    public List<int> DirectMatrixIds { get; }

    public List<int> ClosureMatrixIds { get; }

    public List<int> AffectedConceptIds { get; }

    public int AffectedAggregatorCount { get; }

    /// <summary>
    /// Huella estable de la selección y de TODO su conjunto de dependencias. La
    /// previsualización la entrega a la UI y la escritura la revalida dentro de su
    /// transacción: si algo cambió entre confirmar y eliminar, el lote se rechaza.
    /// </summary>
    public string Fingerprint { get; }
}

/// <summary>
/// Estado canónico de una fila del presupuesto que puede alterar el recálculo
/// confirmado: proyecto, identidad, matriz referenciada, rol, posición jerárquica
/// y los valores que alimentan los totales de los agrupadores.
/// </summary>
internal readonly record struct FilaPresupuestoFingerprint(
    int Id,
    int ProyectoId,
    int? MatrizId,
    bool EsAgrupador,
    int Nivel,
    int Orden,
    decimal Cantidad,
    decimal ImporteTotal);

/// <summary>
/// Estado canónico de una matriz del cierre: identidad, proyecto propietario (su
/// precisión gobierna el redondeo), tipo (una auxiliar de tipo Cuadrilla cambia la
/// base de mano de obra de su padre) y costo directo almacenado (entrada cuando la
/// matriz se consume como hoja).
/// </summary>
internal readonly record struct MatrizCalculoFingerprint(
    int Id,
    int? ProyectoId,
    TipoMatriz Tipo,
    decimal CostoDirecto);

/// <summary>
/// Entrada de cálculo de un componente de una matriz del cierre: asociación
/// componente→matriz, tipo, referencias, cantidad y el precio unitario resuelto
/// (o el indicador %MO) del insumo referenciado. Es exactamente lo que consume
/// <see cref="MatrixGraphSnapshotAdapter"/> al recalcular.
/// </summary>
internal readonly record struct ComponenteCalculoFingerprint(
    int Id,
    int MatrizId,
    TipoComponenteMatriz TipoComponente,
    int? MaterialId,
    int? ManoDeObraId,
    int? MaquinariaId,
    int? HerramientaId,
    int? AuxiliarId,
    int Orden,
    decimal Cantidad,
    bool EsPorcentajeManoDeObra,
    decimal? PrecioUnitarioResuelto);

/// <summary>Arista auxiliar del proyecto: matriz padre que consume una auxiliar.</summary>
internal readonly record struct AristaAuxiliarFingerprint(int MatrizId, int AuxiliarId);

/// <summary>
/// Auxiliar referenciada por un componente del cierre pero fuera del cierre
/// ascendente. El evaluador la consume como hoja precalculada: su <c>Tipo</c>
/// (Cuadrilla cambia la base de mano de obra del padre) y su <c>CostoDirecto</c>
/// almacenado son entradas directas del recálculo.
/// </summary>
internal readonly record struct AuxiliarReferenciadoFingerprint(
    int Id,
    int? ProyectoId,
    TipoMatriz Tipo,
    decimal CostoDirecto);

/// <summary>
/// Descubrimiento ÚNICO de la eliminación en lote, compartido por la
/// previsualización (solo lectura) y por el comando (dentro de su transacción).
/// Centralizar el cálculo garantiza que lo confirmado y lo ejecutado describan el
/// mismo conjunto de dependencias y produzcan la misma huella.
/// </summary>
internal static class CatalogBatchDeletionPlanner
{
    /// <summary>
    /// Valida la solicitud sin tocar la base: tipo soportado, lote no vacío, Ids
    /// positivos, herramientas solo en una sesión de proyecto. Devuelve los Ids
    /// normalizados (sin repetir, ordenados) cuando la solicitud es válida.
    /// </summary>
    public static AppError? ValidarSolicitud(
        ProjectSessionInfo session,
        CatalogItemKind kind,
        IReadOnlyList<int>? ids,
        out List<int> idsNormalizados)
    {
        idsNormalizados = new List<int>();

        if (!Enum.IsDefined(kind))
            return new AppError(AppErrorCode.NotSupported, "El tipo de insumo solicitado no está soportado.");

        if (ids is null || ids.Count == 0)
            return new AppError(AppErrorCode.Validation, "Seleccione al menos un insumo para eliminar.");

        if (ids.Any(id => id <= 0))
            return new AppError(AppErrorCode.Validation, "La selección contiene identificadores inválidos.");

        if (kind == CatalogItemKind.Herramienta && session.Project.IsMasterCatalog)
        {
            return new AppError(
                AppErrorCode.NotSupported,
                "Las herramientas pertenecen a un proyecto; no se pueden eliminar desde el catálogo maestro.");
        }

        // Un Id repetido en la selección no debe eliminar dos veces ni contar dos
        // veces el impacto: el lote trabaja sobre el conjunto exacto de Ids.
        idsNormalizados = ids.Distinct().OrderBy(id => id).ToList();
        return null;
    }

    /// <summary>
    /// Calcula el plan de eliminación con consultas de solo lectura. No abre
    /// transacción, no muta entidades ni guarda: es seguro llamarlo desde la
    /// previsualización y desde la transacción de escritura.
    /// </summary>
    public static async Task<(AppError? Error, CatalogBatchDeletionPlan? Plan)> PlanificarAsync(
        SOPROContext context,
        ProjectSessionInfo session,
        CatalogItemKind kind,
        IReadOnlyList<int>? ids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(session);

        var errorSolicitud = ValidarSolicitud(session, kind, ids, out var idsNormalizados);
        if (errorSolicitud is not null) return (errorSolicitud, null);

        int? proyectoId = session.Project.ProjectId;

        var filas = await CargarFilasAsync(context, kind, idsNormalizados, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // Validación de Ids exactos: si falta uno solo (inexistente, de otro
        // proyecto o ya eliminado) el lote no se aplica parcialmente.
        if (filas.Count != idsNormalizados.Count)
            return (NotFound(), null);

        if (filas.Any(fila => fila.ProyectoId != proyectoId))
        {
            // Filas maestras en una sesión de proyecto (o filas de proyecto en la
            // sesión maestra): el lote mezcla alcances y se rechaza completo para
            // no borrar catálogo maestro desde un proyecto ni al revés.
            if (!proyectoId.HasValue)
            {
                return (new AppError(
                    AppErrorCode.Conflict,
                    "El catálogo maestro no puede eliminar insumos que pertenecen a un proyecto."), null);
            }

            if (filas.Any(fila => !fila.ProyectoId.HasValue))
            {
                return (new AppError(
                    AppErrorCode.Conflict,
                    "El lote mezcla insumos del proyecto con filas del catálogo maestro."), null);
            }

            // Filas de otro proyecto: mismo mensaje que un Id inexistente para no
            // revelar la existencia de catálogos ajenos.
            return (NotFound(), null);
        }

        var componentesCrudos = await ComponentesDelTipo(context, kind, idsNormalizados)
            .AsNoTracking()
            .Select(c => new { c.Id, c.MatrizId, c.Matriz.ProyectoId })
            .ToListAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        bool referenciasExternas = proyectoId.HasValue
            ? componentesCrudos.Any(c => c.ProyectoId != proyectoId.Value)
            : componentesCrudos.Any(c => c.ProyectoId != null);

        if (referenciasExternas)
        {
            return (new AppError(
                AppErrorCode.Conflict,
                "No se pueden eliminar los insumos seleccionados: están referenciados en matrices de otro proyecto."), null);
        }

        var componentesLocales = proyectoId.HasValue
            ? componentesCrudos.Where(c => c.ProyectoId == proyectoId.Value).ToList()
            : componentesCrudos.Where(c => c.ProyectoId == null).ToList();

        var componenteIds = componentesLocales
            .Select(c => c.Id)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var matricesDirectas = componentesLocales
            .Select(c => c.MatrizId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        // Mismo descubrimiento de cierre que usa la propagación: matrices afectadas
        // más todos sus ascendientes auxiliares del proyecto, sin repetir.
        var cierre = matricesDirectas.Count == 0
            ? new HashSet<int>()
            : PricePropagationService.CalcularCierreAscendente(context, matricesDirectas, proyectoId);
        var cierreIds = cierre.OrderBy(id => id).ToList();

        // El catálogo maestro no tiene proyecto: la propagación canónica no recalcula
        // matrices sin proyecto (RecalcularConMotor exige los decimales del proyecto),
        // así que eliminar un insumo en uso dejaría CostoDirecto obsoleto. Se rechaza
        // el lote completo antes de confirmar o escribir; la previsualización y el
        // comando comparten el plan.
        if (!proyectoId.HasValue && matricesDirectas.Count > 0)
        {
            return (new AppError(
                AppErrorCode.Conflict,
                "No se pueden eliminar los insumos seleccionados desde el catálogo maestro: " +
                "están en uso en matrices sin proyecto y su recálculo no está soportado."), null);
        }

        var conceptos = new List<int>();
        var filasPresupuesto = new List<FilaPresupuestoFingerprint>();
        var proyectosAfectados = new List<Proyecto>();
        int agrupadores = 0;
        string estadoCalculo = string.Empty;

        if (proyectoId.HasValue && cierreIds.Count > 0)
        {
            // El recálculo usa la precisión del proyecto PROPIETARIO de cada matriz del
            // cierre y actualiza las hojas y agrupadores de esos mismos proyectos. En
            // datos soportados el cierre es del proyecto de la sesión; una referencia
            // auxiliar a otro proyecto se cubre igual (conservador).
            var proyectosDelCierre = await context.Matrices
                .AsNoTracking()
                .Where(m => cierreIds.Contains(m.Id) && m.ProyectoId != null)
                .Select(m => m.ProyectoId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (!proyectosDelCierre.Contains(proyectoId.Value))
                proyectosDelCierre.Add(proyectoId.Value);

            // La propagación muta las hojas que referencian el cierre y TODOS los
            // agrupadores de esos proyectos; además, el total de cada agrupador depende
            // del orden, la jerarquía y el ImporteTotal de cualquier fila de su bloque
            // (incluidas las hojas sin matriz). La huella debe cubrir ese estado
            // completo, no solo la identidad de las filas: reasignar una hoja a otra
            // matriz del cierre, moverla de nivel/orden o editar un importe cambia el
            // resultado sin cambiar los números que vería el usuario.
            var filasPresupuestoCrudas = await context.ConceptosPresupuesto
                .AsNoTracking()
                .Where(c => proyectosDelCierre.Contains(c.ProyectoId))
                .Select(c => new
                {
                    c.Id,
                    c.ProyectoId,
                    c.MatrizId,
                    c.EsAgrupador,
                    c.Nivel,
                    c.Orden,
                    c.Cantidad,
                    c.ImporteTotal
                })
                .ToListAsync(cancellationToken);

            filasPresupuesto = filasPresupuestoCrudas
                .Select(f => new FilaPresupuestoFingerprint(
                    f.Id, f.ProyectoId, f.MatrizId, f.EsAgrupador, f.Nivel, f.Orden, f.Cantidad, f.ImporteTotal))
                .OrderBy(f => f.ProyectoId)
                .ThenBy(f => f.Id)
                .ToList();

            conceptos = filasPresupuesto
                .Where(f => f.MatrizId.HasValue && cierreIds.Contains(f.MatrizId.Value))
                .Select(f => f.Id)
                .ToList();

            agrupadores = filasPresupuesto.Count(f => f.EsAgrupador);

            // Los porcentajes y la precisión de cada proyecto afectado alimentan el
            // PrecioUnitario y los redondeos que la propagación reescribe; también
            // forman parte del estado confirmado.
            proyectosAfectados = await context.Proyectos
                .AsNoTracking()
                .Where(p => proyectosDelCierre.Contains(p.Id))
                .ToListAsync(cancellationToken);

            if (proyectosAfectados.Count != proyectosDelCierre.Count)
            {
                return (new AppError(
                    AppErrorCode.NotFound,
                    "No se encontró el proyecto propietario de una matriz del cierre."), null);
            }

            // Entradas de cálculo de TODO el cierre: aunque los Ids de componentes y
            // matrices no cambien, mover un componente entre matrices afectadas,
            // repuntar una arista auxiliar o cambiar el precio de un insumo altera lo
            // que la propagación va a recalcular.
            estadoCalculo = await CargarEstadoCalculoAsync(
                context, proyectoId.Value, cierreIds, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var fingerprint = CrearHuella(
            proyectoId, kind, idsNormalizados, componenteIds, matricesDirectas, cierreIds,
            filasPresupuesto, proyectosAfectados, estadoCalculo);

        return (null, new CatalogBatchDeletionPlan(
            kind,
            idsNormalizados,
            componenteIds,
            matricesDirectas,
            cierreIds,
            conceptos,
            agrupadores,
            fingerprint));
    }

    /// <summary>
    /// Componentes que referencian alguno de los Ids por la FK del tipo pedido. No
    /// se filtra por <see cref="ComponenteMatriz.TipoComponente"/>: la restricción
    /// real de la base es la FK, y un dato malformado con el tipo equivocado debe
    /// bloquear la eliminación igual que uno bien formado.
    /// </summary>
    public static IQueryable<ComponenteMatriz> ComponentesDelTipo(
        SOPROContext context,
        CatalogItemKind kind,
        List<int> ids)
        => kind switch
        {
            CatalogItemKind.Material => context.ComponentesMatriz
                .Where(c => c.MaterialId.HasValue && ids.Contains(c.MaterialId.Value)),
            CatalogItemKind.ManoDeObra => context.ComponentesMatriz
                .Where(c => c.ManoDeObraId.HasValue && ids.Contains(c.ManoDeObraId.Value)),
            CatalogItemKind.Herramienta => context.ComponentesMatriz
                .Where(c => c.HerramientaId.HasValue && ids.Contains(c.HerramientaId.Value)),
            _ => context.ComponentesMatriz
                .Where(c => c.MaquinariaId.HasValue && ids.Contains(c.MaquinariaId.Value)),
        };

    private static async Task<List<(int Id, int? ProyectoId)>> CargarFilasAsync(
        SOPROContext context,
        CatalogItemKind kind,
        List<int> ids,
        CancellationToken cancellationToken)
    {
        switch (kind)
        {
            case CatalogItemKind.Material:
            {
                var filas = await context.Materiales
                    .AsNoTracking()
                    .Where(m => ids.Contains(m.Id))
                    .Select(m => new { m.Id, m.ProyectoId })
                    .ToListAsync(cancellationToken);
                return filas.Select(f => (f.Id, f.ProyectoId)).ToList();
            }

            case CatalogItemKind.ManoDeObra:
            {
                var filas = await context.ManoDeObra
                    .AsNoTracking()
                    .Where(m => ids.Contains(m.Id))
                    .Select(m => new { m.Id, m.ProyectoId })
                    .ToListAsync(cancellationToken);
                return filas.Select(f => (f.Id, f.ProyectoId)).ToList();
            }

            case CatalogItemKind.Herramienta:
            {
                var filas = await context.Herramientas
                    .AsNoTracking()
                    .Where(h => ids.Contains(h.Id))
                    .Select(h => new { h.Id, ProyectoId = (int?)h.ProyectoId })
                    .ToListAsync(cancellationToken);
                return filas.Select(f => (f.Id, f.ProyectoId)).ToList();
            }

            default:
            {
                var filas = await context.Maquinaria
                    .AsNoTracking()
                    .Where(m => ids.Contains(m.Id))
                    .Select(m => new { m.Id, m.ProyectoId })
                    .ToListAsync(cancellationToken);
                return filas.Select(f => (f.Id, f.ProyectoId)).ToList();
            }
        }
    }

    private static AppError NotFound() => new(
        AppErrorCode.NotFound,
        "No se encontraron todos los insumos seleccionados en el catálogo de la sesión.");

    /// <summary>
    /// Serializa el estado de cálculo de TODO el cierre con las mismas entradas que
    /// consume <see cref="MatrixComponentCalculationService.Recalculate"/> a través de
    /// <see cref="MatrixGraphSnapshotAdapter"/>: por matriz su tipo y costo
    /// almacenado; por componente de cualquier matriz del cierre su asociación
    /// componente→matriz, tipo, referencias, orden, cantidad y el precio unitario
    /// resuelto (o el indicador %MO) del insumo referenciado; las aristas auxiliares
    /// del proyecto que forman el cierre; y las auxiliares referenciadas por el cierre
    /// pero fuera de él, que el evaluador consume como hojas con su tipo y costo
    /// almacenado (sin recursión en sus componentes).
    /// </summary>
    private static async Task<string> CargarEstadoCalculoAsync(
        SOPROContext context,
        int proyectoId,
        List<int> cierreIds,
        CancellationToken cancellationToken)
    {
        var matrices = await context.Matrices
            .AsNoTracking()
            .Where(m => cierreIds.Contains(m.Id))
            .Select(m => new { m.Id, m.ProyectoId, m.Tipo, m.CostoDirecto })
            .ToListAsync(cancellationToken);

        var componentes = await context.ComponentesMatriz
            .AsNoTracking()
            .Where(c => cierreIds.Contains(c.MatrizId))
            .Select(c => new
            {
                c.Id,
                c.MatrizId,
                c.TipoComponente,
                c.Cantidad,
                c.Orden,
                c.MaterialId,
                c.ManoDeObraId,
                c.MaquinariaId,
                c.HerramientaId,
                c.AuxiliarId,
                PrecioMaterial = c.Material != null ? c.Material.PrecioUnitario : (decimal?)null,
                SalarioManoDeObra = c.ManoDeObra != null ? c.ManoDeObra.SalarioReal : (decimal?)null,
                UnidadManoDeObra = c.ManoDeObra != null ? c.ManoDeObra.Unidad : null,
                CostoMaquinaria = c.Maquinaria != null ? c.Maquinaria.CostoHorario : (decimal?)null,
                PrecioHerramienta = c.Herramienta != null ? c.Herramienta.PrecioUnitario : (decimal?)null,
                UnidadHerramienta = c.Herramienta != null ? c.Herramienta.Unidad : null
            })
            .ToListAsync(cancellationToken);

        var aristas = await context.ComponentesMatriz
            .AsNoTracking()
            .Where(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar && c.AuxiliarId != null)
            .Where(c => c.Matriz.ProyectoId == proyectoId)
            .Select(c => new { c.MatrizId, c.AuxiliarId })
            .ToListAsync(cancellationToken);

        // El evaluador consume cada auxiliar como hoja con su costo almacenado, sin
        // recursión: una auxiliar referenciada por el cierre pero fuera del cierre
        // ascendente (una descendiente no afectada) no se recalcula, pero su Tipo y
        // CostoDirecto entran directo en el cálculo de su padre. Deben formar parte de
        // la huella aunque sus Ids no estén en el cierre.
        var auxiliaresReferenciados = componentes
            .Where(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar && c.AuxiliarId.HasValue)
            .Select(c => c.AuxiliarId!.Value)
            .Distinct()
            .Where(id => !cierreIds.Contains(id))
            .ToList();

        var auxiliaresFueraDelCierre = auxiliaresReferenciados.Count == 0
            ? new List<AuxiliarReferenciadoFingerprint>()
            : (await context.Matrices
                .AsNoTracking()
                .Where(m => auxiliaresReferenciados.Contains(m.Id))
                .Select(m => new { m.Id, m.ProyectoId, m.Tipo, m.CostoDirecto })
                .ToListAsync(cancellationToken))
                .Select(m => new AuxiliarReferenciadoFingerprint(m.Id, m.ProyectoId, m.Tipo, m.CostoDirecto))
                .OrderBy(m => m.Id)
                .ToList();

        cancellationToken.ThrowIfCancellationRequested();

        var texto = new StringBuilder();

        texto.Append("e");
        foreach (var arista in aristas.OrderBy(a => a.MatrizId).ThenBy(a => a.AuxiliarId))
        {
            texto.Append('\n')
                .Append(EnteroCanonico(arista.MatrizId)).Append(';')
                .Append(IdONulo(arista.AuxiliarId));
        }

        texto.Append("|g");
        foreach (var matriz in matrices.OrderBy(m => m.Id))
        {
            texto.Append('\n')
                .Append(EnteroCanonico(matriz.Id)).Append(';')
                .Append(IdONulo(matriz.ProyectoId)).Append(';')
                .Append(EnteroCanonico((int)matriz.Tipo)).Append(';')
                .Append(DecimalCanonico(matriz.CostoDirecto));
        }

        texto.Append("|a");
        foreach (var auxiliar in auxiliaresFueraDelCierre)
        {
            texto.Append('\n')
                .Append(EnteroCanonico(auxiliar.Id)).Append(';')
                .Append(IdONulo(auxiliar.ProyectoId)).Append(';')
                .Append(EnteroCanonico((int)auxiliar.Tipo)).Append(';')
                .Append(DecimalCanonico(auxiliar.CostoDirecto));
        }

        texto.Append("|h");
        foreach (var componente in componentes.OrderBy(c => c.Id))
        {
            var (esPorcentajeManoDeObra, precioUnitario) = ResolverEntradaCalculo(
                componente.TipoComponente,
                componente.PrecioMaterial,
                componente.SalarioManoDeObra,
                componente.UnidadManoDeObra,
                componente.CostoMaquinaria,
                componente.PrecioHerramienta,
                componente.UnidadHerramienta);

            texto.Append('\n')
                .Append(EnteroCanonico(componente.Id)).Append(';')
                .Append(EnteroCanonico(componente.MatrizId)).Append(';')
                .Append(EnteroCanonico((int)componente.TipoComponente)).Append(';')
                .Append(IdONulo(componente.MaterialId)).Append(';')
                .Append(IdONulo(componente.ManoDeObraId)).Append(';')
                .Append(IdONulo(componente.MaquinariaId)).Append(';')
                .Append(IdONulo(componente.HerramientaId)).Append(';')
                .Append(IdONulo(componente.AuxiliarId)).Append(';')
                .Append(EnteroCanonico(componente.Orden)).Append(';')
                .Append(DecimalCanonico(componente.Cantidad)).Append(';')
                .Append(esPorcentajeManoDeObra ? "1" : "0").Append(';')
                .Append(precioUnitario is { } valor ? DecimalCanonico(valor) : "null");
        }

        return texto.ToString();
    }

    private static (bool EsPorcentajeManoDeObra, decimal? PrecioUnitario) ResolverEntradaCalculo(
        TipoComponenteMatriz tipo,
        decimal? precioMaterial,
        decimal? salarioManoDeObra,
        string? unidadManoDeObra,
        decimal? costoMaquinaria,
        decimal? precioHerramienta,
        string? unidadHerramienta)
        => tipo switch
        {
            TipoComponenteMatriz.Material => (false, precioMaterial),
            TipoComponenteMatriz.ManoDeObra => (EsPorcentajeMo(unidadManoDeObra), salarioManoDeObra),
            TipoComponenteMatriz.Maquinaria => (false, costoMaquinaria),
            TipoComponenteMatriz.Herramienta => (EsPorcentajeMo(unidadHerramienta), precioHerramienta),
            _ => (false, null)
        };

    private static bool EsPorcentajeMo(string? unidad)
        => string.Equals(unidad?.Trim(), "%MO", StringComparison.OrdinalIgnoreCase);

    private static string EnteroCanonico(int valor) => valor.ToString(CultureInfo.InvariantCulture);

    private static string IdONulo(int? valor) => valor?.ToString(CultureInfo.InvariantCulture) ?? "null";

    /// <summary>
    /// Huella canónica (SHA-256 en hexadecimal) de la selección y de TODO el estado
    /// que el recálculo consume o muta: componentes y matrices del cierre, el estado
    /// completo de cada fila del presupuesto del proyecto (identidad, matriz, rol,
    /// nivel, orden, cantidad e importe), las entradas de cálculo de todo el cierre
    /// (asociación componente→matriz, tipo, referencias, cantidad y precio resuelto,
    /// tipo/costo de cada matriz y aristas auxiliares) y los parámetros de
    /// precio/precisión del proyecto. Se serializa con cultura invariante y marcador
    /// explícito de nulos: dos estados equivalentes producen la misma huella y
    /// cualquier cambio que altere el recálculo confirmado la cambia.
    /// </summary>
    private static string CrearHuella(
        int? proyectoId,
        CatalogItemKind kind,
        IReadOnlyList<int> ids,
        IReadOnlyList<int> componenteIds,
        IReadOnlyList<int> matricesDirectas,
        IReadOnlyList<int> cierre,
        IReadOnlyList<FilaPresupuestoFingerprint> filasPresupuesto,
        IReadOnlyList<Proyecto> proyectos,
        string estadoCalculo)
    {
        var texto = new StringBuilder();

        texto.Append("v4");
        texto.Append("|pr").Append(proyectoId?.ToString(CultureInfo.InvariantCulture) ?? "maestro");
        texto.Append("|k").Append(((int)kind).ToString(CultureInfo.InvariantCulture));
        texto.Append("|i").Append(string.Join(",", ids));
        texto.Append("|c").Append(string.Join(",", componenteIds));
        texto.Append("|m").Append(string.Join(",", matricesDirectas));
        texto.Append("|x").Append(string.Join(",", cierre));

        texto.Append("|r");
        foreach (var fila in filasPresupuesto)
            texto.Append('\n').Append(SerializarFilaPresupuesto(fila));

        texto.Append("|q").Append(proyectos.Count == 0 ? "maestro" : SerializarParametrosProyecto(proyectos));

        texto.Append("|s").Append(estadoCalculo);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString())));
    }

    private static string SerializarFilaPresupuesto(FilaPresupuestoFingerprint fila)
        => string.Join(
            ";",
            fila.Id.ToString(CultureInfo.InvariantCulture),
            fila.ProyectoId.ToString(CultureInfo.InvariantCulture),
            fila.MatrizId?.ToString(CultureInfo.InvariantCulture) ?? "null",
            fila.EsAgrupador ? "1" : "0",
            fila.Nivel.ToString(CultureInfo.InvariantCulture),
            fila.Orden.ToString(CultureInfo.InvariantCulture),
            DecimalCanonico(fila.Cantidad),
            DecimalCanonico(fila.ImporteTotal));

    /// <summary>
    /// Parámetros de cada proyecto afectado que alimentan el PrecioUnitario y los
    /// redondeos del recálculo (<see cref="BudgetPricingService.CalculateUnitPrice"/>
    /// y <see cref="CalculationEngineFactory.FromProyecto"/>). El modo de cálculo se
    /// normaliza porque su comparación es insensible a mayúsculas.
    /// </summary>
    private static string SerializarParametrosProyecto(IReadOnlyList<Proyecto> proyectos)
    {
        var texto = new StringBuilder();

        foreach (var proyecto in proyectos.OrderBy(p => p.Id))
        {
            texto.Append('\n')
                .Append(proyecto.Id.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(DecimalCanonico(proyecto.PorcentajeIndirectosCentral)).Append(';')
                .Append(DecimalCanonico(proyecto.PorcentajeIndirectosCampo)).Append(';')
                .Append(DecimalCanonico(proyecto.PorcentajeFinanciamiento)).Append(';')
                .Append(DecimalCanonico(proyecto.PorcentajeUtilidad)).Append(';')
                .Append(DecimalCanonico(proyecto.PorcentajeCargosAdicionales)).Append(';')
                .Append((proyecto.ModoCalculoPorcentajes ?? "Acumulables").ToLowerInvariant()).Append(';')
                .Append(proyecto.DecimalesCantidad.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(proyecto.DecimalesImporte.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(proyecto.DecimalesPorcentaje.ToString(CultureInfo.InvariantCulture));
        }

        return texto.ToString();
    }

    /// <summary>
    /// Serialización invariante de un decimal sin ceros finales ni notación
    /// científica: 10.00 y 10 producen el mismo texto y cualquier cambio real de
    /// valor produce uno distinto.
    /// </summary>
    private static string DecimalCanonico(decimal valor)
        => valor == 0m
            ? "0"
            : valor.ToString(FormatoDecimalCanonico, CultureInfo.InvariantCulture);

    private static readonly string FormatoDecimalCanonico = "0." + new string('#', 28);
}
