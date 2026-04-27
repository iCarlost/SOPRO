using System;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Plantilla de encabezado y pie de página para reportes.
    /// Una por proyecto, personalizable con zonas izquierda/centro/derecha
    /// y campos dinámicos como {nombre_proyecto}, {fecha_impresion}, etc.
    /// </summary>
    public class PlantillaReporte
    {
        public int Id { get; set; }
        public int ProyectoId { get; set; }

        // ── ENCABEZADO ───────────────────────────────────────────────────────
        // Cada zona puede contener texto con campos dinámicos y/o ruta de imagen
        public string EncabezadoIzqTipo    { get; set; } = "Texto";   // "Texto" | "Imagen"
        public string EncabezadoIzqContenido { get; set; } = "";      // texto o ruta de imagen
        public string EncabezadoIzqFuente  { get; set; } = "Segoe UI";
        public float  EncabezadoIzqTamaño  { get; set; } = 9f;
        public bool   EncabezadoIzqNegrita { get; set; } = false;
        public bool   EncabezadoIzqCursiva { get; set; } = false;
        public string EncabezadoIzqAlineacion { get; set; } = "Izquierda";

        public string EncabezadoCenTipo    { get; set; } = "Texto";
        public string EncabezadoCenContenido { get; set; } = "{nombre_proyecto}";
        public string EncabezadoCenFuente  { get; set; } = "Segoe UI";
        public float  EncabezadoCenTamaño  { get; set; } = 11f;
        public bool   EncabezadoCenNegrita { get; set; } = true;
        public bool   EncabezadoCenCursiva { get; set; } = false;
        public string EncabezadoCenAlineacion { get; set; } = "Centro";

        public string EncabezadoDerTipo    { get; set; } = "Texto";
        public string EncabezadoDerContenido { get; set; } = "{fecha_impresion}";
        public string EncabezadoDerFuente  { get; set; } = "Segoe UI";
        public float  EncabezadoDerTamaño  { get; set; } = 9f;
        public bool   EncabezadoDerNegrita { get; set; } = false;
        public bool   EncabezadoDerCursiva { get; set; } = false;
        public string EncabezadoDerAlineacion { get; set; } = "Derecha";

        public int    EncabezadoAltura     { get; set; } = 60;  // px en pantalla / pts en Excel

        // ── PIE DE PÁGINA ────────────────────────────────────────────────────
        public string PiePaginaIzqTipo    { get; set; } = "Texto";
        public string PiePaginaIzqContenido { get; set; } = "{elaboro}";
        public string PiePaginaIzqFuente  { get; set; } = "Segoe UI";
        public float  PiePaginaIzqTamaño  { get; set; } = 9f;
        public bool   PiePaginaIzqNegrita { get; set; } = false;
        public bool   PiePaginaIzqCursiva { get; set; } = false;
        public string PiePaginaIzqAlineacion { get; set; } = "Izquierda";

        public string PiePaginaCenTipo    { get; set; } = "Texto";
        public string PiePaginaCenContenido { get; set; } = "Pagina {pagina} de {total_paginas}";
        public string PiePaginaCenFuente  { get; set; } = "Segoe UI";
        public float  PiePaginaCenTamaño  { get; set; } = 8f;
        public bool   PiePaginaCenNegrita { get; set; } = false;
        public bool   PiePaginaCenCursiva { get; set; } = false;
        public string PiePaginaCenAlineacion { get; set; } = "Centro";

        public string PiePaginaDerTipo    { get; set; } = "Texto";
        public string PiePaginaDerContenido { get; set; } = "{reviso}";
        public string PiePaginaDerFuente  { get; set; } = "Segoe UI";
        public float  PiePaginaDerTamaño  { get; set; } = 9f;
        public bool   PiePaginaDerNegrita { get; set; } = false;
        public bool   PiePaginaDerCursiva { get; set; } = false;
        public string PiePaginaDerAlineacion { get; set; } = "Derecha";

        public int    PiePaginaAltura     { get; set; } = 40;

        // ── DATOS DINÁMICOS DEL PROYECTO ─────────────────────────────────────
        // Estos son los valores que se sustituyen en los campos {campo}
        public string CampoElabaro        { get; set; } = "";   // {elaboro}
        public string CampoReviso         { get; set; } = "";   // {reviso}
        public string CampoAutorizo       { get; set; } = "";   // {autorizo}
        public string CampoDependencia    { get; set; } = "";   // {dependencia}
        public string CampoNumeroContrato { get; set; } = "";   // {numero_contrato}
        public string CampoLicitacion     { get; set; } = "";   // {licitacion}
        public string CampoUbicacion      { get; set; } = "";   // {ubicacion}
        public string CampoFechaInicio    { get; set; } = "";   // {fecha_inicio}
        public string CampoFechaTermino   { get; set; } = "";   // {fecha_termino}
        public string CampoTextoLibre1    { get; set; } = "";   // {texto1}
        public string CampoTextoLibre2    { get; set; } = "";   // {texto2}

        // ── CONTROL ──────────────────────────────────────────────────────────
        public DateTime FechaModificacion { get; set; } = DateTime.Now;

        // Alturas de franja en dmm para el diseñador PDF (M012)
        // Independientes de EncabezadoAltura/PiePaginaAltura (que son px/pts para Excel)
        public int AlturaEncabezadoDmm { get; set; } = 400;  // 40 mm
        public int AlturaPieDmm        { get; set; } = 200;  // 20 mm

        // Navegación
        public virtual Proyecto Proyecto  { get; set; }
    }
}
