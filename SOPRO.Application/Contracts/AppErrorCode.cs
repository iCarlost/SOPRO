namespace SOPRO.Application.Contracts;

/// <summary>Códigos de error tipados de la frontera de Application.</summary>
public enum AppErrorCode
{
    /// <summary>Los datos de entrada no pasan las reglas de negocio.</summary>
    Validation,

    /// <summary>La entidad solicitada no existe.</summary>
    NotFound,

    /// <summary>Conflicto con datos existentes (por ejemplo, clave duplicada).</summary>
    Conflict,

    /// <summary>Error de persistencia o de base de datos.</summary>
    Database,

    /// <summary>La operación solicitada no está soportada.</summary>
    NotSupported,

    /// <summary>Error no clasificado.</summary>
    Unknown,
}