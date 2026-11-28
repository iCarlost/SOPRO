using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Diálogo de apariencia (issue #203): selección del preset de tema que
    /// SOPRO aplicará en el PRÓXIMO inicio. Code-only (sin diseñador), con el
    /// cromo compartido de diálogos SOPRO.
    ///
    /// Contrato de fase 1: el diálogo NUNCA llama a
    /// <see cref="SoproTheme.InitializePalette(SoproTemaPreset)"/> ni muta
    /// <see cref="SoproTheme.CurrentPalette"/>; solo lee la preferencia
    /// persistida con <see cref="SoproTemaStore.Load"/> y la escribe con
    /// <see cref="SoproTemaStore.TrySave"/>. Las vistas previas usan
    /// <see cref="SoproTheme.GetPalette(SoproTemaPreset)"/> (paleta ajena al
    /// tema activo) para pintar la mini-muestra de cada opción.
    /// </summary>
    internal sealed class FormApariencia : Form
    {
        /// <summary>Texto exacto del aviso de aplicación al reiniciar.</summary>
        internal const string AvisoReinicio = "El tema guardado se aplicará al reiniciar SOPRO.";

        /// <summary>Mensaje mostrado cuando la preferencia no pudo persistirse.</summary>
        internal const string MensajeErrorGuardado = "No se pudo guardar el tema. Revisa los permisos de tu carpeta de datos e intenta de nuevo.";

        /// <summary>Ancho cliente del diálogo (px a 96 DPI). Fijo: FixedDialog.</summary>
        internal const int AnchoCliente = 460;

        /// <summary>Alto cliente del diálogo (px a 96 DPI) con holgura para el mensaje de error envuelto.</summary>
        internal const int AltoCliente = 400;

        private readonly SoproTemaStore _store;
        private readonly RadioButton[] _opciones;
        private readonly Label _estado = new();
        private bool _guardado;

        /// <summary>
        /// Crea el diálogo. Sin argumento usa el almacén real de la aplicación
        /// (carpeta de datos local); las pruebas inyectan uno sobre un
        /// directorio temporal.
        /// </summary>
        public FormApariencia(SoproTemaStore? store = null)
        {
            _store = store ?? new SoproTemaStore();

            Text = "Apariencia";
            AccessibleDescription = "Selección del tema de la interfaz. El cambio se aplica al reiniciar SOPRO.";

            var btnGuardar = SoproDialogTheme.CrearBotonPrimario("Guardar");
            var btnCancelar = SoproDialogTheme.CrearBotonSecundario("Cancelar");
            btnGuardar.Click += (_, _) => Guardar();

            var contenido = CrearContenido(out _opciones);

            SoproDialogTheme.AplicarCromoEditor(
                this,
                new Size(AnchoCliente, AltoCliente),
                "Apariencia",
                "Elige el tema de la interfaz.",
                contenido,
                btnGuardar,
                btnCancelar);

            // Selección inicial: la preferencia ya persistida (o el
            // predeterminado). No se altera la paleta activa del proceso.
            var guardado = _store.Load();
            foreach (var opcion in _opciones)
            {
                if (opcion.Tag is SoproTemaPreset preset && preset == guardado)
                {
                    opcion.Checked = true;
                    break;
                }
            }
        }

        /// <summary>Almacén de preferencia inyectado o real (solo lectura externa).</summary>
        internal SoproTemaStore Store => _store;

        /// <summary>Preset seleccionado en este momento (RadioButton marcado).</summary>
        internal SoproTemaPreset PresetSeleccionado
            => _opciones.Where(opcion => opcion.Checked)
                .OfType<RadioButton>()
                .Select(opcion => opcion.Tag)
                .OfType<SoproTemaPreset>()
                .DefaultIfEmpty(SoproTemaPreset.ClaroAzul)
                .First();

        /// <summary>Rótulo de estado para errores de guardado (visible solo si falló).</summary>
        internal Label EstadoGuardado => _estado;

        // ------------------------------------------------------------------
        // Contenido
        // ------------------------------------------------------------------

        private Control CrearContenido(out RadioButton[] opciones)
        {
            // Rejilla del cuerpo: grupo de opciones, aviso de reinicio y
            // rótulo de estado (solo ante errores). Filas AutoSize medidas
            // con la fuente real: sin recortes en ningún DPI.
            var tabla = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = SoproTheme.Colors.Surface,
                ColumnCount = 1,
                AutoSize = true,
                Margin = new Padding(0)
            };
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var grupo = CrearGrupoOpciones(out opciones);
            tabla.Controls.Add(grupo, 0, 0);

            var aviso = new Label
            {
                AutoSize = true,
                Text = AvisoReinicio,
                ForeColor = SoproTheme.Colors.Muted,
                Font = SoproTheme.Fonts.Subtitle,
                Margin = new Padding(0, 12, 0, 0)
            };
            tabla.Controls.Add(aviso, 0, 1);

            _estado.AutoSize = true;
            _estado.Text = string.Empty;
            _estado.Visible = false;
            _estado.ForeColor = SoproTheme.Colors.Error;
            _estado.Font = SoproTheme.Fonts.Subtitle;
            _estado.Margin = new Padding(0, 8, 0, 0);
            tabla.Controls.Add(_estado, 0, 2);

            // Envoltura del mensaje de error: AutoSize + MaximumSize con el
            // ancho REAL del contenido de la tarjeta. El mensaje mide ~532px
            // a una línea a 96 DPI frente a ~410px disponibles, así que DEBE
            // envolver (el propio Label mide con WordBreak) y crecer en ALTO,
            // sin desbordar horizontalmente ni invadir encabezado/pie. Se
            // recalcula ante cambios de tamaño/DPI del contenedor.
            void AjustarEnvolturaEstado()
            {
                if (tabla.IsDisposed) return;

                int anchoContenido = tabla.ClientSize.Width > 1
                    ? tabla.ClientSize.Width
                    : SoproTheme.EscalarSegunDpi(this, AnchoCliente - 2 * SoproDialogTheme.MargenHorizontalContenido - 2);
                _estado.MaximumSize = new Size(Math.Max(1, anchoContenido - _estado.Margin.Horizontal), 0);
            }
            tabla.SizeChanged += (_, _) => AjustarEnvolturaEstado();
            tabla.HandleCreated += (_, _) => AjustarEnvolturaEstado();
            AjustarEnvolturaEstado();

            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            return tabla;
        }

        private GroupBox CrearGrupoOpciones(out RadioButton[] opciones)
        {
            var grupo = new GroupBox
            {
                Text = "Tema de la interfaz",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                BackColor = SoproTheme.Colors.Surface,
                ForeColor = SoproTheme.Colors.Ink,
                Font = SoproTheme.Fonts.GroupTitle,
                Margin = new Padding(0)
            };

            // Rejilla por opción: mini-muestra de paleta + radio accesible.
            // La navegación por flechas y Espacio la resuelve el GroupBox
            // nativo con RadioButtons reales.
            var filas = new (SoproTemaPreset Preset, string Etiqueta)[]
            {
                (SoproTemaPreset.ClaroAzul, "Claro azul (predeterminado)"),
                (SoproTemaPreset.Grafito, "Oscuro grafito"),
                (SoproTemaPreset.ClaroVerde, "Claro verde")
            };

            var rejilla = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = filas.Length,
                BackColor = SoproTheme.Colors.Surface,
                Margin = new Padding(0),
                Padding = new Padding(12, 4, 12, 8)
            };
            rejilla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            rejilla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            opciones = new RadioButton[filas.Length];

            for (int i = 0; i < filas.Length; i++)
            {
                var preset = filas[i].Preset;
                var paleta = SoproTheme.GetPalette(preset);

                var muestra = CrearMiniMuestra(paleta, i);
                rejilla.Controls.Add(muestra, 0, i);

                var radio = new RadioButton
                {
                    Text = filas[i].Etiqueta,
                    Tag = preset,
                    AutoSize = true,
                    Cursor = Cursors.Hand,
                    ForeColor = SoproTheme.Colors.Ink,
                    Font = SoproTheme.Fonts.DialogBody,
                    AccessibleName = $"Tema {filas[i].Etiqueta}",
                    AccessibleRole = AccessibleRole.RadioButton,
                    Margin = new Padding(8, 8, 0, 0)
                };
                rejilla.Controls.Add(radio, 1, i);
                opciones[i] = radio;
            }

            grupo.Controls.Add(rejilla);
            return grupo;
        }

        /// <summary>
        /// Mini-muestra de la paleta de una opción: tres bandas (superficie,
        /// acento de marca y texto) tomadas de los tokens de vista previa de
        /// la paleta del preset — jamás del tema activo — más un borde
        /// hairline para verla sobre la tarjeta.
        /// </summary>
        private Panel CrearMiniMuestra(SoproTemaPaleta paleta, int indice)
        {
            var muestra = new Panel
            {
                Name = $"swatch{indice}",
                Tag = paleta,
                Size = new Size(SoproTheme.EscalarSegunDpi(this, 46), SoproTheme.EscalarSegunDpi(this, 22)),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 4, 10, 0),
                AccessibleName = "Muestra de colores del tema",
                AccessibleRole = AccessibleRole.Graphic
            };
            muestra.Paint += (_, e) =>
            {
                var rect = muestra.ClientRectangle;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                using (var brocha = new SolidBrush(paleta.PreviewFondo))
                    e.Graphics.FillRectangle(brocha, rect);

                int banda = rect.Width / 3;
                using (var acento = new SolidBrush(paleta.PreviewAcento))
                    e.Graphics.FillRectangle(acento, rect.Left + banda, 0, banda, rect.Height);
                using (var texto = new SolidBrush(paleta.PreviewTinta))
                    e.Graphics.FillRectangle(texto, rect.Left + 2 * banda, 0, rect.Width - 2 * banda, rect.Height);

                using var borde = new Pen(paleta.PreviewBorde);
                e.Graphics.DrawRectangle(borde, 0, 0, rect.Width - 1, rect.Height - 1);
            };
            return muestra;
        }

        // ------------------------------------------------------------------
        // Guardado
        // ------------------------------------------------------------------

        private void Guardar()
        {
            if (_guardado) return;

            var seleccionado = PresetSeleccionado;
            if (!_store.TrySave(seleccionado))
            {
                // El diálogo NO se cierra ni anuncia éxito: el aviso queda a
                // la vista y el usuario decide (reintentar o Cancelar).
                _estado.Text = MensajeErrorGuardado;
                _estado.Visible = true;
                return;
            }

            _guardado = true;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
