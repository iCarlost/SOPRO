using System;
using System.Collections.Generic;

namespace SOPRO.WinForms.Forms
{
    public sealed class ConsolidacionInsumoItem
    {
        public int Id { get; init; }
        public string Clave { get; init; } = string.Empty;
        public string Descripcion { get; init; } = string.Empty;
        public string Unidad { get; init; } = string.Empty;
        public decimal Precio { get; init; }
        public string PrecioEtiqueta { get; init; } = string.Empty;
        public string TextoLista => $"{Clave} - {Descripcion} [{Unidad}]  {PrecioEtiqueta}";
        public override string ToString() => TextoLista;
    }

    public interface IConsolidacionInsumos
    {
        bool ConsolidacionDisponible { get; }
        event EventHandler EstadoConsolidacionCambiado;
        string NombreTipoConsolidacion { get; }
        IReadOnlyList<ConsolidacionInsumoItem> ObtenerSeleccionConsolidable();
        void EjecutarConsolidacion();
    }
}
