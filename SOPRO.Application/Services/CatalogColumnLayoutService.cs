using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services;

/// <summary>
/// Persistencia del layout de columnas del catálogo de materiales (formato,
/// ancho). Configuración de presentación, no datos de negocio del catálogo.
/// Mantiene fuera del formulario el acceso directo a EF (Gate N3).
/// </summary>
public static class CatalogColumnLayoutService
{
    public static void GuardarAnchoColumna(SOPROContext context, int columnaId, int ancho)
    {
        var columnaDb = context.ColumnasMaterial.Find(columnaId);
        if (columnaDb == null || columnaDb.AnchoColumna == ancho) return;

        columnaDb.AnchoColumna = ancho;
        columnaDb.FechaModificacion = DateTime.Now;
        context.SaveChanges();
    }

    public static void GuardarFormatoColumna(SOPROContext context, int columnaId, ColumnaPersonalizada fmt)
    {
        var columnaDb = context.ColumnasMaterial.Find(columnaId);
        if (columnaDb == null) return;

        AplicarFormato(columnaDb, fmt);
        context.SaveChanges();
    }

    public static void GuardarFormatoGlobal(SOPROContext context, int proyectoId, ColumnaPersonalizada fmt)
    {
        var columnas = context.ColumnasMaterial
            .Where(c => c.ProyectoId == proyectoId)
            .ToList();

        foreach (var columna in columnas)
            AplicarFormato(columna, fmt);

        context.SaveChanges();
    }

    private static void AplicarFormato(ColumnaMaterial columna, ColumnaPersonalizada fmt)
    {
        columna.NombreFuente = fmt.NombreFuente;
        columna.TamanoFuente = fmt.TamanoFuente;
        columna.Negrita = fmt.Negrita;
        columna.Cursiva = fmt.Cursiva;
        columna.Alineacion = fmt.Alineacion;
        columna.ColorFondo = fmt.ColorFondo;
        columna.ColorFuente = fmt.ColorFuente;
        columna.WrapTexto = fmt.WrapTexto;
        columna.AlineacionVertical = fmt.AlineacionVertical;
        columna.FechaModificacion = DateTime.Now;
    }
}