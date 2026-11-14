using SOPRO.Core.Entities;

namespace SOPRO.Application.DTOs.Matrices
{
    /// <summary>
    /// Edición explícita de la descripción de un insumo (fuente) usada por una
    /// matriz. Viaja en el DTO del guardado para que la escritura por operación
    /// aplique el cambio en la misma transacción, sin depender de mutaciones de
    /// navegación en el contexto de la sesión.
    /// </summary>
    public class MatrixSourceDescriptionEditDto
    {
        public TipoComponenteMatriz TipoComponente { get; set; }
        public int SourceId { get; set; }
        public string OriginalDescription { get; set; } = string.Empty;
        public string NewDescription { get; set; } = string.Empty;
    }
}
