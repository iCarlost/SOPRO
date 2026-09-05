using SOPRO.Core.Entities;
using SOPRO.Core.Services;
using SOPRO.Data.Context;
using SOPRO.Data.Repositories;
using System;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormCalculoCostoHorario : Form
    {
        private readonly SOPROContext _context;
        private readonly Repository<Maquinaria> _repository;
        private readonly Maquinaria _maquinaria;

        // Resultados intermedios y finales
        private decimal _vm = 0, _vr = 0, _vmvrMedio = 0;
        private decimal _depreciacion = 0, _inversion = 0, _seguros = 0, _mantenimiento = 0;
        private decimal _totalCargosFijos = 0;
        private decimal _combustible = 0, _lubricantes = 0, _llantas = 0, _piezasEspeciales = 0;
        private decimal _totalConsumos = 0;
        private decimal _salarioReal = 0, _operacion = 0;
        private decimal _costoHorarioTotal = 0;

        public FormCalculoCostoHorario(SOPROContext context, Maquinaria maquinaria)
        {
            InitializeComponent();

            _context    = context    ?? throw new ArgumentNullException(nameof(context));
            _repository = new Repository<Maquinaria>(_context);
            _maquinaria = maquinaria ?? throw new ArgumentNullException(nameof(maquinaria));

            CargarDatos();
            SuscribirEventos();
        }

        // ──────────────────────────────────────────────────────────────────────
        // CARGA INICIAL
        // ──────────────────────────────────────────────────────────────────────
        private void CargarDatos()
        {
            // Info general
            lblClaveVal.Text    = _maquinaria.Clave;
            lblDescVal.Text     = _maquinaria.Descripcion;
            lblPotenciaVal.Text = $"{_maquinaria.PotenciaNominal:N2} HP";
            lblCombVal.Text     = _maquinaria.TipoCombustible.ToString();

            // Tab A — Cargos Fijos
            nudValorAdquisicion.Value = _maquinaria.ValorAdquisicion;
            nudValorLlantas.Value     = _maquinaria.ValorLlantas;
            nudValorPiezasEsp.Value   = _maquinaria.ValorPiezasEspeciales;
            nudFactorRescate.Value    = _maquinaria.FactorRescate;
            nudVidaEconomica.Value    = _maquinaria.VidaEconomica > 0 ? _maquinaria.VidaEconomica : 10000;
            nudTasaInteres.Value      = _maquinaria.TasaInteres;
            nudHorasAnio.Value        = _maquinaria.HorasEfectivasAnio;
            nudPrimaSeguro.Value      = _maquinaria.PrimaSeguro;
            nudFactorManten.Value     = _maquinaria.FactorMantenimiento;

            // Tab B — Consumos
            nudCantCombustible.Value  = _maquinaria.CantidadCombustible;
            nudPrecioCombustible.Value= _maquinaria.PrecioCombustible;
            nudCantAceite.Value       = _maquinaria.CantidadAceite;
            nudPrecioAceite.Value     = _maquinaria.PrecioAceite;
            nudNumLlantas.Value       = _maquinaria.NumeroLlantas;
            nudVidaLlantas.Value      = _maquinaria.VidaEconomicaLlantas > 0 ? _maquinaria.VidaEconomicaLlantas : 5000;
            nudVidaPiezasEsp.Value    = _maquinaria.VidaPiezasEspeciales > 0 ? _maquinaria.VidaPiezasEspeciales : 5000;

            // Tab C — Operación
            nudSalarioOperador.Value  = _maquinaria.SalarioOperador;
            nudFSR.Value              = _maquinaria.FactorSalarioReal;
            nudHorasTurno.Value       = _maquinaria.HorasEfectivasTurno;

            // Calcular con los datos cargados
            Recalcular();
        }

        private void SuscribirEventos()
        {
            // Tab A
            nudValorAdquisicion.ValueChanged += (s, e) => Recalcular();
            nudValorLlantas.ValueChanged     += (s, e) => Recalcular();
            nudValorPiezasEsp.ValueChanged   += (s, e) => Recalcular();
            nudFactorRescate.ValueChanged    += (s, e) => Recalcular();
            nudVidaEconomica.ValueChanged    += (s, e) => Recalcular();
            nudTasaInteres.ValueChanged      += (s, e) => Recalcular();
            nudHorasAnio.ValueChanged        += (s, e) => Recalcular();
            nudPrimaSeguro.ValueChanged      += (s, e) => Recalcular();
            nudFactorManten.ValueChanged     += (s, e) => Recalcular();

            // Tab B
            nudCantCombustible.ValueChanged  += (s, e) => Recalcular();
            nudPrecioCombustible.ValueChanged += (s, e) => Recalcular();
            nudCantAceite.ValueChanged       += (s, e) => Recalcular();
            nudPrecioAceite.ValueChanged     += (s, e) => Recalcular();
            nudNumLlantas.ValueChanged       += (s, e) => Recalcular();
            nudVidaLlantas.ValueChanged      += (s, e) => Recalcular();
            nudVidaPiezasEsp.ValueChanged    += (s, e) => Recalcular();

            // Tab C
            nudSalarioOperador.ValueChanged  += (s, e) => Recalcular();
            nudFSR.ValueChanged              += (s, e) => Recalcular();
            nudHorasTurno.ValueChanged       += (s, e) => Recalcular();
        }

        // ──────────────────────────────────────────────────────────────────────
        // CÁLCULOS — RLOPSRM Arts. 194–210
        // ──────────────────────────────────────────────────────────────────────
        private void Recalcular()
        {
            var result = HourlyCostPresentationMapper.FromBreakdown(
                MaquinariaHourlyCostAdapter.Calculate(new Maquinaria
            {
                ValorAdquisicion = nudValorAdquisicion.Value,
                ValorLlantas = nudValorLlantas.Value,
                ValorPiezasEspeciales = nudValorPiezasEsp.Value,
                FactorRescate = nudFactorRescate.Value,
                VidaEconomica = nudVidaEconomica.Value,
                TasaInteres = nudTasaInteres.Value,
                HorasEfectivasAnio = nudHorasAnio.Value,
                PrimaSeguro = nudPrimaSeguro.Value,
                FactorMantenimiento = nudFactorManten.Value,
                CantidadCombustible = nudCantCombustible.Value,
                PrecioCombustible = nudPrecioCombustible.Value,
                CantidadAceite = nudCantAceite.Value,
                PrecioAceite = nudPrecioAceite.Value,
                NumeroLlantas = (int)nudNumLlantas.Value,
                VidaEconomicaLlantas = nudVidaLlantas.Value,
                VidaPiezasEspeciales = nudVidaPiezasEsp.Value,
                SalarioOperador = nudSalarioOperador.Value,
                FactorSalarioReal = nudFSR.Value,
                HorasEfectivasTurno = nudHorasTurno.Value
            }));

            _vm = result.NetValue;
            _vr = result.SalvageValue;
            _vmvrMedio = result.AverageValue;
            _depreciacion = result.Depreciation;
            _inversion = result.Investment;
            _seguros = result.Insurance;
            _mantenimiento = result.Maintenance;
            _totalCargosFijos = result.FixedChargesTotal;
            _combustible = result.Fuel;
            _lubricantes = result.Lubricants;
            _llantas = result.Tires;
            _piezasEspeciales = result.SpecialParts;
            _totalConsumos = result.ConsumptionTotal;
            _salarioReal = result.RealSalary;
            _operacion = result.Operation;
            _costoHorarioTotal = result.HourlyCost;
            ActualizarUI();
        }

        // ──────────────────────────────────────────────────────────────────────
        // ACTUALIZAR UI
        // ──────────────────────────────────────────────────────────────────────
        private void ActualizarUI()
        {
            // Intermedios Tab A
            lblVmVal.Text       = $"${_vm:N2}";
            lblVrVal.Text       = $"${_vr:N2}";
            lblVmVrMedioVal.Text= $"${_vmvrMedio:N2}";

            // Resultados Tab A
            lblDepreciacion.Text    = $"${_depreciacion:N4}";
            lblInversion.Text       = $"${_inversion:N4}";
            lblSeguros.Text         = $"${_seguros:N4}";
            lblMantenimiento.Text   = $"${_mantenimiento:N4}";
            lblTotalCargosFijos.Text= $"${_totalCargosFijos:N4} / hr";

            // Resultados Tab B
            lblCombustible.Text     = $"${_combustible:N4}";
            lblLubricantes.Text     = $"${_lubricantes:N4}";
            lblLlantas.Text         = $"${_llantas:N4}";
            lblPiezasEsp.Text       = $"${_piezasEspeciales:N4}";
            lblTotalConsumos.Text   = $"${_totalConsumos:N4} / hr";

            // Intermedios + resultado Tab C
            lblSalarioReal.Text     = $"${_salarioReal:N2}";
            lblOperacion.Text       = $"${_operacion:N4}";

            // Total en el panel bottom
            lblCostoTotal.Text      = $"${_costoHorarioTotal:N2} / hr";

            // ── Resumen ──────────────────────────────────────────────────────
            lblResADepVal.Text  = $"${_depreciacion:N4}";
            lblResAInvVal.Text  = $"${_inversion:N4}";
            lblResASegVal.Text  = $"${_seguros:N4}";
            lblResAMntVal.Text  = $"${_mantenimiento:N4}";
            lblResATotVal.Text  = $"${_totalCargosFijos:N4}";

            lblResBComVal.Text  = $"${_combustible:N4}";
            lblResBLubVal.Text  = $"${_lubricantes:N4}";
            lblResBLlaVal.Text  = $"${_llantas:N4}";
            lblResBPieVal.Text  = $"${_piezasEspeciales:N4}";
            lblResBTotVal.Text  = $"${_totalConsumos:N4}";

            lblResCOpeVal.Text  = $"${_operacion:N4}";
            lblResTotalVal.Text = $"${_costoHorarioTotal:N2} / hr";
        }

        // ──────────────────────────────────────────────────────────────────────
        // BOTÓN — Estimar combustible por potencia nominal
        // ──────────────────────────────────────────────────────────────────────
        private void btnEstimarCombustible_Click(object sender, EventArgs e)
        {
            if (_maquinaria.PotenciaNominal <= 0)
            {
                MessageBox.Show(
                    "La potencia nominal del equipo es 0 HP.\n\n" +
                    "Para estimar el consumo de combustible, ingresa la potencia\n" +
                    "nominal en el catálogo de maquinaria antes de calcular.",
                    "Potencia no definida",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Factores empíricos estándar RLOPSRM / Caterpillar Performance Handbook:
            //   Diesel:   ~0.15 lts / HP / hr  (factor eficiencia 0.60–0.65)
            //   Gasolina: ~0.23 lts / HP / hr  (motores de ciclo Otto, menor eficiencia)
            //   SinMotor: no aplica
            decimal factor = _maquinaria.TipoCombustible switch
            {
                TipoCombustible.Diesel   => 0.15m,
                TipoCombustible.Gasolina => 0.23m,
                _                        => 0m
            };

            if (factor == 0)
            {
                MessageBox.Show(
                    "El equipo está marcado como 'Sin Motor'.\nNo aplica estimación de combustible.",
                    "Sin Motor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            decimal estimado = _maquinaria.PotenciaNominal * factor;

            var msg = $"Estimación por potencia nominal:\n\n" +
                      $"  Potencia:  {_maquinaria.PotenciaNominal:N2} HP\n" +
                      $"  Factor:    {factor} lts/HP/hr  ({_maquinaria.TipoCombustible})\n" +
                      $"  Gh estim.: {estimado:N4} lts/hr\n\n" +
                      $"¿Desea usar este valor como Gh?";

            if (MessageBox.Show(msg, "Estimar Combustible por HP",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                nudCantCombustible.Value = Math.Min(estimado, nudCantCombustible.Maximum);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // BOTÓN — Restaurar valores por defecto
        // ──────────────────────────────────────────────────────────────────────
        private void btnRestaurarDefecto_Click(object sender, EventArgs e)
        {
            var r = MessageBox.Show(
                "¿Restaurar los valores por defecto?\n\n" +
                "  • Factor de Rescate:        10 %\n" +
                "  • Vida Económica:           10,000 hrs\n" +
                "  • Tasa de Interés:          21.24 %\n" +
                "  • Horas Efectivas/Año:      1,600\n" +
                "  • Prima de Seguro:          3.00 %\n" +
                "  • Factor Mantenimiento Ko:  0.20\n" +
                "  • FSR:                      1.6543\n" +
                "  • Horas Efectivas/Turno:    8",
                "Restaurar Defectos",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (r != DialogResult.Yes) return;

            nudFactorRescate.Value   = 0.10m;
            nudVidaEconomica.Value   = 10000;
            nudTasaInteres.Value     = 21.24m;
            nudHorasAnio.Value       = 1600;
            nudPrimaSeguro.Value     = 3.00m;
            nudFactorManten.Value    = 0.20m;
            nudFSR.Value             = 1.6543m;
            nudHorasTurno.Value      = 8;
            // No se tocan valores de adquisición/combustible/salario porque son datos reales
        }

        // ──────────────────────────────────────────────────────────────────────
        // BOTÓN — Guardar
        // ──────────────────────────────────────────────────────────────────────
        private async void btnGuardar_Click(object sender, EventArgs e)
        {
            try
            {
                // Persistir todos los parámetros de cálculo en la entidad
                _maquinaria.ValorAdquisicion       = nudValorAdquisicion.Value;
                _maquinaria.ValorLlantas           = nudValorLlantas.Value;
                _maquinaria.ValorPiezasEspeciales  = nudValorPiezasEsp.Value;
                _maquinaria.FactorRescate          = nudFactorRescate.Value;
                _maquinaria.VidaEconomica          = nudVidaEconomica.Value;
                _maquinaria.TasaInteres            = nudTasaInteres.Value;
                _maquinaria.HorasEfectivasAnio     = nudHorasAnio.Value;
                _maquinaria.PrimaSeguro            = nudPrimaSeguro.Value;
                _maquinaria.FactorMantenimiento    = nudFactorManten.Value;

                _maquinaria.CantidadCombustible    = nudCantCombustible.Value;
                _maquinaria.PrecioCombustible      = nudPrecioCombustible.Value;
                _maquinaria.CantidadAceite         = nudCantAceite.Value;
                _maquinaria.PrecioAceite           = nudPrecioAceite.Value;
                _maquinaria.NumeroLlantas          = (int)nudNumLlantas.Value;
                _maquinaria.VidaEconomicaLlantas   = nudVidaLlantas.Value;
                _maquinaria.VidaPiezasEspeciales   = nudVidaPiezasEsp.Value;

                _maquinaria.SalarioOperador        = nudSalarioOperador.Value;
                _maquinaria.FactorSalarioReal      = nudFSR.Value;
                _maquinaria.HorasEfectivasTurno    = nudHorasTurno.Value;

                _maquinaria.CostoHorario           = _costoHorarioTotal;
                _maquinaria.EsCostoCalculado       = true;
                _maquinaria.FechaCalculoCosto      = DateTime.Now;
                _maquinaria.FechaModificacion      = DateTime.Now;

                await _repository.UpdateAsync(_maquinaria);
                await _repository.SaveChangesAsync();

                MessageBox.Show(
                    $"Costo horario calculado y guardado:\n\n" +
                    $"  A — Cargos Fijos:   ${_totalCargosFijos:N4} / hr\n" +
                    $"  B — Consumos:       ${_totalConsumos:N4} / hr\n" +
                    $"  C — Operación:      ${_operacion:N4} / hr\n" +
                    $"  {'─',35}\n" +
                    $"  Phm TOTAL:          ${_costoHorarioTotal:N2} / hr",
                    "Guardado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // BOTÓN — Cancelar
        // ──────────────────────────────────────────────────────────────────────
        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
