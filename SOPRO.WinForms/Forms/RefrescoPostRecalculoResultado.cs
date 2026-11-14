using System;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Resultado del refresco posterior al recálculo global: cuántas pestañas no
    /// pudieron refrescarse y cualquier fallo fuera del refresco por pestaña.
    /// </summary>
    internal sealed class RefrescoPostRecalculoResultado
    {
        internal RefrescoPostRecalculoResultado(int pestanasFallidas, Exception? error)
        {
            PestanasFallidas = pestanasFallidas;
            Error = error;
        }

        internal int PestanasFallidas { get; }
        internal Exception? Error { get; }
        internal bool TieneFallos => PestanasFallidas > 0 || Error != null;

        internal static RefrescoPostRecalculoResultado ConError(Exception error)
            => new(0, error);
    }
}
