using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Selección total del contenido de un NumericUpDown al adquirir el foco por
    /// Tab/Shift+Tab o en el primer clic de ratón (botón izquierdo); con el foco
    /// ya adquirido, los clics posteriores posicionan el cursor con normalidad.
    /// Comportamiento pasivo: no modifica la visibilidad ni el estado de
    /// habilitación del control. Idempotente: puede llamarse Configurar más de
    /// una vez.
    /// </summary>
    public static class SeleccionTotalNumericUpDown
    {
        private sealed class Estado
        {
            public NumericUpDown Propietario = null!;
            public bool FocoRecuperado;
            public bool ClicSeleccionPendiente;
        }

        private static readonly ConditionalWeakTable<Control, Estado> _estados = new();

        public static void Configurar(NumericUpDown control)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));

            control.Enter -= OnEnter;
            control.Enter += OnEnter;

            TextBox? edicion = ObtenerEdicion(control);
            if (edicion == null) return;

            Estado estado = ObtenerEstado(edicion, control);

            edicion.Leave -= OnEdicionLeave;
            edicion.MouseDown -= OnEdicionMouseDown;
            edicion.MouseUp -= OnEdicionMouseUp;
            edicion.Leave += OnEdicionLeave;
            edicion.MouseDown += OnEdicionMouseDown;
            edicion.MouseUp += OnEdicionMouseUp;
        }

        private static void OnEnter(object? sender, EventArgs e)
        {
            if (sender is not NumericUpDown control) return;

            TextBox? edicion = ObtenerEdicion(control);
            if (edicion == null || !_estados.TryGetValue(edicion, out Estado estado)) return;
            if (!estado.FocoRecuperado) return;
            estado.FocoRecuperado = false;

            // Encolado: en un clic adquisitivo el gesto del ratón recoloca el
            // cursor después de los eventos de foco; encolar asegura que la
            // selección completa sobreviva también al clic y funcione igual
            // por Tab.
            if (!control.IsDisposed && control.IsHandleCreated)
            {
                MethodInvoker accion = delegate
                {
                    if (!control.IsDisposed && !control.Disposing)
                        SeleccionarTodo(control);
                };
                control.BeginInvoke(accion);
            }
            else
            {
                SeleccionarTodo(control);
            }
        }

        private static void OnEdicionLeave(object? sender, EventArgs e)
        {
            if (sender is not Control edicion || !_estados.TryGetValue(edicion, out Estado estado)) return;

            estado.FocoRecuperado = true;
        }

        private static void OnEdicionMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (sender is not Control edicion || !_estados.TryGetValue(edicion, out Estado estado)) return;

            estado.ClicSeleccionPendiente = !estado.Propietario.ContainsFocus;
        }

        private static void OnEdicionMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (sender is not Control edicion || !_estados.TryGetValue(edicion, out Estado estado)) return;
            if (!estado.ClicSeleccionPendiente) return;

            estado.ClicSeleccionPendiente = false;
            SeleccionarTodo(estado.Propietario);
        }

        private static void SeleccionarTodo(NumericUpDown control)
        {
            if (string.IsNullOrEmpty(control.Text)) return;
            ObtenerEdicion(control)?.Select(0, control.Text.Length);
        }

        private static TextBox? ObtenerEdicion(NumericUpDown control)
        {
            return control.Controls.OfType<TextBox>().FirstOrDefault();
        }

        private static Estado ObtenerEstado(Control edicion, NumericUpDown propietario)
        {
            if (!_estados.TryGetValue(edicion, out Estado estado))
            {
                estado = new Estado
                {
                    Propietario = propietario,
                    FocoRecuperado = true
                };
                _estados.Add(edicion, estado);
                return estado;
            }

            estado.Propietario = propietario;
            return estado;
        }
    }
}
