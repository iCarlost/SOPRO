using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Repositories;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Helpers;
using System.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Actualización de la interfaz con los resultados del FSR.
    /// </summary>
    public partial class FormFSR
    {

        private void ActualizarUI()
        {
            // Sección días
            lblDPAVal.Text   = $"{FSR_DPA:N5} días";
            lblDNLAVal.Text  = $"{FSR_DNLA:N5} días";
            lblDLAVal.Text   = $"{FSR_DLA:N5} días";
            lblFSIVal.Text   = $"{FSR_FSI:N5}";
            lblFSBCVal.Text  = $"{FSR_FSBC:N5}";
            lblSACBVal.Text  = $"{FSR_SABC:N5}";
            lblSACALVal.Text = $"{FSR_SACAL:N5}";

            // Sección IMSS
            lblACVal.Text    = $"{AC:N5}";
            lblADVal.Text    = $"{AD:N5}";
            lblAEVal.Text    = $"{AE:N5}";
            lblAFVal.Text    = $"{AF:N5}";
            lblAGVal.Text    = $"{AG:N5}";
            lblAHVal.Text    = $"{AH:N5}";
            lblAIVal.Text    = $"{AI:N5}";
            lblAJVal.Text    = $"{AJ:N5}";
            lblAKVal.Text    = $"{AK:N5}";
            lblALVal.Text    = $"{AL:N5}";
            lblIMIMSVal.Text = $"{FSR_IMIMS:N5}";

            // INFONAVIT y otros
            lblAMVal.Text    = $"{AM:N5}";
            lblANVal.Text    = $"{AN:N5}";
            lblAOVal.Text    = $"{AO:N5}";
            lblAPVal.Text    = $"{AP:N5}";
            lblAQVal.Text    = $"{AQ:N5}";

            // FSR Final
            lblBHVal.Text   = $"{BH:N5}";
            lblFSRVal.Text  = $"{FSR_FSR:N5}";

            // Resultado grande en panel bottom
            lblResultadoFSR.Text = $"{FSR_FSR:N5}";

            // Color advertencia si días no laborados > días calendario
            lblDNLAVal.ForeColor = FSR_DNLA >= nudDiasCalendario.Value
                ? System.Drawing.Color.Red
                : System.Drawing.Color.FromArgb(33, 33, 33);
        }
    }
}
