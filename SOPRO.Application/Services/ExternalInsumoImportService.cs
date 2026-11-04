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
    // ║  [N5-13] (Imports externos, parte 2) Motor del servicio: las 3 llamadas ║
    // ║          Multiplicar → Multiply (Importe del componente importado de     ║
    // ║          Material/Maquinaria/Auxiliar) con la precisión del proyecto     ║
    // ║          destino (o 2/2/4 si no hay proyecto). Rendimiento conserva     ║
    // ║          Math.Round 5 por contrato. Copia/CRUD de importación intactos.  ║
    // ╚══════════════════════════════════════════════════════════════════════════╝
    public sealed class ExternalInsumoImportService
    {
        private readonly ExternalMatrixImportService _matrixImportService;
        private readonly IProjectDbContextFactory _dbContextFactory;

        public ExternalInsumoImportService(IProjectDbContextFactory? dbContextFactory = null)
        {
            _dbContextFactory = dbContextFactory ?? new ProjectDbContextFactory();
            _matrixImportService = new ExternalMatrixImportService(dbContextFactory);
        }

        public ExternalProjectInsumoLoadResult LoadExternalInsumos(string projectPath, TipoComponenteMatriz tipoComponente, string? filtro, bool incluirManoDeObraIndividual = true, bool incluirCuadrillas = true)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentException("Debe indicar la ruta del proyecto externo.", nameof(projectPath));
            if (!File.Exists(projectPath))
                throw new FileNotFoundException("No se encontró el proyecto seleccionado.", projectPath);

            // La fuente externa NUNCA se migra: el arrendamiento elige entre lectura
            // directa (esquema actual) o copia temporal migrada (esquema legado).
            using var externalSource = ExternalProjectSource.Open(_dbContextFactory, projectPath);
            var externalContext = externalSource.Context;
            var originalPath = externalSource.OriginalPath;
            var projectName = externalContext.Proyectos
                .AsNoTracking()
                .OrderBy(p => p.Id)
                .Select(p => p.Nombre)
                .FirstOrDefault() ?? Path.GetFileNameWithoutExtension(originalPath);

            filtro = string.IsNullOrWhiteSpace(filtro) ? null : filtro.Trim().ToLowerInvariant();
            var items = new List<ExternalProjectInsumoOption>();

            bool Match(string? clave, string? descripcion)
            {
                if (string.IsNullOrWhiteSpace(filtro)) return true;
                return (clave ?? string.Empty).ToLowerInvariant().Contains(filtro) || (descripcion ?? string.Empty).ToLowerInvariant().Contains(filtro);
            }

            switch (tipoComponente)
            {
                case TipoComponenteMatriz.Material:
                    items.AddRange(externalContext.Materiales.AsNoTracking().OrderBy(x => x.Clave).ToList()
                        .Where(x => Match(x.Clave, x.Descripcion))
                        .Select(x => new ExternalProjectInsumoOption { ItemId = x.Id, ProjectName = projectName, ProjectPath = originalPath, TipoComponente = TipoComponenteMatriz.Material, Clave = x.Clave, Descripcion = x.Descripcion, Unidad = x.Unidad, PrecioUnitario = x.PrecioUnitario, PrecioMostrado = x.PrecioUnitario.ToString("C2") }));
                    break;
                case TipoComponenteMatriz.ManoDeObra:
                    if (incluirManoDeObraIndividual)
                    {
                        items.AddRange(externalContext.ManoDeObra.AsNoTracking().OrderBy(x => x.Clave).ToList()
                            .Where(x => Match(x.Clave, x.Descripcion))
                            .Select(x => new ExternalProjectInsumoOption { ItemId = x.Id, ProjectName = projectName, ProjectPath = originalPath, TipoComponente = TipoComponenteMatriz.ManoDeObra, Tag = "Individual", Clave = x.Clave, Descripcion = x.Descripcion, Unidad = x.Unidad, PrecioUnitario = x.SalarioReal, PrecioMostrado = x.SalarioReal.ToString("C2") }));
                    }
                    if (incluirCuadrillas)
                    {
                        items.AddRange(externalContext.Matrices.AsNoTracking().Where(m => m.Tipo == TipoMatriz.Cuadrilla).OrderBy(x => x.Clave).ToList()
                            .Where(x => Match(x.Clave, x.Descripcion))
                            .Select(x => new ExternalProjectInsumoOption { ItemId = x.Id, ProjectName = projectName, ProjectPath = originalPath, TipoComponente = TipoComponenteMatriz.Auxiliar, Tag = "Cuadrilla", Clave = x.Clave, Descripcion = "👷 " + x.Descripcion, Unidad = x.Unidad, PrecioUnitario = x.CostoDirecto, PrecioMostrado = x.CostoDirecto.ToString("C2") }));
                    }
                    break;
                case TipoComponenteMatriz.Maquinaria:
                    items.AddRange(externalContext.Maquinaria.AsNoTracking().OrderBy(x => x.Clave).ToList()
                        .Where(x => Match(x.Clave, x.Descripcion))
                        .Select(x => new ExternalProjectInsumoOption { ItemId = x.Id, ProjectName = projectName, ProjectPath = originalPath, TipoComponente = TipoComponenteMatriz.Maquinaria, Clave = x.Clave, Descripcion = x.Descripcion, Unidad = "hora", PrecioUnitario = x.CostoHorario, PrecioMostrado = x.CostoHorario.ToString("C2") }));
                    break;
                case TipoComponenteMatriz.Auxiliar:
                    items.AddRange(externalContext.Matrices.AsNoTracking().Where(m => m.Tipo == TipoMatriz.Basico).OrderBy(x => x.Clave).ToList()
                        .Where(x => Match(x.Clave, x.Descripcion))
                        .Select(x => new ExternalProjectInsumoOption { ItemId = x.Id, ProjectName = projectName, ProjectPath = originalPath, TipoComponente = TipoComponenteMatriz.Auxiliar, Clave = x.Clave, Descripcion = x.Descripcion, Unidad = x.Unidad, PrecioUnitario = x.CostoDirecto, PrecioMostrado = x.CostoDirecto.ToString("C4") }));
                    break;
                case TipoComponenteMatriz.Herramienta:
                    items.AddRange(LoadExternalHerramientasBasic(externalSource)
                        .Where(x => Match(x.Clave, x.Descripcion))
                        .Select(x => new ExternalProjectInsumoOption { ItemId = x.Id, ProjectName = projectName, ProjectPath = originalPath, TipoComponente = TipoComponenteMatriz.Herramienta, Clave = x.Clave, Descripcion = x.Descripcion, Unidad = x.Unidad, PrecioUnitario = x.PrecioUnitario, PrecioMostrado = x.PrecioUnitario.ToString("C2") }));
                    break;
            }

            return new ExternalProjectInsumoLoadResult { ProjectName = projectName, ProjectPath = originalPath, Items = items.OrderBy(x => x.Clave).ToList() };
        }

        public ExternalInsumoImportPreview BuildPreview(SOPROContext currentContext, int currentProjectId, string projectPath, IEnumerable<ExternalProjectInsumoOption> selectedItems)
        {
            var list = selectedItems?.ToList() ?? new List<ExternalProjectInsumoOption>();
            var preview = new ExternalInsumoImportPreview { SourceProjectName = list.FirstOrDefault()?.ProjectName ?? string.Empty, SelectedItems = list.Count };
            preview.Items.AddRange(list.Select(item => $"{GetTipoNombre(item)} • {item.Clave} - {item.Descripcion}"));
            var materialKeys = new HashSet<string>();
            var manoKeys = new HashSet<string>();
            var maqKeys = new HashSet<string>();
            var herrKeys = new HashSet<string>();
            var matKeys = new HashSet<string>();

            foreach (var item in list)
            {
                switch (item.TipoComponente)
                {
                    case TipoComponenteMatriz.Material: materialKeys.Add(NormalizeKey(item.Clave)); break;
                    case TipoComponenteMatriz.ManoDeObra: manoKeys.Add(NormalizeKey(item.Clave)); break;
                    case TipoComponenteMatriz.Maquinaria: maqKeys.Add(NormalizeKey(item.Clave)); break;
                    case TipoComponenteMatriz.Herramienta: herrKeys.Add(NormalizeKey(item.Clave)); break;
                    case TipoComponenteMatriz.Auxiliar:
                        matKeys.Add(NormalizeKey(item.Clave));
                        preview.MatrixDependencies++;
                        break;
                }
            }

            preview.MaterialConflicts = currentContext.Materiales.Where(x => x.ProyectoId == currentProjectId).AsEnumerable().Count(x => materialKeys.Contains(NormalizeKey(x.Clave)));
            preview.ManoDeObraConflicts = currentContext.ManoDeObra.Where(x => x.ProyectoId == currentProjectId).AsEnumerable().Count(x => manoKeys.Contains(NormalizeKey(x.Clave)));
            preview.MaquinariaConflicts = currentContext.Maquinaria.Where(x => x.ProyectoId == currentProjectId).AsEnumerable().Count(x => maqKeys.Contains(NormalizeKey(x.Clave)));
            preview.HerramientaConflicts = currentContext.Herramientas.Where(x => x.ProyectoId == currentProjectId).AsEnumerable().Count(x => herrKeys.Contains(NormalizeKey(x.Clave)));
            preview.MatrizConflicts = currentContext.Matrices.Where(x => x.ProyectoId == currentProjectId).AsEnumerable().Count(x => matKeys.Contains(NormalizeKey(x.Clave)));
            if (preview.MatrizConflicts > 0) preview.ConflictLines.Add($"Matrices auxiliares: {preview.MatrizConflicts}");
            if (preview.MaterialConflicts > 0) preview.ConflictLines.Add($"Materiales: {preview.MaterialConflicts}");
            if (preview.ManoDeObraConflicts > 0) preview.ConflictLines.Add($"Mano de obra: {preview.ManoDeObraConflicts}");
            if (preview.MaquinariaConflicts > 0) preview.ConflictLines.Add($"Maquinaria: {preview.MaquinariaConflicts}");
            if (preview.HerramientaConflicts > 0) preview.ConflictLines.Add($"Herramientas: {preview.HerramientaConflicts}");
            return preview;
        }

        private static string GetTipoNombre(ExternalProjectInsumoOption item)
        {
            return item.TipoComponente switch
            {
                TipoComponenteMatriz.Material => "Material",
                TipoComponenteMatriz.ManoDeObra => string.Equals(item.Tag, "Cuadrilla", StringComparison.OrdinalIgnoreCase) ? "Cuadrilla" : "Mano de obra",
                TipoComponenteMatriz.Maquinaria => "Maquinaria",
                TipoComponenteMatriz.Herramienta => "Herramienta",
                TipoComponenteMatriz.Auxiliar => "Básico",
                _ => "Insumo"
            };
        }

        public ExternalInsumoImportResult ImportSelected(SOPROContext currentContext, int currentProjectId, string projectPath, IEnumerable<ExternalProjectInsumoOption> selectedItems, ExternalMatrixImportConflictPolicy policy, decimal cantidad)
        {
            var result = new ExternalInsumoImportResult();
            var list = selectedItems?.ToList() ?? new List<ExternalProjectInsumoOption>();
            if (list.Count == 0) return result;

            // Reemplazos de MO existente: se propagan al final para no dejar
            // obsoletas las matrices/conceptos destino que ya la consumían.
            var manoReemplazadas = new List<int>();

            using var externalSource = ExternalProjectSource.Open(_dbContextFactory, projectPath);
            var externalContext = externalSource.Context;
            var sourceProjectName = externalContext.Proyectos.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Nombre).FirstOrDefault() ?? Path.GetFileNameWithoutExtension(externalSource.OriginalPath);

            // Motor con la precisión del proyecto destino (precisión de pantalla).
            var proyectoDestino = currentContext.Proyectos.AsNoTracking()
                .FirstOrDefault(p => p.Id == currentProjectId);
            var engine = proyectoDestino != null
                ? CalculationEngineFactory.FromProyecto(proyectoDestino)
                : new SoproCalculationEngine(2, 2, 4);

            foreach (var item in list)
            {
                switch (item.TipoComponente)
                {
                    case TipoComponenteMatriz.Material:
                    {
                        var source = externalContext.Materiales.AsNoTracking().FirstOrDefault(x => x.Id == item.ItemId);
                        if (source == null) break;
                        var existing = currentContext.Materiales.Where(x => x.ProyectoId == currentProjectId).AsEnumerable().FirstOrDefault(x => NormalizeKey(x.Clave) == NormalizeKey(source.Clave));
                        Material target;
                        if (existing != null && policy == ExternalMatrixImportConflictPolicy.ReplaceExisting)
                        {
                            target = existing;
                            ExternalImportEntityMapper.ApplyMaterial(target, source, currentProjectId, sourceProjectName);
                            currentContext.SaveChanges();
                        }
                        else
                        {
                            target = ExternalImportEntityMapper.CloneMaterial(source, currentProjectId, sourceProjectName);
                            if (existing != null) target.Clave = GenerateTempKey(source.Clave, currentContext.Materiales.Where(x => x.ProyectoId == currentProjectId).Select(x => x.Clave));
                            currentContext.Materiales.Add(target);
                            currentContext.SaveChanges();
                            result.ImportedMateriales++;
                        }
                        result.ImportedComponents.Add(new ComponenteMatriz { MaterialId = target.Id, Material = target, TipoComponente = TipoComponenteMatriz.Material, Cantidad = cantidad, Importe = engine.Multiply(cantidad, target.PrecioUnitario) });
                        break;
                    }
                    case TipoComponenteMatriz.ManoDeObra:
                    {
                        var source = externalContext.ManoDeObra.AsNoTracking().FirstOrDefault(x => x.Id == item.ItemId);
                        if (source == null) break;
                        var existing = currentContext.ManoDeObra.Where(x => x.ProyectoId == currentProjectId).AsEnumerable().FirstOrDefault(x => NormalizeKey(x.Clave) == NormalizeKey(source.Clave));
                        ManoDeObra target;
                        if (existing != null && policy == ExternalMatrixImportConflictPolicy.ReplaceExisting)
                        {
                            target = existing;
                            ExternalImportEntityMapper.ApplyManoDeObra(
                                target, source, currentProjectId, sourceProjectName, proyectoDestino?.ParametrosFSR);
                            currentContext.SaveChanges();
                            manoReemplazadas.Add(target.Id);
                        }
                        else
                        {
                            target = ExternalImportEntityMapper.CloneManoDeObra(
                                source, currentProjectId, sourceProjectName, proyectoDestino?.ParametrosFSR);
                            if (existing != null) target.Clave = GenerateTempKey(source.Clave, currentContext.ManoDeObra.Where(x => x.ProyectoId == currentProjectId).Select(x => x.Clave));
                            currentContext.ManoDeObra.Add(target);
                            currentContext.SaveChanges();
                            result.ImportedManoDeObra++;
                        }
                        result.ImportedComponents.Add(new ComponenteMatriz { ManoDeObraId = target.Id, ManoDeObra = target, TipoComponente = TipoComponenteMatriz.ManoDeObra, Cantidad = cantidad, Importe = 0m });
                        break;
                    }
                    case TipoComponenteMatriz.Maquinaria:
                    {
                        var source = externalContext.Maquinaria.AsNoTracking().FirstOrDefault(x => x.Id == item.ItemId);
                        if (source == null) break;
                        var existing = currentContext.Maquinaria.Where(x => x.ProyectoId == currentProjectId).AsEnumerable().FirstOrDefault(x => NormalizeKey(x.Clave) == NormalizeKey(source.Clave));
                        Maquinaria target;
                        if (existing != null && policy == ExternalMatrixImportConflictPolicy.ReplaceExisting)
                        {
                            target = existing;
                            ExternalImportEntityMapper.ApplyMaquinaria(target, source, currentProjectId, sourceProjectName);
                            currentContext.SaveChanges();
                        }
                        else
                        {
                            target = ExternalImportEntityMapper.CloneMaquinaria(source, currentProjectId, sourceProjectName);
                            if (existing != null) target.Clave = GenerateTempKey(source.Clave, currentContext.Maquinaria.Where(x => x.ProyectoId == currentProjectId).Select(x => x.Clave));
                            currentContext.Maquinaria.Add(target);
                            currentContext.SaveChanges();
                            result.ImportedMaquinaria++;
                        }
                        result.ImportedComponents.Add(new ComponenteMatriz { MaquinariaId = target.Id, Maquinaria = target, TipoComponente = TipoComponenteMatriz.Maquinaria, Cantidad = cantidad, Rendimiento = Maquinaria.CalcularRendimiento(cantidad), Importe = engine.Multiply(cantidad, target.CostoHorario) });
                        break;
                    }
                    case TipoComponenteMatriz.Herramienta:
                    {
                        var source = GetExternalHerramientaBasic(externalSource, item.ItemId);
                        if (source == null) break;
                        var existing = currentContext.Herramientas.Where(x => x.ProyectoId == currentProjectId).AsEnumerable().FirstOrDefault(x => NormalizeKey(x.Clave) == NormalizeKey(source.Clave));
                        Herramienta target;
                        if (existing != null && policy == ExternalMatrixImportConflictPolicy.ReplaceExisting)
                        {
                            target = existing;
                            ExternalImportEntityMapper.ApplyHerramienta(target, source, currentProjectId, sourceProjectName);
                            currentContext.SaveChanges();
                        }
                        else
                        {
                            target = ExternalImportEntityMapper.CloneHerramienta(source, currentProjectId, sourceProjectName);
                            if (existing != null) target.Clave = GenerateTempKey(source.Clave, currentContext.Herramientas.Where(x => x.ProyectoId == currentProjectId).Select(x => x.Clave));
                            currentContext.Herramientas.Add(target);
                            currentContext.SaveChanges();
                            result.ImportedHerramientas++;
                        }
                        result.ImportedComponents.Add(new ComponenteMatriz { HerramientaId = target.Id, Herramienta = target, TipoComponente = TipoComponenteMatriz.Herramienta, Cantidad = cantidad, Importe = 0m });
                        break;
                    }
                    case TipoComponenteMatriz.Auxiliar:
                    {
                        var matrixResult = _matrixImportService.ImportMatrixTree(currentContext, currentProjectId, externalSource, item.ItemId, policy);
                        var target = currentContext.Matrices.Find(matrixResult.RootMatrixId);
                        if (target != null)
                        {
                            result.ImportedMatrices += matrixResult.ImportedMatrices;
                            result.ImportedMateriales += matrixResult.ImportedMateriales;
                            result.ImportedManoDeObra += matrixResult.ImportedManoDeObra;
                            result.ImportedMaquinaria += matrixResult.ImportedMaquinaria;
                            result.ImportedHerramientas += matrixResult.ImportedHerramientas;
                            result.ImportedComponents.Add(new ComponenteMatriz { AuxiliarId = target.Id, Auxiliar = target, TipoComponente = TipoComponenteMatriz.Auxiliar, Cantidad = cantidad, Importe = engine.Multiply(cantidad, target.CostoDirecto) });
                        }
                        break;
                    }
                }
            }

            foreach (var manoId in manoReemplazadas.Distinct())
                PricePropagationService.PropagarManoDeObra(currentContext, manoId, currentProjectId);

            return result;
        }

        private sealed class ExternalHerramientaBasic
        {
            public int Id { get; set; }
            public string Clave { get; set; } = string.Empty;
            public string Descripcion { get; set; } = string.Empty;
            public string Unidad { get; set; } = string.Empty;
            public decimal PrecioUnitario { get; set; }
            public string? Notas { get; set; }
        }

        private static List<ExternalHerramientaBasic> LoadExternalHerramientasBasic(ExternalProjectSource source)
        {
            return source.Context.Herramientas
                .AsNoTracking()
                .OrderBy(h => h.Clave)
                .Select(h => new ExternalHerramientaBasic
                {
                    Id = h.Id,
                    Clave = h.Clave,
                    Descripcion = h.Descripcion,
                    Unidad = h.Unidad,
                    PrecioUnitario = h.PrecioUnitario
                })
                .ToList();
        }

        private static Herramienta? GetExternalHerramientaBasic(ExternalProjectSource source, int itemId)
        {
            var item = source.Context.Herramientas
                .AsNoTracking()
                .FirstOrDefault(h => h.Id == itemId);
            if (item == null)
                return null;

            return new Herramienta
            {
                Id = item.Id,
                Clave = item.Clave ?? string.Empty,
                Descripcion = item.Descripcion ?? string.Empty,
                Unidad = item.Unidad ?? string.Empty,
                PrecioUnitario = item.PrecioUnitario,
                // [N7-11] Las notas viajan como el resto de campos: la ruta de
                // matrices ya las conservaba y la paridad campo a campo lo exige.
                Notas = item.Notas ?? string.Empty,
                Origen = OrigenInsumo.Proyecto
            };
        }

        private static string NormalizeKey(string? key) => (key ?? string.Empty).Trim().ToUpperInvariant();
        private static string GenerateTempKey(string? originalKey, IEnumerable<string?> existingKeys)
        {
            var existing = new HashSet<string>(existingKeys.Where(x => !string.IsNullOrWhiteSpace(x)).Select(NormalizeKey));
            var clean = string.IsNullOrWhiteSpace(originalKey) ? "ITEM" : originalKey!.Trim();
            var baseCandidate = $"TEMP-{clean}";
            var candidate = baseCandidate;
            var i = 2;
            while (existing.Contains(NormalizeKey(candidate))) { candidate = $"{baseCandidate}-{i}"; i++; }
            if (candidate.Length > 50) candidate = candidate.Substring(0, 50);
            return candidate.ToUpperInvariant();
        }
    }
}
