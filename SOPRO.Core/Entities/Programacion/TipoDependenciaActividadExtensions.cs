namespace SOPRO.Core.Entities
{
    public static class TipoDependenciaActividadExtensions
    {
        /// <summary>Normaliza valores indefinidos (p. ej. 0 de proyectos externos) a FS.</summary>
        public static TipoDependenciaActividad Normalizar(this TipoDependenciaActividad tipo)
            => Enum.IsDefined(typeof(TipoDependenciaActividad), tipo)
                ? tipo
                : TipoDependenciaActividad.FS;
    }
}
