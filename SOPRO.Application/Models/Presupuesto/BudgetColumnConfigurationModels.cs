using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Alcance de un formato global de columnas. <see cref="Full"/> replica el
    /// formato completo de la columna del ribbon (fuente, estilo, alineación,
    /// color de texto, wrap y alineación vertical; el color de fondo NUNCA se
    /// aplica globalmente). <see cref="FontOnly"/> replica el "aplicar a todas"
    /// del diálogo de columnas (solo fuente, tamaño, negrita, cursiva y color de
    /// texto).
    /// </summary>
    public enum BudgetGlobalColumnFormatScope
    {
        Full = 0,
        FontOnly = 1
    }

    /// <summary>
    /// Definición disconnected de una columna personalizada del presupuesto. La
    /// produce la fábrica pura de la vista (23 definiciones predeterminadas o una
    /// columna nueva) y la consume el servicio de Application; no contiene
    /// identidad ni contexto de datos.
    /// </summary>
    public sealed class BudgetColumnDefinitionDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string NombreInterno { get; set; } = string.Empty;
        public TipoColumnaPersonalizada TipoColumna { get; set; } = TipoColumnaPersonalizada.Personal;
        public TipoDatoColumna TipoDato { get; set; } = TipoDatoColumna.Texto;
        public string Formula { get; set; } = string.Empty;
        public bool Visible { get; set; } = true;
        public bool Imprimible { get; set; } = true;
        public int AnchoColumna { get; set; } = 100;
        public int Orden { get; set; }
        public bool Totalizar { get; set; }
        public string CondicionTotalizacion { get; set; } = string.Empty;
        public string FormatoNumerico { get; set; } = string.Empty;
        public string FormatoFecha { get; set; } = string.Empty;
        public AlineacionColumna Alineacion { get; set; } = AlineacionColumna.Izquierda;
        public string NombreFuente { get; set; } = "Segoe UI";
        public int TamanoFuente { get; set; } = 9;
        public string ColorFuente { get; set; } = "#000000";
        public string ColorFondo { get; set; } = "#FFFFFF";
        public bool Negrita { get; set; }
        public bool Cursiva { get; set; }
        public bool WrapTexto { get; set; }
        public int AlineacionVertical { get; set; } = 1;

        public ColumnaPersonalizada ToEntity(int proyectoId)
        {
            return new ColumnaPersonalizada
            {
                ProyectoId = proyectoId,
                Nombre = Nombre,
                NombreInterno = NombreInterno,
                TipoColumna = TipoColumna,
                TipoDato = TipoDato,
                Formula = Formula ?? string.Empty,
                Visible = Visible,
                Imprimible = Imprimible,
                AnchoColumna = AnchoColumna,
                Orden = Orden,
                Totalizar = Totalizar,
                CondicionTotalizacion = CondicionTotalizacion ?? string.Empty,
                FormatoNumerico = FormatoNumerico ?? string.Empty,
                FormatoFecha = FormatoFecha ?? string.Empty,
                Alineacion = Alineacion,
                NombreFuente = NombreFuente,
                TamanoFuente = TamanoFuente,
                ColorFuente = ColorFuente,
                ColorFondo = ColorFondo,
                Negrita = Negrita,
                Cursiva = Cursiva,
                WrapTexto = WrapTexto,
                AlineacionVertical = AlineacionVertical
            };
        }
    }

    /// <summary>
    /// Instantánea inmutable de una columna personalizada confirmada o leída de la
    /// base. La vista la usa para pintar el grid y el diálogo sin compartir
    /// entidades rastreadas.
    /// </summary>
    public sealed class BudgetColumnSnapshot
    {
        internal BudgetColumnSnapshot(
            int id,
            int? proyectoId,
            string nombre,
            string nombreInterno,
            TipoColumnaPersonalizada tipoColumna,
            TipoDatoColumna tipoDato,
            string formula,
            bool visible,
            bool imprimible,
            int anchoColumna,
            int orden,
            bool totalizar,
            string condicionTotalizacion,
            string formatoNumerico,
            string formatoFecha,
            AlineacionColumna alineacion,
            string nombreFuente,
            int tamanoFuente,
            string colorFuente,
            string colorFondo,
            bool negrita,
            bool cursiva,
            bool wrapTexto,
            int alineacionVertical,
            DateTime fechaModificacion)
        {
            Id = id;
            ProyectoId = proyectoId;
            Nombre = nombre;
            NombreInterno = nombreInterno;
            TipoColumna = tipoColumna;
            TipoDato = tipoDato;
            Formula = formula;
            Visible = visible;
            Imprimible = imprimible;
            AnchoColumna = anchoColumna;
            Orden = orden;
            Totalizar = totalizar;
            CondicionTotalizacion = condicionTotalizacion;
            FormatoNumerico = formatoNumerico;
            FormatoFecha = formatoFecha;
            Alineacion = alineacion;
            NombreFuente = nombreFuente;
            TamanoFuente = tamanoFuente;
            ColorFuente = colorFuente;
            ColorFondo = colorFondo;
            Negrita = negrita;
            Cursiva = cursiva;
            WrapTexto = wrapTexto;
            AlineacionVertical = alineacionVertical;
            FechaModificacion = fechaModificacion;
        }

        public int Id { get; }
        public int? ProyectoId { get; }
        public string Nombre { get; }
        public string NombreInterno { get; }
        public TipoColumnaPersonalizada TipoColumna { get; }
        public TipoDatoColumna TipoDato { get; }
        public string Formula { get; }
        public bool Visible { get; }
        public bool Imprimible { get; }
        public int AnchoColumna { get; }
        public int Orden { get; }
        public bool Totalizar { get; }
        public string CondicionTotalizacion { get; }
        public string FormatoNumerico { get; }
        public string FormatoFecha { get; }
        public AlineacionColumna Alineacion { get; }
        public string NombreFuente { get; }
        public int TamanoFuente { get; }
        public string ColorFuente { get; }
        public string ColorFondo { get; }
        public bool Negrita { get; }
        public bool Cursiva { get; }
        public bool WrapTexto { get; }
        public int AlineacionVertical { get; }
        public DateTime FechaModificacion { get; }

        public static BudgetColumnSnapshot FromEntity(ColumnaPersonalizada columna)
        {
            ArgumentNullException.ThrowIfNull(columna);

            return new BudgetColumnSnapshot(
                columna.Id,
                columna.ProyectoId,
                columna.Nombre ?? string.Empty,
                columna.NombreInterno ?? string.Empty,
                columna.TipoColumna,
                columna.TipoDato,
                columna.Formula ?? string.Empty,
                columna.Visible,
                columna.Imprimible,
                columna.AnchoColumna,
                columna.Orden,
                columna.Totalizar,
                columna.CondicionTotalizacion ?? string.Empty,
                columna.FormatoNumerico ?? string.Empty,
                columna.FormatoFecha ?? string.Empty,
                columna.Alineacion,
                columna.NombreFuente ?? string.Empty,
                columna.TamanoFuente,
                columna.ColorFuente ?? string.Empty,
                columna.ColorFondo ?? string.Empty,
                columna.Negrita,
                columna.Cursiva,
                columna.WrapTexto,
                columna.AlineacionVertical,
                columna.FechaModificacion);
        }
    }

    /// <summary>Formato de presentación de una columna, disconnected de la entidad.</summary>
    public sealed class BudgetColumnFormatDto
    {
        public string NombreFuente { get; set; } = "Segoe UI";
        public int TamanoFuente { get; set; } = 9;
        public bool Negrita { get; set; }
        public bool Cursiva { get; set; }
        public AlineacionColumna Alineacion { get; set; } = AlineacionColumna.Izquierda;
        public string ColorFuente { get; set; } = "#000000";
        public string ColorFondo { get; set; } = "#FFFFFF";
        public bool WrapTexto { get; set; }
        public int AlineacionVertical { get; set; } = 1;

        public static BudgetColumnFormatDto FromColumn(ColumnaPersonalizada columna)
        {
            ArgumentNullException.ThrowIfNull(columna);

            return new BudgetColumnFormatDto
            {
                NombreFuente = columna.NombreFuente ?? "Segoe UI",
                TamanoFuente = columna.TamanoFuente,
                Negrita = columna.Negrita,
                Cursiva = columna.Cursiva,
                Alineacion = columna.Alineacion,
                ColorFuente = columna.ColorFuente ?? "#000000",
                ColorFondo = columna.ColorFondo ?? "#FFFFFF",
                WrapTexto = columna.WrapTexto,
                AlineacionVertical = columna.AlineacionVertical
            };
        }
    }

    /// <summary>Entrada del lote atómico de orden de columnas.</summary>
    public readonly record struct BudgetColumnOrderEntry(int ColumnId, int Orden);

    /// <summary>
    /// Resultado de una operación de columnas: clasificación segura, columnas
    /// confirmadas (vacío si no se confirmó) y diagnóstico técnico no mostrable.
    /// </summary>
    public sealed class BudgetColumnConfigurationResult
    {
        internal BudgetColumnConfigurationResult(
            BudgetOperationResult operation,
            IReadOnlyList<BudgetColumnSnapshot>? confirmedColumns,
            string? diagnostic)
        {
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            ConfirmedColumns = confirmedColumns ?? Array.Empty<BudgetColumnSnapshot>();
            Diagnostic = diagnostic;
        }

        public BudgetOperationResult Operation { get; }

        public BudgetOperationStatus Status => Operation.Status;

        public bool IsCommitted => Operation.IsCommitted;

        /// <summary>Mensaje seguro para el usuario.</summary>
        public string? Message => Operation.Message;

        /// <summary>Detalle técnico para registro y pruebas; nunca se muestra en la UI.</summary>
        public string? Diagnostic { get; }

        /// <summary>Columnas del proyecto tal como quedaron; vacío si no se confirmó.</summary>
        public IReadOnlyList<BudgetColumnSnapshot> ConfirmedColumns { get; }
    }
}
