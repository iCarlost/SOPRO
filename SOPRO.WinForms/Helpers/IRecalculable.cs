namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Contrato mínimo para módulos que permiten forzar un recálculo/reproceso
    /// desde el ribbon principal sin duplicar lógica en FormProyecto.
    /// </summary>
    public interface IRecalculable
    {
        void RecalcularTodo();
    }
}
