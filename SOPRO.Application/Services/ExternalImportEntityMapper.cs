using System;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  ExternalImportEntityMapper — mapeo único de entidades importadas (N7-11)║
    // ║  ExternalMatrixImportService y ExternalInsumoImportService compartían     ║
    // ║  ocho métodos Apply/Clone idénticos campo por campo (probado por el       ║
    // ║  diferencial ExternalImportEntityMapperParityTests antes de unificar).    ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Copia entidades de insumos desde un proyecto externo al proyecto actual:
    /// normaliza la clave, reasigna proyecto/origen, anula el vínculo maestro,
    /// sella las notas con el proyecto de origen y refresca la fecha.
    /// Internal: solo la consumen los dos servicios de importación externa.
    /// </summary>
    internal static class ExternalImportEntityMapper
    {
        public static Material CloneMaterial(Material source, int currentProjectId, string projectName)
        {
            var clone = new Material();
            ApplyMaterial(clone, source, currentProjectId, projectName);
            return clone;
        }

        public static void ApplyMaterial(Material target, Material source, int currentProjectId, string projectName)
        {
            target.Clave = (source.Clave ?? string.Empty).Trim().ToUpperInvariant();
            target.Descripcion = source.Descripcion;
            target.Unidad = source.Unidad;
            target.PrecioUnitario = source.PrecioUnitario;
            target.ProyectoId = currentProjectId;
            target.Origen = OrigenInsumo.Proyecto;
            target.MaterialMaestroId = null;
            target.Notas = ImportOriginStampService.AppendStamp(source.Notas, projectName);
            target.FechaModificacion = DateTime.Now;
        }

        public static ManoDeObra CloneManoDeObra(ManoDeObra source, int currentProjectId, string projectName)
        {
            var clone = new ManoDeObra();
            ApplyManoDeObra(clone, source, currentProjectId, projectName);
            return clone;
        }

        public static void ApplyManoDeObra(ManoDeObra target, ManoDeObra source, int currentProjectId, string projectName)
        {
            target.Clave = (source.Clave ?? string.Empty).Trim().ToUpperInvariant();
            target.Descripcion = source.Descripcion;
            target.Unidad = source.Unidad;
            target.SalarioBase = source.SalarioBase;
            target.FactorSalarioReal = source.FactorSalarioReal;
            target.SalarioReal = source.SalarioReal;
            target.ProyectoId = currentProjectId;
            target.Origen = OrigenInsumo.Proyecto;
            target.ManoDeObraMaestraId = null;
            target.Notas = ImportOriginStampService.AppendStamp(source.Notas, projectName);
            target.FechaModificacion = DateTime.Now;
        }

        public static Maquinaria CloneMaquinaria(Maquinaria source, int currentProjectId, string projectName)
        {
            var clone = new Maquinaria();
            ApplyMaquinaria(clone, source, currentProjectId, projectName);
            return clone;
        }

        public static void ApplyMaquinaria(Maquinaria target, Maquinaria source, int currentProjectId, string projectName)
        {
            target.Clave = (source.Clave ?? string.Empty).Trim().ToUpperInvariant();
            target.Descripcion = source.Descripcion;
            target.PotenciaNominal = source.PotenciaNominal;
            target.TipoCombustible = source.TipoCombustible;
            target.ValorAdquisicion = source.ValorAdquisicion;
            target.ValorLlantas = source.ValorLlantas;
            target.ValorPiezasEspeciales = source.ValorPiezasEspeciales;
            target.FactorRescate = source.FactorRescate;
            target.VidaEconomica = source.VidaEconomica;
            target.TasaInteres = source.TasaInteres;
            target.HorasEfectivasAnio = source.HorasEfectivasAnio;
            target.PrimaSeguro = source.PrimaSeguro;
            target.FactorMantenimiento = source.FactorMantenimiento;
            target.CantidadCombustible = source.CantidadCombustible;
            target.PrecioCombustible = source.PrecioCombustible;
            target.CantidadAceite = source.CantidadAceite;
            target.PrecioAceite = source.PrecioAceite;
            target.NumeroLlantas = source.NumeroLlantas;
            target.VidaEconomicaLlantas = source.VidaEconomicaLlantas;
            target.VidaPiezasEspeciales = source.VidaPiezasEspeciales;
            target.SalarioOperador = source.SalarioOperador;
            target.FactorSalarioReal = source.FactorSalarioReal;
            target.HorasEfectivasTurno = source.HorasEfectivasTurno;
            target.CostoHorario = source.CostoHorario;
            target.EsCostoCalculado = source.EsCostoCalculado;
            target.ProyectoId = currentProjectId;
            target.Origen = OrigenInsumo.Proyecto;
            target.MaquinariaMaestraId = null;
            target.Notas = ImportOriginStampService.AppendStamp(source.Notas, projectName);
            target.FechaModificacion = DateTime.Now;
            target.FechaCalculoCosto = source.FechaCalculoCosto;
        }

        public static Herramienta CloneHerramienta(Herramienta source, int currentProjectId, string projectName)
        {
            var clone = new Herramienta();
            ApplyHerramienta(clone, source, currentProjectId, projectName);
            return clone;
        }

        public static void ApplyHerramienta(Herramienta target, Herramienta source, int currentProjectId, string projectName)
        {
            target.Clave = (source.Clave ?? string.Empty).Trim().ToUpperInvariant();
            target.Descripcion = source.Descripcion;
            target.Unidad = source.Unidad;
            target.PrecioUnitario = source.PrecioUnitario;
            target.ProyectoId = currentProjectId;
            target.Origen = OrigenInsumo.Proyecto;
            target.Notas = ImportOriginStampService.AppendStamp(source.Notas, projectName);
            target.FechaModificacion = DateTime.Now;
        }
    }
}
