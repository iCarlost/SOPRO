using SOPRO.Application.Services;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Texto visible de la columna "Origen" de los catálogos. La procedencia es
    /// un dato, no la propiedad: solo la marca reconocida en Notas
    /// (<see cref="ImportOriginStampService"/>) indica que la fila vino de otro
    /// catálogo; sin marca, la etiqueta la decide la PROPIEDAD real de la fila
    /// (ProyectoId), nunca <c>Origen</c>, que en una copia importada conserva
    /// Maestro y produciría un reclamo falso de "Catálogo Maestro" sobre una
    /// fila que pertenece al proyecto.
    /// </summary>
    public static class EtiquetaProcedenciaCatalogoHelper
    {
        /// <param name="notas">Notas de la fila, donde se busca la marca de importación.</param>
        /// <param name="proyectoIdFila">ProyectoId real de la fila (null = fila del catálogo maestro).</param>
        public static string ParaFila(string? notas, int? proyectoIdFila)
            => ImportOriginStampService.BuildOriginDisplayForOwner(notas, proyectoIdFila);
    }
}
