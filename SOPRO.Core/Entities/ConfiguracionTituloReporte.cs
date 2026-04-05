namespace SOPRO.Core.Entities
{
    public class ConfiguracionTituloReporte
    {
        public int Id { get; set; }
        public int ProyectoId { get; set; }
        public string Modulo { get; set; } = string.Empty;
        public string TextoTitulo { get; set; } = string.Empty;
        public string NombreFuente { get; set; } = "Segoe UI";
        public float TamanoFuente { get; set; } = 13f;
        public bool Negrita { get; set; } = true;
        public bool Cursiva { get; set; } = false;
        public string ColorTexto { get; set; } = "#FFFFFF";

        public virtual Proyecto? Proyecto { get; set; }
    }
}
