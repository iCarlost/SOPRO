using System;
using System.Collections.Generic;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;
using SOPRO.Reporting.Formatting;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Coordina el DESTINO del formato del ribbon sobre el grid activo:
    /// <list type="bullet">
    ///   <item>Si el grid implementa <see cref="IGridSeleccionFormato"/> y tiene
    ///   celdas seleccionadas, aplica el formato POR CELDA (pintado inmediato) y
    ///   produce los pares antes/después por dirección estable para el undo y la
    ///   persistencia (Fase 4.1).</item>
    ///   <item>En caso contrario conserva intacto el comportamiento por COLUMNA
    ///   de <see cref="IGridFormato"/>.</item>
    /// </list>
    /// El coordinador es agnóstico al <c>DataGridView</c> concreto: sólo habla
    /// con los contratos, de modo que es verificable sin UI real.
    /// </summary>
    public sealed class FormatoSeleccionCoordinator
    {
        private IGridFormato? _form;
        private IReadOnlyList<CambioFormatoSeleccion> _ultimosCambios = Array.Empty<CambioFormatoSeleccion>();

        /// <summary>
        /// Fija el grid activo (o <c>null</c> cuando no hay ninguno). Un cambio
        /// real de grid descarta los pares de la última aplicación.
        /// </summary>
        public void EstablecerGrid(IGridFormato? form)
        {
            if (ReferenceEquals(_form, form)) return;
            _form = form;
            _ultimosCambios = Array.Empty<CambioFormatoSeleccion>();
        }

        private IGridSeleccionFormato? GridCeldas => _form as IGridSeleccionFormato;

        /// <summary>
        /// Pares antes/después de la última aplicación a la selección de celdas,
        /// listos para que el consumidor de persistencia los persista/simplemente
        /// los agrupe como UNA entrada de undo.
        /// </summary>
        public IReadOnlyList<CambioFormatoSeleccion> UltimosCambios => _ultimosCambios;

        /// <summary>
        /// Destino actual del ribbon según la selección del grid activo:
        /// encabezado → <see cref="DestinoFormatoGrid.Columna"/>; celdas →
        /// <see cref="DestinoFormatoGrid.Celdas"/>.
        /// </summary>
        public DestinoFormatoGrid Destino
        {
            get
            {
                if (_form is null) return DestinoFormatoGrid.Ninguno;

                if (GridCeldas is { TieneSeleccionCeldas: true } && ObtenerCeldasFormateables().Count > 0)
                    return DestinoFormatoGrid.Celdas;

                return _form.ColumnaSeleccionada != null
                    ? DestinoFormatoGrid.Columna
                    : DestinoFormatoGrid.Ninguno;
            }
        }

        /// <summary>
        /// Descripción consultable por el ribbon de "qué se modificará"
        /// (p. ej. "columna Costo" o "selección de 3 celda(s)").
        /// </summary>
        public string DescripcionDestino => Destino switch
        {
            DestinoFormatoGrid.Columna => $"columna {_form?.ColumnaSeleccionada?.Nombre}".TrimEnd(),
            DestinoFormatoGrid.Celdas => $"selección de {ObtenerCeldasFormateables().Count} celda(s)",
            _ => "sin selección",
        };

        /// <summary>
        /// <c>true</c> cuando la próxima aplicación de formato irá a la selección
        /// de celdas; <c>false</c> cuando debe usarse el comportamiento por columna.
        /// </summary>
        public bool PuedeAplicarASeleccion => Destino == DestinoFormatoGrid.Celdas;

        /// <summary>
        /// <c>true</c> cuando las celdas formateables seleccionadas NO comparten
        /// el mismo ESTILO EFECTIVO (el ribbon debe pintar un estado mixto /
        /// indeterminado). Se compara el estilo efectivo (no sólo el override
        /// puntual) para que dos celdas de columnas o roles distintos se traten
        /// como distintas, igual que haría Excel.
        /// </summary>
        public bool EstadoMixto
        {
            get
            {
                var celdas = ObtenerCeldasFormateables();
                if (celdas.Count < 2 || GridCeldas is not { } grid) return false;

                ReportStyleOverlay? referencia = null;
                bool primera = true;
                foreach (var direccion in celdas)
                {
                    var estilo = grid.ObtenerEstiloEfectivo(direccion);
                    if (primera)
                    {
                        referencia = estilo;
                        primera = false;
                        continue;
                    }

                    if (!Equals(referencia, estilo)) return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Estilo EFECTIVO UNIFORME de la selección de celdas: el de una única
        /// celda, o el de todas cuando comparten el MISMO estilo efectivo. Devuelve
        /// <c>null</c> cuando no hay selección o cuando las celdas difieren
        /// (estado MIXTO) — en ambos casos el ribbon muestra un estado neutro sin
        /// arrastrar el valor de la celda anterior. Lo consume
        /// <c>ActualizarEstadoFormatoRibbon</c> para volcar el formato a los
        /// controles (P2).
        /// </summary>
        public ReportStyleOverlay? EstiloEfectivoSeleccion
        {
            get
            {
                var celdas = ObtenerCeldasFormateables();
                if (celdas.Count == 0 || GridCeldas is not { } grid) return null;

                ReportStyleOverlay? referencia = null;
                bool primera = true;
                foreach (var direccion in celdas)
                {
                    var estilo = grid.ObtenerEstiloEfectivo(direccion);
                    if (primera)
                    {
                        referencia = estilo;
                        primera = false;
                        continue;
                    }

                    // Cualquier diferencia => estado mixto: sin estilo uniforme.
                    if (!Equals(referencia, estilo)) return null;
                }

                return referencia;
            }
        }

        /// <summary>
        /// Aplica el overlay a la selección de celdas (pintado inmediato) y
        /// devuelve los pares antes/después, deduplicados y filtrados. Si el
        /// destino no es la selección, no modifica nada y devuelve lista vacía.
        /// <para>
        /// El overlay entrante se interpreta como un DELTA PARCIAL: se FUSIONA
        /// propiedad a propiedad sobre el overlay PREVIO de CADA celda (una
        /// propiedad <c>null</c> deja intacto el valor anterior y una no nula gana).
        /// Así cada celda conserva EXACTAMENTE las propiedades que se le han
        /// aplicado y una edición (p. ej. alineación) NO arrastra ni pisa el estado
        /// de otra celda (p. ej. su color de fondo).
        /// </para>
        /// </summary>
        public IReadOnlyList<CambioFormatoSeleccion> AplicarASeleccion(ReportStyleOverlay overlay)
        {
            ArgumentNullException.ThrowIfNull(overlay);

            var grid = GridCeldas;
            if (grid is null || Destino != DestinoFormatoGrid.Celdas)
            {
                _ultimosCambios = Array.Empty<CambioFormatoSeleccion>();
                return _ultimosCambios;
            }

            var cambios = new List<CambioFormatoSeleccion>();
            foreach (var direccion in ObtenerCeldasFormateables())
            {
                var antes = grid.ObtenerOverlayActual(direccion);
                // Fusión parcial: el overlay entrante (delta) gana sobre el previo
                // celda a celda, sin tocar otras celdas.
                var despues = ReportCellStyleResolver.Combinar(antes, overlay);
                grid.AplicarOverlayCelda(direccion, despues);
                cambios.Add(new CambioFormatoSeleccion(direccion, antes, despues));
            }

            _ultimosCambios = cambios;
            return cambios;
        }

        /// <summary>
        /// Celdas seleccionadas, deduplicadas por dirección estable y filtradas a
        /// las formateables (sin filas vacías ni columnas no exportables). Se
        /// conserva el orden de primera aparición.
        /// </summary>
        private List<ReportCellAddress> ObtenerCeldasFormateables()
        {
            var resultado = new List<ReportCellAddress>();
            if (GridCeldas is not { } grid) return resultado;

            var vistas = new HashSet<ReportCellAddress>();
            foreach (var direccion in grid.ObtenerDireccionesSeleccionadas())
            {
                if (direccion is null) continue;
                if (!vistas.Add(direccion)) continue;          // deduplica Ctrl / discontinuas
                if (!grid.EsCeldaFormateable(direccion)) continue; // filas vacías / col. no exportables
                resultado.Add(direccion);
            }

            return resultado;
        }

        // ══════════════════════ Conversión ribbon → overlay neutral ══════════════════════

        /// <summary>
        /// Construye el overlay NEUTRAL a partir del formato del ribbon
        /// (<see cref="ColumnaPersonalizada"/>). La alineación se mapea a los enums
        /// neutrales del contrato de reporting (mismos órdenes numéricos que la
        /// entidad) y los colores viajan como cadenas hexadecimales.
        /// </summary>
        public static ReportStyleOverlay ConstruirOverlay(ColumnaPersonalizada fmt)
        {
            ArgumentNullException.ThrowIfNull(fmt);

            return new ReportStyleOverlay(
                Fuente: string.IsNullOrWhiteSpace(fmt.NombreFuente) ? null : fmt.NombreFuente,
                TamanoFuente: fmt.TamanoFuente > 0 ? fmt.TamanoFuente : (float?)null,
                Negrita: fmt.Negrita,
                Cursiva: fmt.Cursiva,
                ColorFuente: string.IsNullOrWhiteSpace(fmt.ColorFuente) ? null : fmt.ColorFuente,
                ColorFondo: string.IsNullOrWhiteSpace(fmt.ColorFondo) ? null : fmt.ColorFondo,
                AlineacionHorizontal: fmt.Alineacion switch
                {
                    AlineacionColumna.Centro => ReportTextAlignment.Centro,
                    AlineacionColumna.Derecha => ReportTextAlignment.Derecha,
                    AlineacionColumna.Justificado => ReportTextAlignment.Justificado,
                    _ => ReportTextAlignment.Izquierda,
                },
                AlineacionVertical: fmt.AlineacionVertical switch
                {
                    0 => ReportVerticalAlignment.Superior,
                    2 => ReportVerticalAlignment.Inferior,
                    _ => ReportVerticalAlignment.Medio,
                },
                Wrap: fmt.WrapTexto);
        }
    }
}
