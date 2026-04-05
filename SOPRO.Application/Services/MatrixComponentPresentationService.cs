using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    public static class MatrixComponentPresentationService
    {
        public static IReadOnlyList<MatrixComponentRowDisplayModel> BuildRows(IEnumerable<ComponenteMatriz> componentes, decimal baseManoObra)
        {
            if (componentes == null) throw new ArgumentNullException(nameof(componentes));

            var rows = new List<MatrixComponentRowDisplayModel>();

            foreach (var comp in componentes)
            {
                var row = new MatrixComponentRowDisplayModel
                {
                    Cantidad = comp.Cantidad,
                    Importe = comp.Importe
                };

                switch (comp.TipoComponente)
                {
                    case TipoComponenteMatriz.Material:
                        row.Tipo = "📦 Material";
                        if (comp.Material != null)
                        {
                            row.Clave = comp.Material.Clave;
                            row.Descripcion = comp.Material.Descripcion;
                            row.Unidad = comp.Material.Unidad;
                            row.PrecioUnitario = comp.Material.PrecioUnitario;
                        }
                        break;

                    case TipoComponenteMatriz.ManoDeObra:
                        row.Tipo = "👷 M.O.";
                        if (comp.ManoDeObra != null)
                        {
                            row.Clave = comp.ManoDeObra.Clave;
                            row.Descripcion = comp.ManoDeObra.Descripcion;
                            row.Unidad = comp.ManoDeObra.Unidad;
                            row.PrecioUnitario = comp.ManoDeObra.EsPorcentajeMO ? baseManoObra : comp.ManoDeObra.SalarioReal;
                        }
                        break;

                    case TipoComponenteMatriz.Maquinaria:
                        row.Tipo = "🚜 Maq.";
                        if (comp.Maquinaria != null)
                        {
                            row.Clave = comp.Maquinaria.Clave;
                            row.Descripcion = comp.Maquinaria.Descripcion;
                            row.Unidad = "hora";
                            row.PrecioUnitario = comp.Maquinaria.CostoHorario;
                        }
                        break;

                    case TipoComponenteMatriz.Auxiliar:
                        if (comp.Auxiliar != null)
                        {
                            row.Tipo = comp.Auxiliar.Tipo == TipoMatriz.Cuadrilla ? "👷 Cuadrilla" : "🧩 Básico";
                            row.Clave = comp.Auxiliar.Clave;
                            row.Descripcion = comp.Auxiliar.Descripcion;
                            row.Unidad = comp.Auxiliar.Unidad;
                            row.PrecioUnitario = comp.Auxiliar.CostoDirecto;
                        }
                        break;

                    case TipoComponenteMatriz.Herramienta:
                        row.Tipo = "🛠️ Herramienta";
                        if (comp.Herramienta != null)
                        {
                            row.Clave = comp.Herramienta.Clave;
                            row.Descripcion = comp.Herramienta.Descripcion;
                            row.Unidad = comp.Herramienta.Unidad;
                            row.PrecioUnitario = comp.Herramienta.EsPorcentajeMO ? baseManoObra : comp.Herramienta.PrecioUnitario;
                        }
                        break;
                }

                rows.Add(row);
            }

            return rows;
        }
    }
}
