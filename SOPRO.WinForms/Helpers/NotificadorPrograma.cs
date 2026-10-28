using System;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Seam de mensajes y confirmaciones del módulo de Programa de Obra. En producción
    /// muestra diálogos modales; las pruebas STA lo sustituyen para ejercitar los
    /// handlers reales sin abrir ventanas. Los mensajes son los mismos en ambos casos.
    /// </summary>
    internal sealed class NotificadorPrograma
    {
        /// <summary>Reemplaza a los <see cref="MessageBox"/> informativos/error.</summary>
        public Action<string, MessageBoxIcon>? Mostrar { get; set; }

        /// <summary>Reemplaza a las confirmaciones Sí/No (true = continuar).</summary>
        public Func<string, bool>? Confirmar { get; set; }

        public void Informar(string mensaje) => MostrarMensaje(mensaje, MessageBoxIcon.Information);

        public void Advertir(string mensaje) => MostrarMensaje(mensaje, MessageBoxIcon.Warning);

        public void Error(string mensaje) => MostrarMensaje(mensaje, MessageBoxIcon.Error);

        public bool Preguntar(string mensaje)
            => Confirmar?.Invoke(mensaje)
               ?? MessageBox.Show(
                   mensaje,
                   "Programación",
                   MessageBoxButtons.YesNo,
                   MessageBoxIcon.Warning) == DialogResult.Yes;

        private void MostrarMensaje(string mensaje, MessageBoxIcon icono)
        {
            if (Mostrar is not null)
            {
                Mostrar(mensaje, icono);
                return;
            }

            MessageBox.Show(mensaje, "Programación", MessageBoxButtons.OK, icono);
        }
    }
}
