namespace SOPRO.WinForms.Diagnostics
{
    /// <summary>
    /// Contrato del presentador del diálogo fatal. El coordinador de
    /// diagnóstico lo invoca una vez preparados los registros locales.
    /// </summary>
    internal interface IFatalErrorPresenter
    {
        void Show(string correlationId, string logsDirectory);
    }
}
