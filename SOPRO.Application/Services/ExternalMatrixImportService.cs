using Microsoft.EntityFrameworkCore;
using Sopro.Calculation;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  [N5-12] Único motor del servicio: RoundAmount con las tres precisiones  ║
    // ║          del proyecto destino. [N7-1d] El post-orden propio              ║
    // ║          (RecalcularÁrbol) se sustituye por la ruta canónica:            ║
    // ║          MatrixGraphOrderService.OrdenTopologico + PricePropagation       ║
    // ║          Service.RecalcularConMotor; un ciclo en el grafo importado      ║
    // ║          revierte la importación con diagnóstico de ruta.                ║
    // ║          El Rendimiento de maquinaria conserva Math.Round 5 por contrato.║
    // ║          CRUD/copia de importación intactos.                             ║
    // ╚══════════════════════════════════════════════════════════════════════════╝
    public sealed class ExternalMatrixImportService
    {
        private readonly IProjectDbContextFactory _dbContextFactory;

        public ExternalMatrixImportService(IProjectDbContextFactory? dbContextFactory = null)
        {
            _dbContextFactory = dbContextFactory ?? new ProjectDbContextFactory();
        }

        public ExternalProjectMatrixLoadResult LoadExternalMatrices(string projectPath, TipoMatriz? tipo = null)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentException("Debe indicar la ruta del proyecto externo.", nameof(projectPath));
            if (!File.Exists(projectPath))
                throw new FileNotFoundException("No se encontró el proyecto seleccionado.", projectPath);

            // N4-3: el contexto del proyecto externo se abre por operación con la fábrica.
            using var externalContext = _dbContextFactory.Create(projectPath);
            SchemaManager.EnsureCurrentSchema(externalContext);

            var projectName = externalContext.Proyectos
                .AsNoTracking()
                .OrderBy(p => p.Id)
                .Select(p => p.Nombre)
                .FirstOrDefault() ?? Path.GetFileNameWithoutExtension(projectPath);

            var matrices = externalContext.Matrices
                .AsNoTracking()
                .Where(m => !tipo.HasValue || m.Tipo == tipo.Value)
                .OrderBy(m => m.Clave)
                .Select(m => new ExternalProjectMatrixOption
                {
                    MatrixId = m.Id,
                    ProjectName = projectName,
                    ProjectPath = projectPath,
                    Clave = m.Clave,
                    Descripcion = m.Descripcion,
                    Unidad = m.Unidad,
                    CostoDirecto = m.CostoDirecto,
                    Tipo = m.Tipo
                })
                .ToList();

            return new ExternalProjectMatrixLoadResult
            {
                ProjectName = projectName,
                ProjectPath = projectPath,
                Matrices = matrices
            };
        }

        public ExternalProjectMatrixLoadResult LoadExternalApus(string projectPath)
        {
            return LoadExternalMatrices(projectPath, TipoMatriz.APU);
        }

        public ExternalMatrixImportPreview BuildPreview(SOPROContext currentContext, int currentProjectId, string projectPath, int externalMatrixId)
        {
            using var externalContext = _dbContextFactory.Create(projectPath);
            SchemaManager.EnsureCurrentSchema(externalContext);
            var graph = LoadGraph(externalContext, externalMatrixId);

            // [N7-1e] El grafo fuente debe ser un DAG: se diagnostica aquí, con ruta,
            // antes de calcular el preview (y, en importación, antes de abrir
            // transacción o escribir nada). Antes un ciclo fallaba genéricamente en la
            // copia de componentes al perder la referencia aun-no-mapeada.
            ValidarAciclicidadDelGrafoFuente(graph);

            var matrixKeys = new HashSet<string>(graph.OrderedMatrices.Select(m => NormalizeKey(m.Clave)));
            var materialKeys = new HashSet<string>(graph.Materials.Values.Select(m => NormalizeKey(m.Clave)));
            var manoKeys = new HashSet<string>(graph.ManoDeObra.Values.Select(m => NormalizeKey(m.Clave)));
            var maquinariaKeys = new HashSet<string>(graph.Maquinarias.Values.Select(m => NormalizeKey(m.Clave)));
            var herramientaKeys = new HashSet<string>(graph.Herramientas.Values.Select(m => NormalizeKey(m.Clave)));

            var currentMatrices = currentContext.Matrices.Where(m => m.ProyectoId == currentProjectId).AsNoTracking().ToList();
            var currentMatrixIds = currentMatrices.Select(m => m.Id).ToHashSet();
            var currentMateriales = currentContext.Materiales.Where(m => m.ProyectoId == currentProjectId).AsNoTracking().ToList();
            var currentMano = currentContext.ManoDeObra.Where(m => m.ProyectoId == currentProjectId).AsNoTracking().ToList();
            var currentMaquinaria = currentContext.Maquinaria.Where(m => m.ProyectoId == currentProjectId).AsNoTracking().ToList();
            var currentHerramientas = currentContext.Herramientas.Where(m => m.ProyectoId == currentProjectId).AsNoTracking().ToList();
            var currentConceptos = currentContext.ConceptosPresupuesto
                .Where(c => c.ProyectoId == currentProjectId && !c.EsAgrupador && c.MatrizId.HasValue)
                .AsNoTracking()
                .Select(c => c.MatrizId!.Value)
                .ToList();
            var currentAuxUses = currentContext.ComponentesMatriz
                .Where(c => c.AuxiliarId.HasValue && currentMatrixIds.Contains(c.MatrizId))
                .AsNoTracking()
                .Select(c => c.AuxiliarId!.Value)
                .ToList();

            var preview = new ExternalMatrixImportPreview
            {
                SourceProjectName = graph.ProjectName,
                MatrixKey = graph.RootMatrix.Clave,
                MatrixDescription = graph.RootMatrix.Descripcion,
                TotalMatrices = graph.OrderedMatrices.Count,
                TotalMateriales = graph.Materials.Count,
                TotalManoDeObra = graph.ManoDeObra.Count,
                TotalMaquinaria = graph.Maquinarias.Count,
                TotalHerramientas = graph.Herramientas.Count,
                TotalBasicos = graph.OrderedMatrices.Count(m => m.Tipo == TipoMatriz.Basico),
                TotalCuadrillas = graph.OrderedMatrices.Count(m => m.Tipo == TipoMatriz.Cuadrilla),
                MaxDepth = CalculateMaxDepth(graph, graph.RootMatrix.Id, 1),
                MatrixConflicts = currentMatrices.Count(m => matrixKeys.Contains(NormalizeKey(m.Clave))),
                MaterialConflicts = currentMateriales.Count(m => materialKeys.Contains(NormalizeKey(m.Clave))),
                ManoDeObraConflicts = currentMano.Count(m => manoKeys.Contains(NormalizeKey(m.Clave))),
                MaquinariaConflicts = currentMaquinaria.Count(m => maquinariaKeys.Contains(NormalizeKey(m.Clave))),
                HerramientaConflicts = currentHerramientas.Count(m => herramientaKeys.Contains(NormalizeKey(m.Clave)))
            };

            preview.DependencyTreeLines.Add($"[APU] {graph.RootMatrix.Clave} - {graph.RootMatrix.Descripcion}");
            BuildTreeLines(graph, graph.RootMatrix.Id, preview.DependencyTreeLines, "  ");

            if (preview.MatrixConflicts > 0) preview.ConflictKeyLines.Add($"Matrices: {string.Join(", ", currentMatrices.Where(m => matrixKeys.Contains(NormalizeKey(m.Clave))).Select(m => m.Clave).OrderBy(x => x).Take(8))}");
            if (preview.MaterialConflicts > 0) preview.ConflictKeyLines.Add($"Materiales: {string.Join(", ", currentMateriales.Where(m => materialKeys.Contains(NormalizeKey(m.Clave))).Select(m => m.Clave).OrderBy(x => x).Take(8))}");
            if (preview.ManoDeObraConflicts > 0) preview.ConflictKeyLines.Add($"Mano de obra: {string.Join(", ", currentMano.Where(m => manoKeys.Contains(NormalizeKey(m.Clave))).Select(m => m.Clave).OrderBy(x => x).Take(8))}");
            if (preview.MaquinariaConflicts > 0) preview.ConflictKeyLines.Add($"Maquinaria: {string.Join(", ", currentMaquinaria.Where(m => maquinariaKeys.Contains(NormalizeKey(m.Clave))).Select(m => m.Clave).OrderBy(x => x).Take(8))}");
            if (preview.HerramientaConflicts > 0) preview.ConflictKeyLines.Add($"Herramientas: {string.Join(", ", currentHerramientas.Where(m => herramientaKeys.Contains(NormalizeKey(m.Clave))).Select(m => m.Clave).OrderBy(x => x).Take(8))}");

            foreach (var matrix in graph.OrderedMatrices.OrderBy(m => m.Tipo).ThenBy(m => m.Clave))
            {
                var existing = currentMatrices.FirstOrDefault(m => NormalizeKey(m.Clave) == NormalizeKey(matrix.Clave));
                if (existing == null)
                {
                    preview.ActionSummaryLines.Add($"[MATRIZ] {matrix.Clave}: se importará nueva.");
                    continue;
                }

                var conceptosUso = currentConceptos.Count(id => id == existing.Id);
                var matricesUso = currentAuxUses.Count(id => id == existing.Id);
                preview.ActionSummaryLines.Add($"[MATRIZ] {matrix.Clave}: conflicto detectado. Mantener ambas creará TEMP-{matrix.Clave}; reemplazar actualizará la definición actual.");
                preview.ImpactSummaryLines.Add($"{matrix.Clave}: usada en {conceptosUso} concepto(s) y {matricesUso} matriz(ces) como auxiliar.");
            }

            AddInsumoActionLines(preview.ActionSummaryLines, "MATERIAL", graph.Materials.Values.Select(x => x.Clave), currentMateriales.Select(x => x.Clave));
            AddInsumoActionLines(preview.ActionSummaryLines, "MANO DE OBRA", graph.ManoDeObra.Values.Select(x => x.Clave), currentMano.Select(x => x.Clave));
            AddInsumoActionLines(preview.ActionSummaryLines, "MAQUINARIA", graph.Maquinarias.Values.Select(x => x.Clave), currentMaquinaria.Select(x => x.Clave));
            AddInsumoActionLines(preview.ActionSummaryLines, "HERRAMIENTA", graph.Herramientas.Values.Select(x => x.Clave), currentHerramientas.Select(x => x.Clave));

            return preview;
        }

        public ExternalMatrixImportResult ImportMatrixTree(SOPROContext currentContext, int currentProjectId, string projectPath, int externalMatrixId, ExternalMatrixImportConflictPolicy conflictPolicy)
        {
            using var externalContext = _dbContextFactory.Create(projectPath);
            SchemaManager.EnsureCurrentSchema(externalContext);
            var graph = LoadGraph(externalContext, externalMatrixId);

            // [N7-1e] Ciclo en el grafo fuente: diagnóstico con ruta antes de tocar BD.
            ValidarAciclicidadDelGrafoFuente(graph);

            var currentProject = currentContext.Proyectos.FirstOrDefault(p => p.Id == currentProjectId)
                ?? throw new InvalidOperationException("No se encontró el proyecto actual.");

            using var tx = currentContext.Database.BeginTransaction();

            var materialMap = new Dictionary<int, int>();
            var manoMap = new Dictionary<int, int>();
            var maquinariaMap = new Dictionary<int, int>();
            var herramientaMap = new Dictionary<int, int>();
            var matrixMap = new Dictionary<int, int>();

            var currentMateriales = currentContext.Materiales.Where(x => x.ProyectoId == currentProjectId).ToList();
            var currentMano = currentContext.ManoDeObra.Where(x => x.ProyectoId == currentProjectId).ToList();
            var currentMaquinaria = currentContext.Maquinaria.Where(x => x.ProyectoId == currentProjectId).ToList();
            var currentHerramientas = currentContext.Herramientas.Where(x => x.ProyectoId == currentProjectId).ToList();
            var currentMatrices = currentContext.Matrices
                .Include(m => m.Componentes)
                .Where(x => x.ProyectoId == currentProjectId)
                .ToList();

            int importedMateriales = 0, importedMano = 0, importedMaquinaria = 0, importedHerramientas = 0, importedMatrices = 0;

            foreach (var material in graph.Materials.Values.OrderBy(x => x.Clave))
            {
                var existing = currentMateriales.FirstOrDefault(x => NormalizeKey(x.Clave) == NormalizeKey(material.Clave));
                if (existing != null && conflictPolicy == ExternalMatrixImportConflictPolicy.ReplaceExisting)
                {
                    ApplyMaterial(existing, material, currentProjectId, graph.ProjectName);
                    materialMap[material.Id] = existing.Id;
                }
                else
                {
                    var clone = CloneMaterial(material, currentProjectId, graph.ProjectName);
                    if (existing != null)
                        clone.Clave = GenerateTempKey(material.Clave, currentMateriales.Select(x => x.Clave));
                    currentContext.Materiales.Add(clone);
                    currentContext.SaveChanges();
                    currentMateriales.Add(clone);
                    materialMap[material.Id] = clone.Id;
                    importedMateriales++;
                }
            }

            foreach (var mano in graph.ManoDeObra.Values.OrderBy(x => x.Clave))
            {
                var existing = currentMano.FirstOrDefault(x => NormalizeKey(x.Clave) == NormalizeKey(mano.Clave));
                if (existing != null && conflictPolicy == ExternalMatrixImportConflictPolicy.ReplaceExisting)
                {
                    ApplyManoDeObra(existing, mano, currentProjectId, graph.ProjectName);
                    manoMap[mano.Id] = existing.Id;
                }
                else
                {
                    var clone = CloneManoDeObra(mano, currentProjectId, graph.ProjectName);
                    if (existing != null)
                        clone.Clave = GenerateTempKey(mano.Clave, currentMano.Select(x => x.Clave));
                    currentContext.ManoDeObra.Add(clone);
                    currentContext.SaveChanges();
                    currentMano.Add(clone);
                    manoMap[mano.Id] = clone.Id;
                    importedMano++;
                }
            }

            foreach (var maquinaria in graph.Maquinarias.Values.OrderBy(x => x.Clave))
            {
                var existing = currentMaquinaria.FirstOrDefault(x => NormalizeKey(x.Clave) == NormalizeKey(maquinaria.Clave));
                if (existing != null && conflictPolicy == ExternalMatrixImportConflictPolicy.ReplaceExisting)
                {
                    ApplyMaquinaria(existing, maquinaria, currentProjectId, graph.ProjectName);
                    maquinariaMap[maquinaria.Id] = existing.Id;
                }
                else
                {
                    var clone = CloneMaquinaria(maquinaria, currentProjectId, graph.ProjectName);
                    if (existing != null)
                        clone.Clave = GenerateTempKey(maquinaria.Clave, currentMaquinaria.Select(x => x.Clave));
                    currentContext.Maquinaria.Add(clone);
                    currentContext.SaveChanges();
                    currentMaquinaria.Add(clone);
                    maquinariaMap[maquinaria.Id] = clone.Id;
                    importedMaquinaria++;
                }
            }

            foreach (var herramienta in graph.Herramientas.Values.OrderBy(x => x.Clave))
            {
                var existing = currentHerramientas.FirstOrDefault(x => NormalizeKey(x.Clave) == NormalizeKey(herramienta.Clave));
                if (existing != null && conflictPolicy == ExternalMatrixImportConflictPolicy.ReplaceExisting)
                {
                    ApplyHerramienta(existing, herramienta, currentProjectId, graph.ProjectName);
                    herramientaMap[herramienta.Id] = existing.Id;
                }
                else
                {
                    var clone = CloneHerramienta(herramienta, currentProjectId, graph.ProjectName);
                    if (existing != null)
                        clone.Clave = GenerateTempKey(herramienta.Clave, currentHerramientas.Select(x => x.Clave));
                    currentContext.Herramientas.Add(clone);
                    currentContext.SaveChanges();
                    currentHerramientas.Add(clone);
                    herramientaMap[herramienta.Id] = clone.Id;
                    importedHerramientas++;
                }
            }

            foreach (var sourceMatrix in graph.OrderedMatrices)
            {
                var existing = currentMatrices.FirstOrDefault(x => NormalizeKey(x.Clave) == NormalizeKey(sourceMatrix.Clave));
                Matriz target;
                if (existing != null && conflictPolicy == ExternalMatrixImportConflictPolicy.ReplaceExisting)
                {
                    target = existing;
                    ApplyMatrix(target, sourceMatrix, currentProjectId, graph.ProjectName);
                    if (target.Componentes.Any())
                    {
                        currentContext.ComponentesMatriz.RemoveRange(target.Componentes);
                        currentContext.SaveChanges();
                    }
                    target.Componentes = new List<ComponenteMatriz>();
                }
                else
                {
                    target = CloneMatrix(sourceMatrix, currentProjectId, graph.ProjectName);
                    if (existing != null)
                        target.Clave = GenerateTempKey(sourceMatrix.Clave, currentMatrices.Select(x => x.Clave));
                    currentContext.Matrices.Add(target);
                    currentContext.SaveChanges();
                    currentMatrices.Add(target);
                    importedMatrices++;
                }

                matrixMap[sourceMatrix.Id] = target.Id;

                var sourceComponents = graph.ComponentsByMatrixId.TryGetValue(sourceMatrix.Id, out var value) ? value : new List<ComponenteMatriz>();
                foreach (var component in sourceComponents.OrderBy(c => c.Orden).ThenBy(c => c.Id))
                {
                    int? materialId = component.MaterialId.HasValue && materialMap.ContainsKey(component.MaterialId.Value) ? materialMap[component.MaterialId.Value] : (int?)null;
                    int? manoId = component.ManoDeObraId.HasValue && manoMap.ContainsKey(component.ManoDeObraId.Value) ? manoMap[component.ManoDeObraId.Value] : (int?)null;
                    int? maquinariaId = component.MaquinariaId.HasValue && maquinariaMap.ContainsKey(component.MaquinariaId.Value) ? maquinariaMap[component.MaquinariaId.Value] : (int?)null;
                    int? herramientaId = component.HerramientaId.HasValue && herramientaMap.ContainsKey(component.HerramientaId.Value) ? herramientaMap[component.HerramientaId.Value] : (int?)null;
                    int? auxiliarId = component.AuxiliarId.HasValue && matrixMap.ContainsKey(component.AuxiliarId.Value) ? matrixMap[component.AuxiliarId.Value] : (int?)null;

                    ValidateImportedReference(component, materialId, manoId, maquinariaId, herramientaId, auxiliarId, sourceMatrix.Clave);

                    var newComponent = new ComponenteMatriz
                    {
                        MatrizId = target.Id,
                        TipoComponente = component.TipoComponente,
                        Cantidad = component.Cantidad,
                        Rendimiento = component.TipoComponente == TipoComponenteMatriz.Maquinaria && component.Cantidad > 0
                            ? Math.Round(1m / component.Cantidad, 5, MidpointRounding.AwayFromZero) : 0m,
                        Importe = component.Importe,
                        Orden = component.Orden,
                        Notas = component.Notas ?? string.Empty,
                        MaterialId = materialId,
                        ManoDeObraId = manoId,
                        MaquinariaId = maquinariaId,
                        HerramientaId = herramientaId,
                        AuxiliarId = auxiliarId
                    };
                    currentContext.ComponentesMatriz.Add(newComponent);
                }

                target.CostoDirecto = sourceMatrix.CostoDirecto;
                target.FechaModificacion = DateTime.Now;
                currentContext.SaveChanges();
            }

            // [O2-AUDITORIA] Recalcular con el motor del proyecto destino.
            // Los Importe de componentes y el CostoDirecto copiados del origen
            // pueden no respetar la precisión de pantalla del destino; se recalculan
            // todos a partir de las cantidades y los precios importados.
            RecalcularConMotorDelProyecto(currentContext, currentProject, graph, matrixMap);
            currentContext.SaveChanges();

            tx.Commit();
            return new ExternalMatrixImportResult
            {
                RootMatrixId = matrixMap[graph.RootMatrix.Id],
                ImportedMatrices = importedMatrices,
                ImportedMateriales = importedMateriales,
                ImportedManoDeObra = importedMano,
                ImportedMaquinaria = importedMaquinaria,
                ImportedHerramientas = importedHerramientas
            };
        }

        private static void RecalcularConMotorDelProyecto(
            SOPROContext currentContext, Proyecto currentProject, MatrixGraph graph,
            Dictionary<int, int> matrixMap)
        {
            // Reutiliza la ruta canónica (PricePropagationService.RecalcularConMotor)
            // que ya implementa la precisión de pantalla por proyecto propietario.
            var matricesImportadas = currentContext.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .Where(m => matrixMap.Values.Contains(m.Id))
                .ToList();

            var matrizRaizId = graph != null && graph.RootMatrix != null
                ? matrixMap.TryGetValue(graph.RootMatrix.Id, out var raiz) ? raiz : 0
                : 0;

            // [N7-1d] Post-orden canónico: orden topológico por dependencias de
            // auxiliares (MatrixGraphOrderService) + recálculo por la ruta compartida,
            // de modo que un auxiliar ya tenga CostoDirecto final cuando su padre lo
            // consuma. Un grafo importado con ciclo lanza diagnóstico con ruta y la
            // transacción revierte la importación (antes se parcializaba en silencio).
            if (matrizRaizId != 0)
                PricePropagationService.RecalcularConMotor(
                    currentContext, MatrixGraphOrderService.OrdenTopologico(matricesImportadas));
            else
                PricePropagationService.RecalcularConMotor(currentContext, matricesImportadas);
        }

        /// <summary>
        /// [N7-1e] El orden topológico canónico es también el verificador de aciclicidad:
        /// si el grafo fuente (alcanzable desde la raíz) tiene un ciclo de referencias
        /// entre matrices, lanza <see cref="InvalidOperationException"/> con la ruta en
        /// lugar de dejar que la copia de componentes pierda la referencia o falle con
        /// un diagnóstico genérico de clave.
        /// </summary>
        private static void ValidarAciclicidadDelGrafoFuente(MatrixGraph graph)
        {
            try
            {
                MatrixGraphOrderService.OrdenTopologico(graph.OrderedMatrices);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Ciclo de matrices detectado", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "El grafo externo contiene un ciclo de referencias entre matrices. " + ex.Message, ex);
            }
        }

        private static MatrixGraph LoadGraph(SOPROContext externalContext, int externalMatrixId)
        {
            var projectName = externalContext.Proyectos
                .AsNoTracking()
                .OrderBy(p => p.Id)
                .Select(p => p.Nombre)
                .FirstOrDefault() ?? Path.GetFileNameWithoutExtension(externalContext.DatabasePath);

            var matrices = externalContext.Matrices
                .AsNoTracking()
                .Include(m => m.Componentes)
                .Where(m => m.ProyectoId != null)
                .ToList()
                .ToDictionary(m => m.Id);

            if (!matrices.ContainsKey(externalMatrixId))
                throw new InvalidOperationException("No se encontró la matriz seleccionada en el proyecto externo.");

            var graph = new MatrixGraph(projectName, matrices[externalMatrixId]);
            var visited = new HashSet<int>();
            VisitMatrix(externalContext, matrices, externalMatrixId, visited, graph);
            return graph;
        }

        private static void VisitMatrix(SOPROContext externalContext, Dictionary<int, Matriz> matrices, int matrixId, HashSet<int> visited, MatrixGraph graph)
        {
            if (!visited.Add(matrixId))
                return;

            var matrix = matrices[matrixId];
            var components = matrix.Componentes.OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList();
            graph.ComponentsByMatrixId[matrixId] = components;

            foreach (var component in components.Where(c => c.AuxiliarId.HasValue))
            {
                if (component.AuxiliarId.HasValue && matrices.ContainsKey(component.AuxiliarId.Value))
                    VisitMatrix(externalContext, matrices, component.AuxiliarId.Value, visited, graph);
            }

            foreach (var component in components)
            {
                if (component.MaterialId.HasValue && !graph.Materials.ContainsKey(component.MaterialId.Value))
                {
                    var item = externalContext.Materiales.AsNoTracking().FirstOrDefault(x => x.Id == component.MaterialId.Value);
                    if (item != null) graph.Materials[item.Id] = item;
                }
                if (component.ManoDeObraId.HasValue && !graph.ManoDeObra.ContainsKey(component.ManoDeObraId.Value))
                {
                    var item = externalContext.ManoDeObra.AsNoTracking().FirstOrDefault(x => x.Id == component.ManoDeObraId.Value);
                    if (item != null) graph.ManoDeObra[item.Id] = item;
                }
                if (component.MaquinariaId.HasValue && !graph.Maquinarias.ContainsKey(component.MaquinariaId.Value))
                {
                    var item = externalContext.Maquinaria.AsNoTracking().FirstOrDefault(x => x.Id == component.MaquinariaId.Value);
                    if (item != null) graph.Maquinarias[item.Id] = item;
                }
                if (component.HerramientaId.HasValue && !graph.Herramientas.ContainsKey(component.HerramientaId.Value))
                {
                    var item = externalContext.Herramientas.AsNoTracking().FirstOrDefault(x => x.Id == component.HerramientaId.Value);
                    if (item != null) graph.Herramientas[item.Id] = item;
                }
            }

            graph.OrderedMatrices.Add(matrix);
        }


        private static void BuildTreeLines(MatrixGraph graph, int matrixId, List<string> lines, string indent)
        {
            if (!graph.ComponentsByMatrixId.TryGetValue(matrixId, out var components))
                return;

            foreach (var component in components.OrderBy(c => c.Orden).ThenBy(c => c.Id))
            {
                if (component.AuxiliarId.HasValue && graph.OrderedMatrices.FirstOrDefault(m => m.Id == component.AuxiliarId.Value) is Matriz aux)
                {
                    var prefix = aux.Tipo == TipoMatriz.Cuadrilla ? "[CUAD]" : "[BAS]";
                    lines.Add($"{indent}{prefix} {aux.Clave} - {aux.Descripcion}");
                    BuildTreeLines(graph, aux.Id, lines, indent + "  ");
                    continue;
                }
                if (component.MaterialId.HasValue && graph.Materials.TryGetValue(component.MaterialId.Value, out var material))
                    lines.Add($"{indent}[MAT] {material.Clave} - {material.Descripcion}");
                else if (component.ManoDeObraId.HasValue && graph.ManoDeObra.TryGetValue(component.ManoDeObraId.Value, out var mano))
                    lines.Add($"{indent}[MO] {mano.Clave} - {mano.Descripcion}");
                else if (component.MaquinariaId.HasValue && graph.Maquinarias.TryGetValue(component.MaquinariaId.Value, out var maq))
                    lines.Add($"{indent}[MAQ] {maq.Clave} - {maq.Descripcion}");
                else if (component.HerramientaId.HasValue && graph.Herramientas.TryGetValue(component.HerramientaId.Value, out var herr))
                    lines.Add($"{indent}[HERR] {herr.Clave} - {herr.Descripcion}");
            }
        }

        private static int CalculateMaxDepth(MatrixGraph graph, int matrixId, int depth)
        {
            return CalculateMaxDepth(graph, matrixId, depth, new HashSet<int>());
        }

        private static int CalculateMaxDepth(MatrixGraph graph, int matrixId, int depth, HashSet<int> visited)
        {
            if (!visited.Add(matrixId))
                return depth;
            if (!graph.ComponentsByMatrixId.TryGetValue(matrixId, out var components))
                return depth;

            var max = depth;
            foreach (var childId in components.Where(c => c.AuxiliarId.HasValue).Select(c => c.AuxiliarId!.Value))
            {
                var childDepth = CalculateMaxDepth(graph, childId, depth + 1, new HashSet<int>(visited));
                if (childDepth > max) max = childDepth;
            }
            return max;
        }


        private static void AddInsumoActionLines(List<string> target, string label, IEnumerable<string?> importedKeys, IEnumerable<string?> currentKeys)
        {
            var currentSet = new HashSet<string>(currentKeys.Where(x => !string.IsNullOrWhiteSpace(x)).Select(NormalizeKey));
            foreach (var key in importedKeys.Where(x => !string.IsNullOrWhiteSpace(x)).OrderBy(x => x))
            {
                var normalized = NormalizeKey(key);
                if (currentSet.Contains(normalized))
                    target.Add($"[{label}] {key}: conflicto detectado. Mantener ambas creará TEMP-{key}; reemplazar actualizará el registro actual.");
                else
                    target.Add($"[{label}] {key}: se importará nuevo.");
            }
        }

        private static string NormalizeKey(string? key)
            => (key ?? string.Empty).Trim().ToUpperInvariant();

        private static string AppendOrigin(string? notes, string projectName)
            => ImportOriginStampService.AppendStamp(notes, projectName);

        private static string GenerateTempKey(string? originalKey, IEnumerable<string?> existingKeys)
        {
            var normalizedExisting = new HashSet<string>(existingKeys.Where(x => !string.IsNullOrWhiteSpace(x)).Select(NormalizeKey));
            var clean = string.IsNullOrWhiteSpace(originalKey) ? "MATRIZ" : originalKey!.Trim();
            var baseCandidate = $"TEMP-{clean}";
            var candidate = baseCandidate;
            var index = 2;
            while (normalizedExisting.Contains(NormalizeKey(candidate)))
            {
                candidate = $"{baseCandidate}-{index}";
                index++;
            }
            if (candidate.Length > 50)
                candidate = candidate.Substring(0, 50);
            return candidate.ToUpperInvariant();
        }

        private static Material CloneMaterial(Material source, int currentProjectId, string projectName)
        {
            var clone = new Material();
            ApplyMaterial(clone, source, currentProjectId, projectName);
            return clone;
        }

        private static void ApplyMaterial(Material target, Material source, int currentProjectId, string projectName)
        {
            target.Clave = (source.Clave ?? string.Empty).Trim().ToUpperInvariant();
            target.Descripcion = source.Descripcion;
            target.Unidad = source.Unidad;
            target.PrecioUnitario = source.PrecioUnitario;
            target.ProyectoId = currentProjectId;
            target.Origen = OrigenInsumo.Proyecto;
            target.MaterialMaestroId = null;
            target.Notas = AppendOrigin(source.Notas, projectName);
            target.FechaModificacion = DateTime.Now;
        }

        private static ManoDeObra CloneManoDeObra(ManoDeObra source, int currentProjectId, string projectName)
        {
            var clone = new ManoDeObra();
            ApplyManoDeObra(clone, source, currentProjectId, projectName);
            return clone;
        }

        private static void ApplyManoDeObra(ManoDeObra target, ManoDeObra source, int currentProjectId, string projectName)
        {
            target.Clave = (source.Clave ?? string.Empty).Trim().ToUpperInvariant();
            target.Descripcion = source.Descripcion;
            target.Unidad = source.Unidad;
            target.SalarioBase = source.SalarioBase;
            target.FactorSalarioReal = source.FactorSalarioReal;
            target.SalarioReal = source.SalarioReal;
            target.ProyectoId = currentProjectId;
            target.Origen = OrigenInsumo.Proyecto;
            target.ManoDeObraMaestraId = null;
            target.Notas = AppendOrigin(source.Notas, projectName);
            target.FechaModificacion = DateTime.Now;
        }

        private static Maquinaria CloneMaquinaria(Maquinaria source, int currentProjectId, string projectName)
        {
            var clone = new Maquinaria();
            ApplyMaquinaria(clone, source, currentProjectId, projectName);
            return clone;
        }

        private static void ApplyMaquinaria(Maquinaria target, Maquinaria source, int currentProjectId, string projectName)
        {
            target.Clave = (source.Clave ?? string.Empty).Trim().ToUpperInvariant();
            target.Descripcion = source.Descripcion;
            target.PotenciaNominal = source.PotenciaNominal;
            target.TipoCombustible = source.TipoCombustible;
            target.ValorAdquisicion = source.ValorAdquisicion;
            target.ValorLlantas = source.ValorLlantas;
            target.ValorPiezasEspeciales = source.ValorPiezasEspeciales;
            target.FactorRescate = source.FactorRescate;
            target.VidaEconomica = source.VidaEconomica;
            target.TasaInteres = source.TasaInteres;
            target.HorasEfectivasAnio = source.HorasEfectivasAnio;
            target.PrimaSeguro = source.PrimaSeguro;
            target.FactorMantenimiento = source.FactorMantenimiento;
            target.CantidadCombustible = source.CantidadCombustible;
            target.PrecioCombustible = source.PrecioCombustible;
            target.CantidadAceite = source.CantidadAceite;
            target.PrecioAceite = source.PrecioAceite;
            target.NumeroLlantas = source.NumeroLlantas;
            target.VidaEconomicaLlantas = source.VidaEconomicaLlantas;
            target.VidaPiezasEspeciales = source.VidaPiezasEspeciales;
            target.SalarioOperador = source.SalarioOperador;
            target.FactorSalarioReal = source.FactorSalarioReal;
            target.HorasEfectivasTurno = source.HorasEfectivasTurno;
            target.CostoHorario = source.CostoHorario;
            target.EsCostoCalculado = source.EsCostoCalculado;
            target.ProyectoId = currentProjectId;
            target.Origen = OrigenInsumo.Proyecto;
            target.MaquinariaMaestraId = null;
            target.Notas = AppendOrigin(source.Notas, projectName);
            target.FechaModificacion = DateTime.Now;
            target.FechaCalculoCosto = source.FechaCalculoCosto;
        }

        private static Herramienta CloneHerramienta(Herramienta source, int currentProjectId, string projectName)
        {
            var clone = new Herramienta();
            ApplyHerramienta(clone, source, currentProjectId, projectName);
            return clone;
        }

        private static void ApplyHerramienta(Herramienta target, Herramienta source, int currentProjectId, string projectName)
        {
            target.Clave = (source.Clave ?? string.Empty).Trim().ToUpperInvariant();
            target.Descripcion = source.Descripcion;
            target.Unidad = source.Unidad;
            target.PrecioUnitario = source.PrecioUnitario;
            target.ProyectoId = currentProjectId;
            target.Origen = OrigenInsumo.Proyecto;
            target.Notas = AppendOrigin(source.Notas, projectName);
            target.FechaModificacion = DateTime.Now;
        }


        private static void ValidateImportedReference(
            ComponenteMatriz component,
            int? materialId,
            int? manoId,
            int? maquinariaId,
            int? herramientaId,
            int? auxiliarId,
            string? matrixKey)
        {
            if (component.MaterialId.HasValue && !materialId.HasValue)
                throw new InvalidOperationException($"No se pudo resolver el material importado para la matriz '{matrixKey}'.");

            if (component.ManoDeObraId.HasValue && !manoId.HasValue)
                throw new InvalidOperationException($"No se pudo resolver la mano de obra importada para la matriz '{matrixKey}'.");

            if (component.MaquinariaId.HasValue && !maquinariaId.HasValue)
                throw new InvalidOperationException($"No se pudo resolver la maquinaria importada para la matriz '{matrixKey}'.");

            if (component.HerramientaId.HasValue && !herramientaId.HasValue)
                throw new InvalidOperationException($"No se pudo resolver la herramienta importada para la matriz '{matrixKey}'.");

            if (component.AuxiliarId.HasValue && !auxiliarId.HasValue)
                throw new InvalidOperationException($"No se pudo resolver la matriz auxiliar importada para la matriz '{matrixKey}'.");
        }

        private static Matriz CloneMatrix(Matriz source, int currentProjectId, string projectName)
        {
            var clone = new Matriz();
            ApplyMatrix(clone, source, currentProjectId, projectName);
            return clone;
        }

        private static void ApplyMatrix(Matriz target, Matriz source, int currentProjectId, string projectName)
        {
            target.Clave = (source.Clave ?? string.Empty).Trim().ToUpperInvariant();
            target.Descripcion = source.Descripcion;
            target.Unidad = source.Unidad;
            target.Tipo = source.Tipo;
            target.CostoDirecto = source.CostoDirecto;
            target.ProyectoId = currentProjectId;
            target.Origen = OrigenInsumo.Proyecto;
            target.MatrizMaestraId = null;
            target.Notas = AppendOrigin(source.Notas, projectName);
            target.FechaModificacion = DateTime.Now;
        }

        private sealed class MatrixGraph
        {
            public MatrixGraph(string projectName, Matriz rootMatrix)
            {
                ProjectName = projectName;
                RootMatrix = rootMatrix;
            }

            public string ProjectName { get; }
            public Matriz RootMatrix { get; }
            public List<Matriz> OrderedMatrices { get; } = new();
            public Dictionary<int, List<ComponenteMatriz>> ComponentsByMatrixId { get; } = new();
            public Dictionary<int, Material> Materials { get; } = new();
            public Dictionary<int, ManoDeObra> ManoDeObra { get; } = new();
            public Dictionary<int, Maquinaria> Maquinarias { get; } = new();
            public Dictionary<int, Herramienta> Herramientas { get; } = new();
        }
    }
}
