using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.TestInfrastructure;

/// <summary>
/// Red de seguridad de TODA la suite WinForms (STA): ningún <c>MessageBox</c> modal
/// puede colgar el hilo de UI de un test. Si un formulario no inyecta su propio
/// <see cref="NotificadorPrograma"/> antes de disparar una operación, el mensaje (o la
/// confirmación) se captura aquí; al final, cualquier uso del fallback hace fallar la
/// corrida indicando el texto, para localizar la vista que faltó inyectar.
/// En producción este fallback no existe: se conserva el diálogo modal.
/// </summary>
[TestClass]
public class NotificadorNoModalSetup
{
    private static readonly List<string> _mensajesCapturados = new();
    private static readonly List<string> _confirmacionesCapturadas = new();

    private static Action<string, System.Windows.Forms.MessageBoxIcon>? _mostrarPrevio;
    private static Func<string, bool>? _confirmarPrevio;
    private static (int Mensajes, int Confirmaciones) _contadoresPrevios;

    /// <summary>Mensajes que alguna vista emitió sin notificador inyectado.</summary>
    public static IReadOnlyList<string> MensajesCapturados => _mensajesCapturados;

    /// <summary>Confirmaciones que alguna vista pidió sin seam inyectado.</summary>
    public static IReadOnlyList<string> ConfirmacionesCapturadas => _confirmacionesCapturadas;

    [AssemblyInitialize]
    public static void Inicializar(TestContext context)
    {
        // Se conserva el estado global previo para restaurarlo siempre en la limpieza.
        _mostrarPrevio = NotificadorPrograma.MostrarPredeterminado;
        _confirmarPrevio = NotificadorPrograma.ConfirmarPredeterminado;
        _contadoresPrevios = NotificadorPrograma.ContadoresFallbackNoModal;

        NotificadorPrograma.MostrarPredeterminado = (mensaje, _) =>
        {
            lock (_mensajesCapturados)
                _mensajesCapturados.Add(mensaje);
        };

        // Sin confirmación modal: una vista que no inyectó Confirmar cancela el flujo,
        // pero el uso queda contabilizado y hace fallar la limpieza.
        NotificadorPrograma.ConfirmarPredeterminado = mensaje =>
        {
            lock (_confirmacionesCapturadas)
                _confirmacionesCapturadas.Add(mensaje);
            return false;
        };
    }

    [AssemblyCleanup]
    public static void VerificarQueNingunMensajeCayoEnLaRutaModal()
    {
        try
        {
            if (_mensajesCapturados.Count == 0 && _confirmacionesCapturadas.Count == 0)
                return;

            var detalle = new List<string>();
            if (_mensajesCapturados.Count > 0)
                detalle.Add($"mensajes: {string.Join(" | ", _mensajesCapturados)}");
            if (_confirmacionesCapturadas.Count > 0)
                detalle.Add($"confirmaciones: {string.Join(" | ", _confirmacionesCapturadas)}");

            throw new AssertFailedException(
                "Vistas que usaron el fallback no modal sin inyectar su NotificadorPrograma "
                + "(habrían abierto un MessageBox modal): " + string.Join(" ;; ", detalle));
        }
        finally
        {
            // Restauración SIEMPRE, incluso si la aserción anterior falla, para no dejar
            // estado global (seams ni contadores) modificado al terminar la suite.
            NotificadorPrograma.MostrarPredeterminado = _mostrarPrevio;
            NotificadorPrograma.ConfirmarPredeterminado = _confirmarPrevio;
            NotificadorPrograma.RestaurarContadoresFallbackNoModal(
                _contadoresPrevios.Mensajes,
                _contadoresPrevios.Confirmaciones);
        }
    }
}
