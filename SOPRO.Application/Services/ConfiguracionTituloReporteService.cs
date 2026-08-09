using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public class ConfiguracionTituloReporteService
    {
        private readonly SOPROContext _context;

        public ConfiguracionTituloReporteService(SOPROContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public ConfiguracionTituloReporte ObtenerOCrear(int proyectoId, string modulo, string tituloDefault)
        {
            if (proyectoId <= 0) throw new ArgumentOutOfRangeException(nameof(proyectoId));
            if (string.IsNullOrWhiteSpace(modulo)) throw new ArgumentException("El módulo es requerido.", nameof(modulo));

            var cfg = _context.ConfiguracionesTituloReporte
                .FirstOrDefault(x => x.ProyectoId == proyectoId && x.Modulo == modulo);

            if (cfg != null)
                return cfg;

            cfg = new ConfiguracionTituloReporte
            {
                ProyectoId = proyectoId,
                Modulo = modulo,
                TextoTitulo = tituloDefault ?? string.Empty,
                NombreFuente = "Segoe UI",
                TamanoFuente = 13f,
                Negrita = true,
                Cursiva = false,
                ColorTexto = "#FFFFFF"
            };

            _context.ConfiguracionesTituloReporte.Add(cfg);
            _context.SaveChanges();
            return cfg;
        }

        public ConfiguracionTituloReporte Guardar(int proyectoId, string modulo, string textoTitulo, string nombreFuente, float tamanoFuente, bool negrita, bool cursiva, string colorTexto)
        {
            var cfg = ObtenerOCrear(proyectoId, modulo, textoTitulo);
            cfg.TextoTitulo = textoTitulo ?? string.Empty;
            cfg.NombreFuente = string.IsNullOrWhiteSpace(nombreFuente) ? "Segoe UI" : nombreFuente;
            cfg.TamanoFuente = tamanoFuente > 0 ? tamanoFuente : 13f;
            cfg.Negrita = negrita;
            cfg.Cursiva = cursiva;
            cfg.ColorTexto = string.IsNullOrWhiteSpace(colorTexto) ? "#FFFFFF" : colorTexto;
            _context.SaveChanges();
            return cfg;
        }
    }
}
