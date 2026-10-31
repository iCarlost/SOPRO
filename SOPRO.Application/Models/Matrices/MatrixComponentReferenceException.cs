using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Matrices;

/// <summary>
/// Error tipado de un componente de matriz cuya referencia no coincide con su
/// <see cref="TipoComponente"/>. Se lanza antes de persistir (guardado) o antes
/// de mutar precios (recálculo) para que la UI muestre un mensaje accionable en
/// lugar de una navegación nula o un <see cref="InvalidOperationException"/>
/// genérico.
/// </summary>
public sealed class MatrixComponentReferenceException : Exception
{
    public MatrixComponentReferenceException(
        string message,
        Matriz? matriz = null,
        ComponenteMatriz? componente = null,
        int position = 0,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Matriz = matriz;
        Componente = componente;
        Position = position;
    }

    /// <summary>Matriz afectada, cuando el error ocurre al recalcular.</summary>
    public Matriz? Matriz { get; }

    /// <summary>Componente con la referencia inválida, cuando se identificó.</summary>
    public ComponenteMatriz? Componente { get; }

    /// <summary>Posición 1-based del componente dentro de la matriz (0 si no aplica).</summary>
    public int Position { get; }

    /// <summary>
    /// Construye el error de recálculo nombrando la matriz (clave, Id y
    /// descripción) y el componente (Id y posición) para que el usuario pueda
    /// localizar y corregir la fila.
    /// </summary>
    public static MatrixComponentReferenceException ForRecalculation(
        Matriz matriz,
        ComponenteMatriz? componente,
        int position,
        string detalle)
    {
        ArgumentNullException.ThrowIfNull(matriz);

        var descripcion = string.IsNullOrWhiteSpace(matriz.Descripcion)
            ? string.Empty
            : $" - {matriz.Descripcion.Trim()}";

        return new MatrixComponentReferenceException(
            $"No se puede recalcular la matriz '{matriz.Clave}' (Id {matriz.Id}){descripcion}: " +
            $"{detalle} Corrija la matriz antes de recalcular.",
            matriz,
            componente,
            position);
    }
}
