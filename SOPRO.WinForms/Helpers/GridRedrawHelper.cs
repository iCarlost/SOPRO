using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    internal static class GridRedrawHelper
    {
        private const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public static IDisposable Suspend(Control control)
        {
            if (control == null || !control.IsHandleCreated)
                return new NoOpDisposable();

            SendMessage(control.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            return new ResumeDisposable(control);
        }

        /// <summary>
        /// Aplica WM_SETREDRAW a la raíz y a TODOS sus descendientes. Para
        /// deshabilitar se recorre raíz→hijos; para reanudar, hijos→raíz, de modo
        /// que la invalidación final de la raíz repinta el subárbol completo en una
        /// sola pasada (transición visual atómica). El recorrido NUNCA aborta: se
        /// hace snapshot de los hijos por nodo (Controls puede mutar durante el
        /// recorrido) y cada control/subárbol es best-effort. Devuelve true sólo si
        /// todos los handles alcanzados quedaron en el estado pedido; una raíz sin
        /// handle no es fallo (sus hijos con handle siguen recorriéndose, p. ej.
        /// durante la destrucción del ciclo de handle).
        /// </summary>
        public static bool SetRedrawArbol(Control? raiz, bool habilitar)
        {
            if (raiz == null)
                return false;

            return AplicarSetRedrawRecursivo(raiz, habilitar);
        }

        private static bool AplicarSetRedrawRecursivo(Control control, bool habilitar)
        {
            bool completo = true;

            if (habilitar)
            {
                foreach (Control hijo in SnapshotHijos(control, ref completo))
                    completo &= AplicarSetRedrawRecursivo(hijo, habilitar);
            }

            try
            {
                if (control.IsHandleCreated)
                    SendMessage(control.Handle, WM_SETREDRAW, habilitar ? new IntPtr(1) : IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GridRedrawHelper.SetRedrawArbol({control.Name}): {ex.Message}");
                completo = false;
            }

            if (!habilitar)
            {
                foreach (Control hijo in SnapshotHijos(control, ref completo))
                    completo &= AplicarSetRedrawRecursivo(hijo, habilitar);
            }

            return completo;
        }

        private static List<Control> SnapshotHijos(Control control, ref bool completo)
        {
            try
            {
                return control.Controls.Cast<Control>().ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GridRedrawHelper.SnapshotHijos({control.Name}): {ex.Message}");
                completo = false;
                return new List<Control>();
            }
        }

        private sealed class ResumeDisposable : IDisposable
        {
            private Control _control;
            private bool _disposed;

            public ResumeDisposable(Control control)
            {
                _control = control;
            }

            public void Dispose()
            {
                if (_disposed || _control == null) return;
                _disposed = true;

                if (_control.IsHandleCreated)
                {
                    SendMessage(_control.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                    _control.Invalidate(true);
                    _control.Refresh();
                }

                _control = null;
            }
        }

        private sealed class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
