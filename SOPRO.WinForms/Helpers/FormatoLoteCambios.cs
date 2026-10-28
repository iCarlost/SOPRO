using System.Collections.Generic;
using SOPRO.Application.Models.Reporting.ReportColumns;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Traducción ÚNICA de los pares antes/después capturados por la UI
    /// (<see cref="CambioFormatoSeleccion"/>) al lote de comandos que persiste
    /// <c>FormatoCeldasCommandService</c>. La comparten todos los formularios de
    /// formato por celda (Presupuesto, Programa de Obra, Indirectos, Matrices,
    /// catálogos, …) para que Undo/Redo no tenga rutas divergentes.
    ///
    /// Semántica: <c>Antes</c> y <c>Despues</c> son snapshots COMPLETOS del
    /// override de la celda (el coordinador de selección fusiona el delta del
    /// ribbon con el estado previo antes de capturarlos). Por eso el lote viaja
    /// como <see cref="OverlayApplyMode.Replace"/>: al deshacer hay que quitar
    /// TAMBIÉN las propiedades que la edición añadió, no sólo fusionar el
    /// "antes" parcial sobre lo persistido. Un estado <c>null</c> (o vacío) en el
    /// destino BORRA el override.
    /// </summary>
    internal static class FormatoLoteCambios
    {
        /// <summary>
        /// Construye el lote con base esperada OPTIMISTA: con
        /// <paramref name="aplicarAntes"/> el destino es el "antes" (undo) y la
        /// base esperada el "después"; en otro caso, al revés.
        /// </summary>
        /// <param name="cambios">Pares antes/después capturados por el formulario.</param>
        /// <param name="aplicarAntes"><c>true</c> para undo; <c>false</c> para do/redo.</param>
        /// <param name="vistaPorDefecto">Vista aplicada cuando la dirección no la informa.</param>
        public static IReadOnlyList<CambioFormatoCeldaCommand> Construir(
            IEnumerable<CambioFormatoSeleccion> cambios,
            bool aplicarAntes,
            string vistaPorDefecto)
        {
            var lote = new List<CambioFormatoCeldaCommand>();
            if (cambios is null) return lote;

            foreach (var cambio in cambios)
            {
                if (cambio?.Direccion is null) continue;

                var vista = string.IsNullOrEmpty(cambio.Direccion.ClaveVista)
                    ? vistaPorDefecto ?? string.Empty
                    : cambio.Direccion.ClaveVista;

                lote.Add(new CambioFormatoCeldaCommand(
                    vista,
                    cambio.Direccion.ClaveFila,
                    cambio.Direccion.NombreColumna,
                    ExpectedOverlay: aplicarAntes ? cambio.Despues : cambio.Antes,
                    ProposedOverlay: aplicarAntes ? cambio.Antes : cambio.Despues,
                    ApplyMode: OverlayApplyMode.Replace));
            }

            return lote;
        }
    }
}
