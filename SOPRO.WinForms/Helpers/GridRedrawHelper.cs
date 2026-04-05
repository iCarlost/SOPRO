using System;
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
