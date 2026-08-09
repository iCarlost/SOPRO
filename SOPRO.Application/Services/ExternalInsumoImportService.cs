using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SOPRO.Application.Services
{
    public sealed class ExternalInsumoImportService
    {
        private readonly ExternalMatrixImportService _matrixImportService = new();

        public ExternalProjectInsumoLoadResult LoadExternalInsumos(string projectPath, TipoComponenteMatriz tipoComponente, string? filtro, bool incluirManoDeObraIndividual = true, bool incluirCuadrillas = true)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentException("Debe indicar la ruta del proyecto externo.", nameof(projectPath));
            if (!File.Exists(projectPath))
                throw new FileNotFoundException("No se encontró el proyecto seleccionado.", projectPath);

            using var externalContext = new SOPROContext(projectPath);
            SchemaManager.EnsureCurrentSchema(externalContext);
            var projectName = externalContext.Proyectos
                .AsNoTracking()
                .OrderBy(p => p.Id)
                .Select(p => p.Nombre)
                .FirstOrDefault() ?? Path.GetFileNameWithoutExtension(projectPath);

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
                        .Select(x => new ExternalProjectInsumoOption { ItemId = x.Id, ProjectName = projectName, ProjectPath = projectPath, TipoComponente = TipoComponenteMatriz.Material, Clave = x.Clave, Descripcion = x.Descripcion, Unidad = x.Unidad, PrecioUnitario = x.PrecioUnitario, PrecioMostrado = x.PrecioUnitario.ToString("C2") }));
                    break;
                case TipoComponenteMatriz.ManoDeObra:
                    if (incluirManoDeObraIndividual)
                    {
                        items.AddRange(externalContext.ManoDeObra.AsNoTracking().OrderBy(x => x.Clave).ToList()
                            .Where(x => Match(x.Clave, x.Descripcion))
                            .Select(x => new ExternalProjectInsumoOption { ItemId = x.Id, ProjectName = projectName, ProjectPath = projectPath, TipoComponente = TipoComponenteMatriz.ManoDeObra, Tag = "Individual", Clave = x.Clave, Descripcion = x.Descripcion, Unidad = x.Unidad, PrecioUnitario = x.SalarioReal, PrecioMostrado = x.SalarioReal.ToString("C2") }));
                    }
                    if (incluirCuadrillas)
                    {
                        items.AddRange(externalContext.Matrices.AsNoTracking().Where(m => m.Tipo == TipoMatriz.Cuadrilla).OrderBy(x => x.Clave).ToList()
                            .Where(x => Match(x.Clave, x.Descripcion))
                            .Select(x => new ExternalProjectInsumoOption { ItemId = x.Id, ProjectName = projectName, ProjectPath = projectPath, TipoComponente = TipoComponenteMatriz.Auxiliar, Tag = "Cuadrilla", Clave = x.Clave, Descripcion = "👷 " + x.Descripcion, Unidad = x.Unidad, PrecioUnitario = x.CostoDirecto, PrecioMostrado = x.CostoDirecto.ToString("C2") }));
                    }
                    break;
                case TipoComponenteMatriz.Maquinaria:
                    items.AddRange(externalContext.Maquinaria.AsNoTracking().OrderBy(x => x.Clave).ToList()
                        .Where(x => Match(x.Clave, x.Descripcion))
                        .Select(x => new ExternalProjectInsumoOption { ItemId = x.Id, ProjectName = projectName, ProjectPath = projectPath, TipoComponente = TipoComponenteMatriz.Maquinaria, Clave = x.Clave, Descripcion = x.Descripcion, Unidad = "hora", PrecioUnitario = x.CostoHorario, PrecioMostrado = x.CostoHorario.ToString("C2") }));
                    break;
                case TipoComponenteMatriz.Auxiliar:
                    items.AddRange(externalContext.Matrices.AsNoTracking().Where(m => m.Tipo == TipoMatriz.Basico).OrderBy(x => x.Clave).ToList()
                        .Where(x => Match(x.Clave, x.Descripcion))
                        .Select(x => new ExternalProjectInsumoOption { ItemId = x.Id, ProjectName = projectName, ProjectPath = projectPath, TipoComponente = TipoComponenteMatriz.Auxiliar, Clave = x.Clave, Descripcion = x.Descripcion, Unidad = x.Unidad, PrecioUnitario = x.CostoDirecto, PrecioMostrado = x.CostoDirecto.ToString("C4") }));
                    break;
                case TipoComponenteMatriz.Herramienta:
                    items.AddRange(LoadExternalHerramientasBasic(projectPath)
                        .Where(x => Match(x.Clave, x.Descripcion))
                        .Select(x => new ExternalProjectInsumoOption { ItemId = x.Id, ProjectName = projectName, ProjectPath = projectPath, TipoComponente = TipoComponenteMatriz.Herramienta, Clave = x.Clave, Descripcion = x.Descripcion, Unidad = x.Unidad, PrecioUnitario = x.PrecioUnitario, PrecioMostrado = x.PrecioUnitario.ToString("C2") }));
                    break;
            }

            return new ExternalProjectInsumoLoadResult { ProjectName = projectName, ProjectPath = projectPath, Items = items.OrderBy(x => x.Clave).ToList() };
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

            using var externalContext = new SOPROContext(projectPath);
            SchemaManager.EnsureCurrentSchema(externalContext);
            var sourceProjectName = externalContext.Proyectos.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Nombre).FirstOrDefault() ?? Path.GetFileNameWithoutExtension(projectPath);

            // Motor con la precisión del proyecto destino (precisión de pantalla).
            var proyectoDestino = currentContext.Proyectos.AsNoTracking()
                .FirstOrDefault(p => p.Id == currentProjectId);
            var motor = proyectoDestino != null
                ? new MotorCalculoSopro(proyectoDestino)
                : new MotorCalculoSopro(2, 2, 4);

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
                            ApplyMaterial(target, source, currentProjectId, sourceProjectName);
                            currentContext.SaveChanges();
                        }
                        else
                        {
                            target = CloneMaterial(source, currentProjectId, sourceProjectName);
                            if (existing != null) target.Clave = GenerateTempKey(source.Clave, currentContext.Materiales.Where(x => x.ProyectoId == currentProjectId).Select(x => x.Clave));
                            currentContext.Materiales.Add(target);
                            currentContext.SaveChanges();
                            result.ImportedMateriales++;
                        }
                        result.ImportedComponents.Add(new ComponenteMatriz { MaterialId = target.Id, Material = target, TipoComponente = TipoComponenteMatriz.Material, Cantidad = cantidad, Importe = motor.Multiplicar(cantidad, target.PrecioUnitario) });
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
                            ApplyManoDeObra(target, source, currentProjectId, sourceProjectName);
                            currentContext.SaveChanges();
                        }
                        else
                        {
                            target = CloneManoDeObra(source, currentProjectId, sourceProjectName);
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
                            ApplyMaquinaria(target, source, currentProjectId, sourceProjectName);
                            currentContext.SaveChanges();
                        }
                        else
                        {
                            target = CloneMaquinaria(source, currentProjectId, sourceProjectName);
                            if (existing != null) target.Clave = GenerateTempKey(source.Clave, currentContext.Maquinaria.Where(x => x.ProyectoId == currentProjectId).Select(x => x.Clave));
                            currentContext.Maquinaria.Add(target);
                            currentContext.SaveChanges();
                            result.ImportedMaquinaria++;
                        }
                        result.ImportedComponents.Add(new ComponenteMatriz { MaquinariaId = target.Id, Maquinaria = target, TipoComponente = TipoComponenteMatriz.Maquinaria, Cantidad = cantidad, Rendimiento = cantidad > 0 ? Math.Round(1m / cantidad, 5, MidpointRounding.AwayFromZero) : 0m, Importe = motor.Multiplicar(cantidad, target.CostoHorario) });
                        break;
                    }
                    case TipoComponenteMatriz.Herramienta:
                    {
                        var source = GetExternalHerramientaBasic(projectPath, item.ItemId);
                        if (source == null) break;
                        var existing = currentContext.Herramientas.Where(x => x.ProyectoId == currentProjectId).AsEnumerable().FirstOrDefault(x => NormalizeKey(x.Clave) == NormalizeKey(source.Clave));
                        Herramienta target;
                        if (existing != null && policy == ExternalMatrixImportConflictPolicy.ReplaceExisting)
                        {
                            target = existing;
                            ApplyHerramienta(target, source, currentProjectId, sourceProjectName);
                            currentContext.SaveChanges();
                        }
                        else
                        {
                            target = CloneHerramienta(source, currentProjectId, sourceProjectName);
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
                        var matrixResult = _matrixImportService.ImportMatrixTree(currentContext, currentProjectId, projectPath, item.ItemId, policy);
                        var target = currentContext.Matrices.Find(matrixResult.RootMatrixId);
                        if (target != null)
                        {
                            result.ImportedMatrices += matrixResult.ImportedMatrices;
                            result.ImportedMateriales += matrixResult.ImportedMateriales;
                            result.ImportedManoDeObra += matrixResult.ImportedManoDeObra;
                            result.ImportedMaquinaria += matrixResult.ImportedMaquinaria;
                            result.ImportedHerramientas += matrixResult.ImportedHerramientas;
                            result.ImportedComponents.Add(new ComponenteMatriz { AuxiliarId = target.Id, Auxiliar = target, TipoComponente = TipoComponenteMatriz.Auxiliar, Cantidad = cantidad, Importe = motor.Multiplicar(cantidad, target.CostoDirecto) });
                        }
                        break;
                    }
                }
            }
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

        private static List<ExternalHerramientaBasic> LoadExternalHerramientasBasic(string projectPath)
        {
            var result = new List<ExternalHerramientaBasic>();
            using var cn = new SqliteConnection($"Data Source={projectPath}");
            cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id, Clave, Descripcion, Unidad, PrecioUnitario FROM Herramientas ORDER BY Clave";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new ExternalHerramientaBasic
                {
                    Id = reader.GetInt32(0),
                    Clave = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Descripcion = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Unidad = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    PrecioUnitario = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4)
                });
            }
            return result;
        }

        private static Herramienta? GetExternalHerramientaBasic(string projectPath, int itemId)
        {
            using var cn = new SqliteConnection($"Data Source={projectPath}");
            cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id, Clave, Descripcion, Unidad, PrecioUnitario FROM Herramientas WHERE Id = $id LIMIT 1";
            cmd.Parameters.AddWithValue("$id", itemId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            return new Herramienta
            {
                Id = reader.GetInt32(0),
                Clave = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Descripcion = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Unidad = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                PrecioUnitario = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
                Notas = string.Empty,
                Origen = OrigenInsumo.Proyecto
            };
        }

        private static string NormalizeKey(string? key) => (key ?? string.Empty).Trim().ToUpperInvariant();
        private static string AppendOrigin(string? notes, string projectName)
            => ImportOriginStampService.AppendStamp(notes, projectName);
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
        private static Material CloneMaterial(Material source, int currentProjectId, string projectName) { var t = new Material(); ApplyMaterial(t, source, currentProjectId, projectName); return t; }
        private static void ApplyMaterial(Material target, Material source, int currentProjectId, string projectName) { target.Clave=(source.Clave??string.Empty).Trim().ToUpperInvariant(); target.Descripcion=source.Descripcion; target.Unidad=source.Unidad; target.PrecioUnitario=source.PrecioUnitario; target.ProyectoId=currentProjectId; target.Origen=OrigenInsumo.Proyecto; target.MaterialMaestroId=null; target.Notas=AppendOrigin(source.Notas, projectName); target.FechaModificacion=DateTime.Now; }
        private static ManoDeObra CloneManoDeObra(ManoDeObra source, int currentProjectId, string projectName) { var t = new ManoDeObra(); ApplyManoDeObra(t, source, currentProjectId, projectName); return t; }
        private static void ApplyManoDeObra(ManoDeObra target, ManoDeObra source, int currentProjectId, string projectName) { target.Clave=(source.Clave??string.Empty).Trim().ToUpperInvariant(); target.Descripcion=source.Descripcion; target.Unidad=source.Unidad; target.SalarioBase=source.SalarioBase; target.FactorSalarioReal=source.FactorSalarioReal; target.SalarioReal=source.SalarioReal; target.ProyectoId=currentProjectId; target.Origen=OrigenInsumo.Proyecto; target.ManoDeObraMaestraId=null; target.Notas=AppendOrigin(source.Notas, projectName); target.FechaModificacion=DateTime.Now; }
        private static Maquinaria CloneMaquinaria(Maquinaria source, int currentProjectId, string projectName) { var t = new Maquinaria(); ApplyMaquinaria(t, source, currentProjectId, projectName); return t; }
        private static void ApplyMaquinaria(Maquinaria target, Maquinaria source, int currentProjectId, string projectName) { target.Clave=(source.Clave??string.Empty).Trim().ToUpperInvariant(); target.Descripcion=source.Descripcion; target.PotenciaNominal=source.PotenciaNominal; target.TipoCombustible=source.TipoCombustible; target.ValorAdquisicion=source.ValorAdquisicion; target.ValorLlantas=source.ValorLlantas; target.ValorPiezasEspeciales=source.ValorPiezasEspeciales; target.FactorRescate=source.FactorRescate; target.VidaEconomica=source.VidaEconomica; target.TasaInteres=source.TasaInteres; target.HorasEfectivasAnio=source.HorasEfectivasAnio; target.PrimaSeguro=source.PrimaSeguro; target.FactorMantenimiento=source.FactorMantenimiento; target.CantidadCombustible=source.CantidadCombustible; target.PrecioCombustible=source.PrecioCombustible; target.CantidadAceite=source.CantidadAceite; target.PrecioAceite=source.PrecioAceite; target.NumeroLlantas=source.NumeroLlantas; target.VidaEconomicaLlantas=source.VidaEconomicaLlantas; target.VidaPiezasEspeciales=source.VidaPiezasEspeciales; target.SalarioOperador=source.SalarioOperador; target.FactorSalarioReal=source.FactorSalarioReal; target.HorasEfectivasTurno=source.HorasEfectivasTurno; target.CostoHorario=source.CostoHorario; target.EsCostoCalculado=source.EsCostoCalculado; target.ProyectoId=currentProjectId; target.Origen=OrigenInsumo.Proyecto; target.MaquinariaMaestraId=null; target.Notas=AppendOrigin(source.Notas, projectName); target.FechaModificacion=DateTime.Now; target.FechaCalculoCosto=source.FechaCalculoCosto; }
        private static Herramienta CloneHerramienta(Herramienta source, int currentProjectId, string projectName) { var t = new Herramienta(); ApplyHerramienta(t, source, currentProjectId, projectName); return t; }
        private static void ApplyHerramienta(Herramienta target, Herramienta source, int currentProjectId, string projectName) { target.Clave=(source.Clave??string.Empty).Trim().ToUpperInvariant(); target.Descripcion=source.Descripcion; target.Unidad=source.Unidad; target.PrecioUnitario=source.PrecioUnitario; target.ProyectoId=currentProjectId; target.Origen=OrigenInsumo.Proyecto; target.Notas=AppendOrigin(source.Notas, projectName); target.FechaModificacion=DateTime.Now; }
    }
}
