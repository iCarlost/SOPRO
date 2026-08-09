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
    /// Formato de columnas y generación de reportes del FSR.
    /// </summary>
    public partial class FormFSR
    {

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            // FSR no expone grid formateable en ribbon.
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            // FSR no expone grid formateable en ribbon.
        }

        public bool GenerarReporteExcel() => GenerarReporteExcelRibbon();
        private readonly SOPROContext _context;
        private readonly Proyecto _proyecto;

        // ── Variables intermedias de cálculo ──────────────────────────────────
        // Básicos
        private decimal BD, BE, BF, BG;
        private decimal FSR_SAMI, FSR_SACAL;
        // Días
        private decimal FSR_DVAC, FSR_DPPVA, FSR_DPPDO, FSR_DPHEX;
        private decimal FSR_DPA, FSR_DNLA, FSR_DLA;
        private decimal FSR_FSI, FSR_FSBC, FSR_SABC;
        // IMSS cuotas
        private decimal AA, AB, AU;
        private decimal FSR_IMPE_p, FSR_IMGM_p, FSR_IMINV_p, FSR_IMCE_p;
        private decimal BA, AS_lim, AY;
        private decimal AC, AD, AE, AF, AG, AH, AI, AJ, AK, AL;
        private decimal FSR_IMIMS;
        // INFONAVIT y otros
        private decimal AZ, AM, AN, AO, AP, AQ;
        // FSR final
        private decimal BH, FSR_FSR;

        public FormFSR(SOPROContext context, Proyecto proyecto)
        {
            InitializeComponent();
            _context  = context  ?? throw new ArgumentNullException(nameof(context));
            _proyecto = proyecto ?? throw new ArgumentNullException(nameof(proyecto));

            CargarValoresPorDefecto();
            RestaurarParametros();
            SuscribirEventos();
            Recalcular();
            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyecto.Id, ReportTitleModuleKeys.FSR).Attach();

        }
    }
}
