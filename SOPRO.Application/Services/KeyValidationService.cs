using System.Linq;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public static class KeyValidationService
    {
        public static string? ValidateUniqueKey(
            SOPROContext context,
            string clave,
            int proyectoId,
            int? excluirId = null,
            CatalogItemType? tipoExcluir = null)
        {
if (string.IsNullOrWhiteSpace(clave)) return null;

        clave = clave.Trim().ToUpperInvariant();

        var materialDuplicado = context.Materiales
            .Where(m => m.ProyectoId == proyectoId)
            .Where(m => m.Clave.ToUpper() == clave)
            .Where(m => !excluirId.HasValue || tipoExcluir != CatalogItemType.Material || m.Id != excluirId.Value)
            .FirstOrDefault();

        if (materialDuplicado != null)
            return $"La clave '{clave}' ya está asignada a un Material:\n{materialDuplicado.Descripcion}";

        var moDuplicada = context.ManoDeObra
            .Where(m => m.ProyectoId == proyectoId)
            .Where(m => m.Clave.ToUpper() == clave)
            .Where(m => !excluirId.HasValue || tipoExcluir != CatalogItemType.ManoDeObra || m.Id != excluirId.Value)
            .FirstOrDefault();

        if (moDuplicada != null)
            return $"La clave '{clave}' ya está asignada a Mano de Obra:\n{moDuplicada.Descripcion}";

        var maqDuplicada = context.Maquinaria
            .Where(m => m.ProyectoId == proyectoId)
            .Where(m => m.Clave.ToUpper() == clave)
            .Where(m => !excluirId.HasValue || tipoExcluir != CatalogItemType.Maquinaria || m.Id != excluirId.Value)
            .FirstOrDefault();

        if (maqDuplicada != null)
            return $"La clave '{clave}' ya está asignada a Maquinaria:\n{maqDuplicada.Descripcion}";

        return null;
        }
    }

    public enum CatalogItemType
    {
        Material,
        ManoDeObra,
        Maquinaria
    }
}
