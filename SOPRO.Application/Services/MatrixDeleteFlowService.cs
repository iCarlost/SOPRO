using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    public sealed class MatrixDeleteFlowService
    {
        public MatrixDeletePreview BuildPreview(Matriz matriz)
        {
            return new MatrixDeletePreview
            {
                ConfirmationMessage =
                    $"¿Está seguro de eliminar la matriz?\n\nClave: {matriz.Clave}\n" +
                    $"Descripción: {matriz.Descripcion}\n\n" +
                    "Esta acción eliminará también todos sus componentes y no se puede deshacer."
            };
        }
    }
}
