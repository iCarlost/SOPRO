using System;
using System.Collections.Generic;
using System.Linq;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Servicio central de reportes.
    /// - Inicializa columnas por defecto cuando no existen aún para un proyecto/tipo.
    /// - Resuelve campos dinámicos {campo} con los datos reales del proyecto.
    /// - Devuelve la configuración lista para que el generador de Excel la consuma.
    /// </summary>
    public class ReporteService
    {
        private readonly SOPROContext _ctx;

        public ReporteService(SOPROContext ctx) => _ctx = ctx;

        // ── CAMPOS DINÁMICOS DISPONIBLES ────────────────────────────────────
        public static readonly Dictionary<string, string> CamposDisponibles = new()
        {
            { "{nombre_proyecto}",   "Nombre del proyecto" },
            { "{descripcion}",       "Descripción del proyecto" },
            { "{ubicacion}",         "Ubicación de la obra" },
            { "{convocante}",        "Convocante / Dependencia" },
            { "{contratista}",       "Contratista" },
            { "{apoderado_legal}",   "Apoderado legal" },
            { "{fecha_inicio}",      "Fecha de inicio" },
            { "{fecha_termino}",     "Fecha de término" },
            { "{plazo_ejecucion}",   "Plazo de ejecución (días)" },
            { "{elaboro}",           "Elaboró (campo plantilla)" },
            { "{reviso}",            "Revisó (campo plantilla)" },
            { "{autorizo}",          "Autorizó (campo plantilla)" },
            { "{dependencia}",       "Dependencia (campo plantilla)" },
            { "{numero_contrato}",   "Número de contrato (campo plantilla)" },
            { "{licitacion}",        "Número de licitación (campo plantilla)" },
            { "{texto1}",            "Texto libre 1 (campo plantilla)" },
            { "{texto2}",            "Texto libre 2 (campo plantilla)" },
            { "{fecha_impresion}",   "Fecha de impresión (automático)" },
            { "{pagina}",            "Número de página (automático)" },
            { "{total_paginas}",     "Total de páginas (automático)" },
        };

        // ── COLUMNAS POR DEFECTO DE CADA REPORTE ───────────────────────────
        private static readonly Dictionary<string, List<ConfigColumnaReporte>> _columnasPorDefecto
            = new()
        {
            ["Presupuesto"] = new()
            {
                new() { NombreInterno="Numero",          Encabezado="No.",          Orden=0,  Ancho=45,  ConAlineacion="Centro",   FormatoNumero="" },
                new() { NombreInterno="Clave",           Encabezado="Clave",        Orden=1,  Ancho=80,  ConAlineacion="Centro",   FormatoNumero="" },
                new() { NombreInterno="Descripcion",     Encabezado="Descripción",  Orden=2,  Ancho=280, ConAlineacion="Izquierda",FormatoNumero="" },
                new() { NombreInterno="Unidad",          Encabezado="Unidad",       Orden=3,  Ancho=55,  ConAlineacion="Centro",   FormatoNumero="" },
                new() { NombreInterno="Cantidad",        Encabezado="Cantidad",     Orden=4,  Ancho=80,  ConAlineacion="Derecha",  FormatoNumero="N3" },
                new() { NombreInterno="PrecioUnitario",  Encabezado="P.U.",         Orden=5,  Ancho=90,  ConAlineacion="Derecha",  FormatoNumero="N2" },
                new() { NombreInterno="ImporteTotal",    Encabezado="Importe",      Orden=6,  Ancho=110, ConAlineacion="Derecha",  FormatoNumero="N2" },
            },
            ["APU"] = new()
            {
                new() { NombreInterno="Tipo",            Encabezado="Tipo",         Orden=0,  Ancho=60,  ConAlineacion="Centro",   FormatoNumero="" },
                new() { NombreInterno="Clave",           Encabezado="Clave",        Orden=1,  Ancho=80,  ConAlineacion="Centro",   FormatoNumero="" },
                new() { NombreInterno="Descripcion",     Encabezado="Descripción",  Orden=2,  Ancho=250, ConAlineacion="Izquierda",FormatoNumero="" },
                new() { NombreInterno="Unidad",          Encabezado="Unidad",       Orden=3,  Ancho=55,  ConAlineacion="Centro",   FormatoNumero="" },
                new() { NombreInterno="Cantidad",        Encabezado="Cantidad",     Orden=4,  Ancho=80,  ConAlineacion="Derecha",  FormatoNumero="N5" },
                new() { NombreInterno="PrecioUnitario",  Encabezado="Costo Unit.",  Orden=5,  Ancho=90,  ConAlineacion="Derecha",  FormatoNumero="N4" },
                new() { NombreInterno="Importe",         Encabezado="Importe",      Orden=6,  Ancho=100, ConAlineacion="Derecha",  FormatoNumero="N4" },
            },
            ["Explosion"] = new()
            {
                new() { NombreInterno="Tipo",            Encabezado="Tipo",         Orden=0,  Ancho=60,  ConAlineacion="Centro",   FormatoNumero="" },
                new() { NombreInterno="Clave",           Encabezado="Clave",        Orden=1,  Ancho=80,  ConAlineacion="Centro",   FormatoNumero="" },
                new() { NombreInterno="Descripcion",     Encabezado="Descripción",  Orden=2,  Ancho=250, ConAlineacion="Izquierda",FormatoNumero="" },
                new() { NombreInterno="Unidad",          Encabezado="Unidad",       Orden=3,  Ancho=55,  ConAlineacion="Centro",   FormatoNumero="" },
                new() { NombreInterno="CantidadTotal",   Encabezado="Cantidad",     Orden=4,  Ancho=90,  ConAlineacion="Derecha",  FormatoNumero="N3" },
                new() { NombreInterno="PrecioUnitario",  Encabezado="P.U.",         Orden=5,  Ancho=90,  ConAlineacion="Derecha",  FormatoNumero="N2" },
                new() { NombreInterno="ImporteTotal",    Encabezado="Importe",      Orden=6,  Ancho=110, ConAlineacion="Derecha",  FormatoNumero="N2" },
            },
            ["ManoObra"] = new()
            {
                new() { NombreInterno="Clave",           Encabezado="Clave",        Orden=0,  Ancho=80,  ConAlineacion="Centro",   FormatoNumero="" },
                new() { NombreInterno="Descripcion",     Encabezado="Descripción",  Orden=1,  Ancho=260, ConAlineacion="Izquierda",FormatoNumero="" },
                new() { NombreInterno="Unidad",          Encabezado="Unidad",       Orden=2,  Ancho=55,  ConAlineacion="Centro",   FormatoNumero="" },
                new() { NombreInterno="SalarioBase",     Encabezado="S. Base",      Orden=3,  Ancho=90,  ConAlineacion="Derecha",  FormatoNumero="N2" },
                new() { NombreInterno="FSR",             Encabezado="FSR",          Orden=4,  Ancho=70,  ConAlineacion="Derecha",  FormatoNumero="N5" },
                new() { NombreInterno="SalarioReal",     Encabezado="S. Real",      Orden=5,  Ancho=90,  ConAlineacion="Derecha",  FormatoNumero="N2" },
            },
            ["Materiales"] = new()
            {
                new() { NombreInterno="Clave",           Encabezado="Clave",        Orden=0,  Ancho=80,  ConAlineacion="Centro",   FormatoNumero="" },
                new() { NombreInterno="Descripcion",     Encabezado="Descripción",  Orden=1,  Ancho=280, ConAlineacion="Izquierda",FormatoNumero="" },
                new() { NombreInterno="Unidad",          Encabezado="Unidad",       Orden=2,  Ancho=55,  ConAlineacion="Centro",   FormatoNumero="" },
                new() { NombreInterno="PrecioUnitario",  Encabezado="Precio Unit.", Orden=3,  Ancho=100, ConAlineacion="Derecha",  FormatoNumero="N2" },
            },
            ["Maquinaria"] = new()
            {
                new() { NombreInterno="Clave",           Encabezado="Clave",        Orden=0,  Ancho=80,  ConAlineacion="Centro",   FormatoNumero="" },
                new() { NombreInterno="Descripcion",     Encabezado="Descripción",  Orden=1,  Ancho=260, ConAlineacion="Izquierda",FormatoNumero="" },
                new() { NombreInterno="CostoHorario",    Encabezado="C. Horario",   Orden=2,  Ancho=100, ConAlineacion="Derecha",  FormatoNumero="N2" },
            },
        };

        // ── API PÚBLICA ─────────────────────────────────────────────────────

        /// <summary>
        /// Devuelve la plantilla del proyecto. Si no existe, la crea con valores por defecto.
        /// </summary>
        public PlantillaReporte ObtenerOCrearPlantilla(int proyectoId)
        {
            var plantilla = _ctx.PlantillasReporte
                .FirstOrDefault(p => p.ProyectoId == proyectoId);

            if (plantilla == null)
            {
                var proyecto = _ctx.Proyectos.Find(proyectoId);
                plantilla = new PlantillaReporte
                {
                    ProyectoId               = proyectoId,
                    EncabezadoCenContenido   = proyecto?.Nombre ?? "",
                    CampoElabaro             = "",
                    CampoReviso              = "",
                    FechaModificacion        = DateTime.Now
                };
                _ctx.PlantillasReporte.Add(plantilla);
                _ctx.SaveChanges();
            }

            return plantilla;
        }

        /// <summary>
        /// Devuelve las columnas configuradas para un tipo de reporte.
        /// Si no existen todavía, las inicializa con los valores por defecto.
        /// </summary>
        public List<ConfigColumnaReporte> ObtenerOCrearColumnas(int proyectoId, string tipoReporte)
        {
            var columnas = _ctx.ConfigColumnasReporte
                .Where(c => c.ProyectoId == proyectoId && c.TipoReporte == tipoReporte)
                .OrderBy(c => c.Orden)
                .ToList();

            if (columnas.Count == 0 && _columnasPorDefecto.TryGetValue(tipoReporte, out var defaults))
            {
                foreach (var d in defaults)
                {
                    d.ProyectoId  = proyectoId;
                    d.TipoReporte = tipoReporte;
                    // Aplicar colores de encabezado por defecto
                    d.EncColorFondo = "#1565C0";
                    d.EncColorTexto = "#FFFFFF";
                    d.EncNegrita    = true;
                    d.EncAlineacion = "Centro";
                    d.EncFuente     = "Segoe UI";
                    d.EncTamaño     = 9f;
                    d.ConFuente     = "Segoe UI";
                    d.ConTamaño     = 9f;
                    d.ConColorFondo = "#FFFFFF";
                    d.ConColorTexto = "#000000";
                    _ctx.ConfigColumnasReporte.Add(d);
                }
                _ctx.SaveChanges();

                columnas = _ctx.ConfigColumnasReporte
                    .Where(c => c.ProyectoId == proyectoId && c.TipoReporte == tipoReporte)
                    .OrderBy(c => c.Orden)
                    .ToList();
            }

            return columnas;
        }

        /// <summary>
        /// Resuelve los campos dinámicos {campo} en un texto,
        /// sustituyendo con los datos reales del proyecto y la plantilla.
        /// Los campos {pagina} y {total_paginas} se dejan para que Excel los resuelva.
        /// </summary>
        public string ResolverCampos(string texto, Proyecto proyecto, PlantillaReporte plantilla)
        {
            if (string.IsNullOrEmpty(texto)) return texto;

            return texto
                .Replace("{nombre_proyecto}",  proyecto?.Nombre          ?? "")
                .Replace("{descripcion}",       proyecto?.Descripcion     ?? "")
                .Replace("{ubicacion}",         proyecto?.Ubicacion       ?? plantilla?.CampoUbicacion ?? "")
                .Replace("{convocante}",        proyecto?.Convocante      ?? "")
                .Replace("{contratista}",       proyecto?.Contratista     ?? "")
                .Replace("{apoderado_legal}",   proyecto?.ApoderadoLegal  ?? "")
                .Replace("{fecha_inicio}",      proyecto?.FechaInicio.ToString("dd/MM/yyyy") ?? plantilla?.CampoFechaInicio ?? "")
                .Replace("{fecha_termino}",     proyecto?.FechaTermino.ToString("dd/MM/yyyy") ?? plantilla?.CampoFechaTermino ?? "")
                .Replace("{plazo_ejecucion}",   proyecto?.PlazoEjecucion.ToString() ?? "")
                .Replace("{elaboro}",           plantilla?.CampoElabaro        ?? "")
                .Replace("{reviso}",            plantilla?.CampoReviso         ?? "")
                .Replace("{autorizo}",          plantilla?.CampoAutorizo       ?? "")
                .Replace("{dependencia}",       plantilla?.CampoDependencia    ?? "")
                .Replace("{numero_contrato}",   plantilla?.CampoNumeroContrato ?? "")
                .Replace("{licitacion}",        plantilla?.CampoLicitacion     ?? "")
                .Replace("{texto1}",            plantilla?.CampoTextoLibre1    ?? "")
                .Replace("{texto2}",            plantilla?.CampoTextoLibre2    ?? "")
                .Replace("{fecha_impresion}",   DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
        }


        public List<PlantillaReporteElemento> ObtenerElementosPlantillaPdf(int plantillaReporteId)
        {
            return _ctx.PlantillasReporteElementos
                .Where(e => e.PlantillaReporteId == plantillaReporteId)
                .OrderBy(e => e.ZOrder)
                .ThenBy(e => e.Id)
                .ToList();
        }

        public void GuardarPlantilla(PlantillaReporte plantilla)
        {
            plantilla.FechaModificacion = DateTime.Now;
            _ctx.PlantillasReporte.Update(plantilla);
            _ctx.SaveChanges();
        }

        public void GuardarColumnas(List<ConfigColumnaReporte> columnas)
        {
            _ctx.ConfigColumnasReporte.UpdateRange(columnas);
            _ctx.SaveChanges();
        }

        public static List<string> TiposReporteDisponibles => new()
        {
            "Presupuesto", "APU", "Explosion", "ManoObra", "Materiales", "Maquinaria"
        };
    }
}
