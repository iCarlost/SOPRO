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

        /// <summary>
        /// Fallback NO modal para procesos de prueba (STA): si una vista no inyectó su
        /// propio <see cref="Mostrar"/>, el mensaje se entrega aquí en lugar de abrir un
        /// <see cref="MessageBox"/> modal que colgaría el hilo de UI del test. En
        /// producción queda null y se mantiene el diálogo modal.
        /// </summary>
        internal static Action<string, MessageBoxIcon>? MostrarPredeterminado { get; set; }

        /// <summary>
        /// Fallback NO modal para confirmaciones en pruebas: si una vista no inyectó
        /// <see cref="Confirmar"/>, se responde <c>false</c> (cancelar) en lugar de abrir
        /// un diálogo modal. En producción queda null.
        /// </summary>
        internal static Func<string, bool>? ConfirmarPredeterminado { get; set; }

        /// <summary>
        /// Veces que un mensaje se entregó al fallback no modal. Es un seam de pruebas:
        /// permite afirmar que una vista usó su notificador inyectado y que ningún
        /// mensaje cayó en la ruta modal.
        /// </summary>
        internal static int VecesUsadoFallbackNoModal { get; private set; }

        /// <summary>
        /// Veces que una confirmación se entregó al fallback no modal (sin seam de
        /// instancia). El cleanup de la suite falla si es mayor que cero.
        /// </summary>
        internal static int VecesUsadoConfirmacionNoModal { get; private set; }

        /// <summary>Contadores del fallback no modal, para snapshot/restauración de la suite.</summary>
        internal static (int Mensajes, int Confirmaciones) ContadoresFallbackNoModal
            => (VecesUsadoFallbackNoModal, VecesUsadoConfirmacionNoModal);

        /// <summary>Restaura los contadores del fallback (limpieza de la suite de pruebas).</summary>
        internal static void RestaurarContadoresFallbackNoModal(int mensajes, int confirmaciones)
        {
            VecesUsadoFallbackNoModal = mensajes;
            VecesUsadoConfirmacionNoModal = confirmaciones;
        }

        public void Informar(string mensaje) => MostrarMensaje(mensaje, MessageBoxIcon.Information);

        public void Advertir(string mensaje) => MostrarMensaje(mensaje, MessageBoxIcon.Warning);

        public void Error(string mensaje) => MostrarMensaje(mensaje, MessageBoxIcon.Error);

        public bool Preguntar(string mensaje)
        {
            if (Confirmar is not null)
                return Confirmar(mensaje);

            if (ConfirmarPredeterminado is not null)
            {
                VecesUsadoConfirmacionNoModal++;
                return ConfirmarPredeterminado(mensaje);
            }

            return MessageBox.Show(
                mensaje,
                "Programación",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        private void MostrarMensaje(string mensaje, MessageBoxIcon icono)
        {
            if (Mostrar is not null)
            {
                Mostrar(mensaje, icono);
                return;
            }

            if (MostrarPredeterminado is not null)
            {
                VecesUsadoFallbackNoModal++;
                MostrarPredeterminado(mensaje, icono);
                return;
            }

            MessageBox.Show(mensaje, "Programación", MessageBoxButtons.OK, icono);
        }
    }
}
