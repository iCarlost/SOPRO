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
    /// Generación del PDF del factor de salario real.
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

                var filas = new List<GeneradorPdfFSR.FsrPdfRow>
                {
                    GeneradorPdfFSR.FsrPdfRow.Seccion("DATOS BÁSICOS"),
                    GeneradorPdfFSR.FsrPdfRow.Subseccion("Para el cálculo de días pagados"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días Calendario (DC)", "", "días", nudDiasCalendario.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días Aguinaldo", "", "días", nudDiasAguinaldo.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días de vacaciones para calcular prima vacacional", "", "días", nudDiasVacaciones.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Prima vacacional", "", "%", nudPrimaVacacional.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Otros días", "", "días", nudOtrosDiasPagados.Value),
                    GeneradorPdfFSR.FsrPdfRow.Subseccion("Para el cálculo de días no laborados"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Descansos semanales", "", "días", nudDiasDescanso.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días festivos", "", "días", nudDiasFestivos.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días por costumbre", "", "días", nudDiasContrato.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días sindicales", "", "días", nudDiasSindicato.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Enfermedad no profesional", "", "días", nudDiasEnfermedad.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Mal tiempo", "", "días", nudDiasClima.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días por costumbre", "", "días", nudDiasArrastre.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Guardias", "", "días", nudDiasGuardia.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Otros días por costumbre", "", "días", nudOtrosDiasNL.Value),
                    GeneradorPdfFSR.FsrPdfRow.Subseccion("Para el calculo de cuotas del IMSS"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Guarderías", "", "%", nudPctGuarderias.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Retiro", "", "%", nudPctRetiro.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Riesgos de trabajo", "", "%", nudPctRiesgos.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Impuesto INFONAVIT", "", "%", nudPctINFONAVIT.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Impuesto Nómina", "", "%", nudPctNomina.Value),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Otros impuestos", "", "%", nudOtrosImpuestos.Value),

                    GeneradorPdfFSR.FsrPdfRow.Seccion("CÁLCULO"),
                    GeneradorPdfFSR.FsrPdfRow.Subseccion("De datos básicos a utilizar"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Salario Mínimo General (D.F.)", "", "", $"{FSR_SAMI:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Salario Nominal por jornada (SND)", "", "", $"{FSR_SACAL:N5}"),

                    GeneradorPdfFSR.FsrPdfRow.Subseccion("De días realmente pagados y SBC"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Vacaciones", "", "días", FSR_DVAC),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Prima vacacional", "", "días", FSR_DPPVA),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Prima Dominical", "", "días", FSR_DPPDO),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días equivalentes por horas extras al año", "", "días", FSR_DPHEX),
                    GeneradorPdfFSR.FsrPdfRow.Numero("SUMA de días pagados", "", "días", FSR_DPA),
                    GeneradorPdfFSR.FsrPdfRow.Numero("SUMA de días no laborados", "", "días", FSR_DNLA),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Días realmente laborados  (TL = DC - DNLA)", $"{nudDiasCalendario.Value:N6} días - {FSR_DNLA:N6} días", "días", FSR_DLA),
                    GeneradorPdfFSR.FsrPdfRow.Texto("TP/TL", "", "", $"{FSR_FSI:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("(FSBC = DPA/DPCAL)", $"{FSR_DPA:N6} días / {nudDiasCalendario.Value:N6} días", "", FSR_FSBC),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Salario Base de Cotización (SB = FSBC * SN)", $"{FSR_SACAL:N6} * {FSR_FSBC:N6}", "", FSR_SABC),

                    GeneradorPdfFSR.FsrPdfRow.Subseccion("De cuotas del IMSS"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Porcentaje sobre salario mínimo para cuota fija", "", "%", AA),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Porcentaje para Excedente a 3 SMGDF", "", "%", AB),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Excedente de 3 SMGDF", "", "", $"{AU:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Prestaciones en dinero (Patron+obrero)", $".7+IIF({FSR_SACAL:N6}>1.000000,0,0.25)", "%", $"{FSR_IMPE_p:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Gastos medicos. Pensionados (Patrón-Obrero)", $"1.05+IIF({FSR_SACAL:N6}>1.000000,0,0.375)", "%", $"{FSR_IMGM_p:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Invalidez y vida", $"1.75+IIF({FSR_SACAL:N6}>1.000000,0,0.625)", "%", $"{FSR_IMINV_p:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Cesantía en edad avanzada y vejez", $"3.15+IIF({FSR_SACAL:N6}>1.000000,0,1.125)", "%", $"{FSR_IMCE_p:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Límite de prest. Inv., vida, cesantía y vejez", "", "", $"{AS_lim:N5}"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Enfermedad y maternidad. Cuota fija especie", "", "", AC),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Enferm.-matern. Exc. a 3 S.M.D.F. especie", "", "", AD),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Enfermedad y maternidad. Prestaciones en dinero", "", "", AE),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Enfermedad y maternidad gastos médicos pensionados", "", "", AF),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Invalidez y vida", "", "", AG),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Guarderías", "", "", AH),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Retiro", "", "", AI),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Cesantía en edad avanzada y vejez", "", "", AJ),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Riesgos de trabajo", "", "", AK),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Cuota patronal del IMSS", "", "", AL),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Factor de cuota patronal del IMSS = IMSS/SND", $"{AL:N6}/{FSR_SACAL:N6}", "factor", FSR_IMIMS),

                    GeneradorPdfFSR.FsrPdfRow.Subseccion("De INFONAVIT y otras cuotas"),
                    GeneradorPdfFSR.FsrPdfRow.Texto("Limite de Aportaciones INFONAVIT", "", "", $"{AZ:N0}"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("INFONAVIT", "", "", AM),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Impuesto sobre Nómina", "", "", AN),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Otros impuestos", "", "", AO),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Obligaciones patronales (IOP)", "", "", AP),
                    GeneradorPdfFSR.FsrPdfRow.Numero("Obligaciones patronales entre SN", $"{AP:N6}/{FSR_SACAL:N6}", "", AQ),

                    GeneradorPdfFSR.FsrPdfRow.Subseccion("Del TP/TL y del FSR"),
                    GeneradorPdfFSR.FsrPdfRow.Numero("FSR = Ps (Tp/Tl) + Tp/Tl", $"{BH:N6}+{FSR_FSI:N6}", "", FSR_FSR),
                };

                Cursor = Cursors.WaitCursor;
                var generador = new GeneradorPdfFSR(svcRep);
                string ruta = generador.Generar(_proyecto, plantilla, filas, FSR_FSR, dlg.FileName, tituloCfg);
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
