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
using SOPRO.Application.Models.Reporting.Fsr;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Reporting;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Generación del PDF del factor de salario real (AE-2A). Las filas se modelan
    /// con <see cref="FsrRow"/> y las columnas/formatos provienen del snapshot
    /// neutral <see cref="FsrReportSnapshotBuilder"/> compartido con la ruta Excel.
    /// </summary>
    public partial class FormFSR
    {

        public void GenerarPdfFSR()
        {
            try
            {
                _proyecto.ParametrosFSR = GuardarParametros();
                Recalcular();

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"CalculoFSR_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyecto.Id);
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.FSR, lblTitulo.Text);

                var filas = new List<FsrRow>
                {
                    FsrRow.Seccion("DATOS BÁSICOS"),
                    FsrRow.Subseccion("Para el cálculo de días pagados"),
                    FsrRow.Numero("Días Calendario (DC)", "", "días", nudDiasCalendario.Value),
                    FsrRow.Numero("Días Aguinaldo", "", "días", nudDiasAguinaldo.Value),
                    FsrRow.Numero("Días de vacaciones para calcular prima vacacional", "", "días", nudDiasVacaciones.Value),
                    FsrRow.Numero("Prima vacacional", "", "%", nudPrimaVacacional.Value),
                    FsrRow.Numero("Otros días", "", "días", nudOtrosDiasPagados.Value),
                    FsrRow.Subseccion("Para el cálculo de días no laborados"),
                    FsrRow.Numero("Descansos semanales", "", "días", nudDiasDescanso.Value),
                    FsrRow.Numero("Días festivos", "", "días", nudDiasFestivos.Value),
                    FsrRow.Numero("Días por costumbre", "", "días", nudDiasContrato.Value),
                    FsrRow.Numero("Días sindicales", "", "días", nudDiasSindicato.Value),
                    FsrRow.Numero("Enfermedad no profesional", "", "días", nudDiasEnfermedad.Value),
                    FsrRow.Numero("Mal tiempo", "", "días", nudDiasClima.Value),
                    FsrRow.Numero("Días por costumbre", "", "días", nudDiasArrastre.Value),
                    FsrRow.Numero("Guardias", "", "días", nudDiasGuardia.Value),
                    FsrRow.Numero("Otros días por costumbre", "", "días", nudOtrosDiasNL.Value),
                    FsrRow.Subseccion("Para el calculo de cuotas del IMSS"),
                    FsrRow.Numero("Guarderías", "", "%", nudPctGuarderias.Value),
                    FsrRow.Numero("Retiro", "", "%", nudPctRetiro.Value),
                    FsrRow.Numero("Riesgos de trabajo", "", "%", nudPctRiesgos.Value),
                    FsrRow.Numero("Impuesto INFONAVIT", "", "%", nudPctINFONAVIT.Value),
                    FsrRow.Numero("Impuesto Nómina", "", "%", nudPctNomina.Value),
                    FsrRow.Numero("Otros impuestos", "", "%", nudOtrosImpuestos.Value),

                    FsrRow.Seccion("CÁLCULO"),
                    FsrRow.Subseccion("De datos básicos a utilizar"),
                    FsrRow.Numero("Salario Mínimo General (D.F.)", "", "", FSR_SAMI),
                    FsrRow.Numero("Salario Nominal por jornada (SND)", "", "", FSR_SACAL),

                    FsrRow.Subseccion("De días realmente pagados y SBC"),
                    FsrRow.Numero("Vacaciones", "", "días", FSR_DVAC),
                    FsrRow.Numero("Prima vacacional", "", "días", FSR_DPPVA),
                    FsrRow.Numero("Prima Dominical", "", "días", FSR_DPPDO),
                    FsrRow.Numero("Días equivalentes por horas extras al año", "", "días", FSR_DPHEX),
                    FsrRow.Numero("SUMA de días pagados", "", "días", FSR_DPA),
                    FsrRow.Numero("SUMA de días no laborados", "", "días", FSR_DNLA),
                    FsrRow.Numero("Días realmente laborados  (TL = DC - DNLA)", $"{nudDiasCalendario.Value:N6} días - {FSR_DNLA:N6} días", "días", FSR_DLA),
                    FsrRow.Numero("TP/TL", "", "", FSR_FSI),
                    FsrRow.Numero("(FSBC = DPA/DPCAL)", $"{FSR_DPA:N6} días / {nudDiasCalendario.Value:N6} días", "", FSR_FSBC),
                    FsrRow.Numero("Salario Base de Cotización (SB = FSBC * SN)", $"{FSR_SACAL:N6} * {FSR_FSBC:N6}", "", FSR_SABC),

                    FsrRow.Subseccion("De cuotas del IMSS"),
                    FsrRow.Numero("Porcentaje sobre salario mínimo para cuota fija", "", "%", AA),
                    FsrRow.Numero("Porcentaje para Excedente a 3 SMGDF", "", "%", AB),
                    FsrRow.Numero("Excedente de 3 SMGDF", "", "", AU),
                    FsrRow.Numero("Prestaciones en dinero (Patron+obrero)", $".7+IIF({FSR_SACAL:N6}>1.000000,0,0.25)", "%", FSR_IMPE_p),
                    FsrRow.Numero("Gastos medicos. Pensionados (Patrón-Obrero)", $"1.05+IIF({FSR_SACAL:N6}>1.000000,0,0.375)", "%", FSR_IMGM_p),
                    FsrRow.Numero("Invalidez y vida", $"1.75+IIF({FSR_SACAL:N6}>1.000000,0,0.625)", "%", FSR_IMINV_p),
                    FsrRow.Numero("Cesantía en edad avanzada y vejez", $"3.15+IIF({FSR_SACAL:N6}>1.000000,0,1.125)", "%", FSR_IMCE_p),
                    FsrRow.Numero("Límite de prest. Inv., vida, cesantía y vejez", "", "", AS_lim),
                    FsrRow.Numero("Enfermedad y maternidad. Cuota fija especie", "", "", AC),
                    FsrRow.Numero("Enferm.-matern. Exc. a 3 S.M.D.F. especie", "", "", AD),
                    FsrRow.Numero("Enfermedad y maternidad. Prestaciones en dinero", "", "", AE),
                    FsrRow.Numero("Enfermedad y maternidad gastos médicos pensionados", "", "", AF),
                    FsrRow.Numero("Invalidez y vida", "", "", AG),
                    FsrRow.Numero("Guarderías", "", "", AH),
                    FsrRow.Numero("Retiro", "", "", AI),
                    FsrRow.Numero("Cesantía en edad avanzada y vejez", "", "", AJ),
                    FsrRow.Numero("Riesgos de trabajo", "", "", AK),
                    FsrRow.Numero("Cuota patronal del IMSS", "", "", AL),
                    FsrRow.Numero("Factor de cuota patronal del IMSS = IMSS/SND", $"{AL:N6}/{FSR_SACAL:N6}", "factor", FSR_IMIMS),

                    FsrRow.Subseccion("De INFONAVIT y otras cuotas"),
                    FsrRow.Texto("Limite de Aportaciones INFONAVIT", "", "", $"{AZ:N0}"),
                    FsrRow.Numero("INFONAVIT", "", "", AM),
                    FsrRow.Numero("Impuesto sobre Nómina", "", "", AN),
                    FsrRow.Numero("Otros impuestos", "", "", AO),
                    FsrRow.Numero("Obligaciones patronales (IOP)", "", "", AP),
                    FsrRow.Numero("Obligaciones patronales entre SN", $"{AP:N6}/{FSR_SACAL:N6}", "", AQ),

                    FsrRow.Subseccion("Del TP/TL y del FSR"),
                    FsrRow.Numero("FSR = Ps (Tp/Tl) + Tp/Tl", $"{BH:N6}+{FSR_FSI:N6}", "", FSR_FSR),
                };

                // Snapshot neutral compartido con Excel: misma lista/orden/ancho/
                // formato/estilo, con los decimales configurados en el proyecto.
                var snapshot = new FsrReportSnapshotBuilder().BuildAE2A(_proyecto, lblTitulo.Text);

                Cursor = Cursors.WaitCursor;
                var generador = new GeneradorPdfFSR(svcRep);
                string ruta = generador.Generar(_proyecto, plantilla, snapshot, filas, FSR_FSR, dlg.FileName, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte PDF generado.¿Desea abrirlo?", "Reporte generado",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error al generar el reporte PDF:{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
