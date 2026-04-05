using System.Linq;
using SOPRO.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Valida que las claves sean únicas entre TODOS los tipos de insumos.
    /// Regla: No puede haber Material, ManoObra y Maquinaria con la misma clave.
    /// </summary>
    public static class ClaveValidator
    {
        /// <summary>
        /// Verifica si una clave ya existe en algún catálogo del proyecto.
        /// </summary>
        /// <param name="context">Contexto de BD</param>
        /// <param name="clave">Clave a validar</param>
        /// <param name="proyectoId">ID del proyecto</param>
        /// <param name="excluirId">ID del insumo a excluir (cuando se edita)</param>
        /// <param name="tipoExcluir">Tipo del insumo a excluir</param>
        /// <returns>Mensaje de error si existe duplicado, null si es válida</returns>
        public static string ValidarClaveUnica(
            SOPROContext context, 
            string clave, 
            int proyectoId,
            int? excluirId = null,
            TipoInsumo? tipoExcluir = null)
        {
            if (string.IsNullOrWhiteSpace(clave)) return null;
            
            clave = clave.Trim().ToUpper();
            
            // Buscar en materiales
            if (tipoExcluir != TipoInsumo.Material || !excluirId.HasValue)
            {
                var materialDuplicado = context.Materiales
                    .Where(m => m.ProyectoId == proyectoId)
                    .Where(m => m.Clave.ToUpper() == clave)
                    .Where(m => !excluirId.HasValue || tipoExcluir != TipoInsumo.Material || m.Id != excluirId.Value)
                    .FirstOrDefault();
                
                if (materialDuplicado != null)
                    return $"La clave '{clave}' ya está asignada a un Material:\n{materialDuplicado.Descripcion}";
            }
            
            // Buscar en mano de obra
            if (tipoExcluir != TipoInsumo.ManoDeObra || !excluirId.HasValue)
            {
                var moDuplicada = context.ManoDeObra
                    .Where(m => m.ProyectoId == proyectoId)
                    .Where(m => m.Clave.ToUpper() == clave)
                    .Where(m => !excluirId.HasValue || tipoExcluir != TipoInsumo.ManoDeObra || m.Id != excluirId.Value)
                    .FirstOrDefault();
                
                if (moDuplicada != null)
                    return $"La clave '{clave}' ya está asignada a Mano de Obra:\n{moDuplicada.Descripcion}";
            }
            
            // Buscar en maquinaria
            if (tipoExcluir != TipoInsumo.Maquinaria || !excluirId.HasValue)
            {
                var maqDuplicada = context.Maquinaria
                    .Where(m => m.ProyectoId == proyectoId)
                    .Where(m => m.Clave.ToUpper() == clave)
                    .Where(m => !excluirId.HasValue || tipoExcluir != TipoInsumo.Maquinaria || m.Id != excluirId.Value)
                    .FirstOrDefault();
                
                if (maqDuplicada != null)
                    return $"La clave '{clave}' ya está asignada a Maquinaria:\n{maqDuplicada.Descripcion}";
            }
            
            return null; // Clave válida
        }
    }
    
    public enum TipoInsumo
    {
        Material,
        ManoDeObra,
        Maquinaria,
        Matriz
    }

    public enum TipoInsumoContexto
    {
        Material,
        ManoDeObra,
        Maquinaria,
        Herramienta
    }
}
