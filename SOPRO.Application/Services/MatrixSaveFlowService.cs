using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    public static class MatrixSaveFlowService
    {
        public static MatrixSaveValidationResult ValidateBeforeSave(
            string? clave,
            string? descripcion,
            IReadOnlyCollection<ComponenteMatriz> componentes)
        {
            if (string.IsNullOrWhiteSpace(clave))
            {
                return new MatrixSaveValidationResult
                {
                    IsValid = false,
                    Message = "La clave es obligatoria.",
                    Field = MatrixEditorField.Clave
                };
            }

            if (string.IsNullOrWhiteSpace(descripcion))
            {
                return new MatrixSaveValidationResult
                {
                    IsValid = false,
                    Message = "La descripción es obligatoria.",
                    Field = MatrixEditorField.Descripcion
                };
            }

            if (!MatrixEditorService.HasComponents(componentes))
            {
                return new MatrixSaveValidationResult
                {
                    IsValid = false,
                    Message = "Debe agregar al menos un componente.",
                    Field = MatrixEditorField.Componentes
                };
            }

            return MatrixSaveValidationResult.Valid();
        }

        public static string GetDuplicateKeyMessage() => "Ya existe una matriz con esa clave.";

        public static string GetSuccessMessage(bool isNew)
            => isNew ? "Matriz creada exitosamente." : "Matriz actualizada exitosamente.";

        public static string BuildErrorMessage(Exception ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));

            var mensaje = $"Error al guardar:\n{ex.Message}";

            if (ex.InnerException != null)
            {
                mensaje += $"\n\nDetalle:\n{ex.InnerException.Message}";

                if (ex.InnerException.InnerException != null)
                {
                    mensaje += $"\n\nDetalle adicional:\n{ex.InnerException.InnerException.Message}";
                }
            }

            if (!string.IsNullOrWhiteSpace(ex.StackTrace) && ex.StackTrace.Length < 500)
            {
                mensaje += $"\n\nStack Trace:\n{ex.StackTrace}";
            }

            return mensaje;
        }
    }
}
