using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    internal static class GridRedrawHelper
    {
        private const int WM_SETREDRAW = 0x000B;
        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_ERASE = 0x0004;
        private const uint RDW_ALLCHILDREN = 0x0080;
        private const uint RDW_UPDATENOW = 0x0100;
        private const int GWL_STYLE = -16;
        private const int WS_VISIBLE = 0x10000000;
        private const int SW_HIDE = 0;
        private const int SW_SHOWNA = 8;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

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

        /// <summary>
        /// Repinta de una sola pasada el subárbol completo. Invalidar y actualizar solo
        /// la raíz pinta primero su fondo y deja los hijos para mensajes posteriores:
        /// en pantalla aparece un frame en blanco entre el estado viejo y el nuevo.
        /// <c>RedrawWindow</c> con <c>RDW_ALLCHILDREN | RDW_UPDATENOW</c> invalida,
        /// borra y actualiza raíz e hijos dentro de la misma llamada.
        /// </summary>
        public static void RedibujarArbol(Control? raiz)
        {
            if (raiz == null || raiz.IsDisposed)
                return;

            try
            {
                if (raiz.IsHandleCreated
                    && RedrawWindow(
                        raiz.Handle,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN | RDW_UPDATENOW))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GridRedrawHelper.RedibujarArbol({raiz.Name}): {ex.Message}");
            }

            if (raiz.IsDisposed)
                return;

            raiz.Invalidate(true);
            raiz.Update();
        }

        /// <summary>
        /// Alinea el bit WS_VISIBLE nativo con la visibilidad administrada. WinForms
        /// omite el ocultamiento nativo cuando la visibilidad efectiva ya era falsa
        /// (por un ancestro oculto o por un ocultamiento anterior): el control queda
        /// con <c>Visible == false</c> pero su ventana sigue en pantalla, incluso
        /// después de volver a mostrar el padre. Solo se sincroniza bajo un padre
        /// visible, donde <c>Visible</c> distingue con certeza el ocultamiento propio.
        /// </summary>
        public static void SincronizarVisibilidadNativa(Control? raiz)
        {
            if (raiz == null || raiz.IsDisposed)
                return;

            SincronizarVisibilidadNativaRecursivo(raiz);
        }

        private static void SincronizarVisibilidadNativaRecursivo(Control control)
        {
            bool completo = true;
            try
            {
                if (control.IsDisposed)
                    return;
                if (control.Parent != null && !control.Parent.Visible)
                    return;
                if (!control.IsHandleCreated)
                    return;

                bool nativaVisible = (GetWindowLong(control.Handle, GWL_STYLE) & WS_VISIBLE) != 0;
                if (control.Visible != nativaVisible)
                    ShowWindow(control.Handle, control.Visible ? SW_SHOWNA : SW_HIDE);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"GridRedrawHelper.SincronizarVisibilidadNativa({control.Name}): {ex.Message}");
            }

            foreach (Control hijo in SnapshotHijos(control, ref completo))
                SincronizarVisibilidadNativaRecursivo(hijo);
        }

        /// <summary>Visibilidad nativa Win32; false sin handle o ante error.</summary>
        public static bool EsVisibleNativo(Control? control)
        {
            if (control == null || control.IsDisposed || !control.IsHandleCreated)
                return false;

            try
            {
                return IsWindowVisible(control.Handle);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GridRedrawHelper.EsVisibleNativo({control.Name}): {ex.Message}");
                return false;
            }
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
