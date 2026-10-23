using System;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Application.Models.Presupuesto;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Sincronización de presupuesto con la ruta de comando de cantidad de
    /// componente de matriz (ADR-0001): el panel confirma con su comando único
    /// sobre un contexto nuevo y notifica aquí la instantánea; el formulario
    /// aplica las hojas y los agrupadores por Id con
    /// <see cref="AplicarFilasConfirmadas"/> y marca las entidades compartidas
    /// afectadas como Unchanged. Nunca dispara
    /// <see cref="RefreshBudgetPrices"/> desde esta ruta (la escritura ya la hizo
    /// el comando); <see cref="PanelMatricesEmbebido.MatrizActualizada"/> sigue
    /// reservada a las rutas heredadas.
    /// </summary>
    public partial class FormPresupuesto
    {
        /// <summary>Instancia del panel embebido para las pruebas del ensamblado.</summary>
        internal Controls.PanelMatricesEmbebido? PanelMatricesInterno => _panelMatricesEmbebido;

        /// <summary>Suscribe la ruta de comando del panel a la sincronización por Id.</summary>
        private void ConectarComandoCantidadComponente()
        {
            if (_panelMatricesEmbebido == null)
                return;

            _panelMatricesEmbebido.CantidadComponenteConfirmada += (sender, snapshot) =>
            {
                if (IsDisposed || _cargando)
                    return;
                if (snapshot is null || snapshot.ProjectId != _proyecto.Id)
                    return;

                try
                {
                    _context.ChangeTracker.DetectChanges();
                    if (_context.ChangeTracker.HasChanges())
                        return;

                    // El panel ya sincronizó el componente visible; el formulario
                    // aplica las hojas confirmadas y todos los agrupadores por Id.
                    SincronizarProyectoCompartido(snapshot.Project);
                    AplicarFilasConfirmadas(snapshot.Leaves.Concat(snapshot.Aggregators).ToArray());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Sincronización de cantidad de componente: {ex.Message}");
                }
            };
        }
    }
}
