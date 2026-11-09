using SOPRO.Core.Entities;
using SOPRO.Core.Services;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using Sopro.Calculation.Equipment;
using System;
using System.Windows.Forms;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Forms
{
    public partial class FormCalculoCostoHorario : Form
    {
        private readonly SOPROContext _context;
        private readonly Maquinaria _maquinaria;

        // Resultados intermedios y finales
        private decimal _vm = 0, _vr = 0, _vmvrMedio = 0;
        private decimal _depreciacion = 0, _inversion = 0, _seguros = 0, _mantenimiento = 0;
        private decimal _totalCargosFijos = 0;
        private decimal _combustible = 0, _lubricantes = 0, _llantas = 0, _piezasEspeciales = 0;
        private decimal _totalConsumos = 0;
        private decimal _salarioReal = 0, _operacion = 0;
        private decimal _costoHorarioTotal = 0;
        private readonly Button btnVincularCombustible = new() { Text = "Vincular…", Size = new System.Drawing.Size(76, 22) };
        private readonly Button btnQuitarCombustible = new() { Text = "Quitar", Size = new System.Drawing.Size(64, 22) };
        private readonly Label lblVinculoCombustible = new() { Text = "Sin vínculo", AutoSize = true, ForeColor = System.Drawing.Color.DarkSlateGray };
        private readonly Button btnVincularAceite = new() { Text = "Vincular…", Size = new System.Drawing.Size(76, 22) };
        private readonly Button btnQuitarAceite = new() { Text = "Quitar", Size = new System.Drawing.Size(64, 22) };
        private readonly Label lblVinculoAceite = new() { Text = "Sin vínculo", AutoSize = true, ForeColor = System.Drawing.Color.DarkSlateGray };
        private readonly Button btnVincularOperador = new() { Text = "Vincular…", Size = new System.Drawing.Size(76, 22) };
        private readonly Button btnQuitarOperador = new() { Text = "Quitar", Size = new System.Drawing.Size(64, 22) };
        private readonly Label lblVinculoOperador = new() { Text = "Sin vínculo", AutoSize = true, ForeColor = System.Drawing.Color.DarkSlateGray };
        private readonly ToolTip _vinculoToolTip = new();
        private int? _materialCombustibleIdPendiente;
        private int? _materialAceiteIdPendiente;
        private int? _operadorIdPendiente;

        public FormCalculoCostoHorario(SOPROContext context, Maquinaria maquinaria)
        {
            InitializeComponent();

            _context    = context    ?? throw new ArgumentNullException(nameof(context));
            _maquinaria = maquinaria ?? throw new ArgumentNullException(nameof(maquinaria));

            ConfigurarSeleccionTotal();
            CargarDatos();
            SuscribirEventos();
            ConfigurarVinculos();
        }

        private void ConfigurarVinculos()
        {
            btnVincularCombustible.Location = new System.Drawing.Point(225, 91);
            btnQuitarCombustible.Location = new System.Drawing.Point(305, 91);
            lblVinculoCombustible.Location = new System.Drawing.Point(10, 94);
            btnVincularAceite.Location = new System.Drawing.Point(225, 89);
            btnQuitarAceite.Location = new System.Drawing.Point(305, 89);
            lblVinculoAceite.Location = new System.Drawing.Point(10, 92);
            btnVincularOperador.Location = new System.Drawing.Point(248, 150);
            btnQuitarOperador.Location = new System.Drawing.Point(328, 150);
            lblVinculoOperador.Location = new System.Drawing.Point(10, 153);
            grpCombustible.Controls.AddRange(new Control[] { btnVincularCombustible, btnQuitarCombustible, lblVinculoCombustible });
            grpLubricantes.Controls.AddRange(new Control[] { btnVincularAceite, btnQuitarAceite, lblVinculoAceite });
            grpOperacion.Controls.AddRange(new Control[] { btnVincularOperador, btnQuitarOperador, lblVinculoOperador });
            btnVincularCombustible.Click += btnVincularCombustible_Click;
            btnVincularAceite.Click += btnVincularAceite_Click;
            btnVincularOperador.Click += btnVincularOperador_Click;
            btnQuitarCombustible.Click += (s, e) => Desvincular(() => _materialCombustibleIdPendiente = null, nudPrecioCombustible, null, lblVinculoCombustible);
            btnQuitarAceite.Click += (s, e) => Desvincular(() => _materialAceiteIdPendiente = null, nudPrecioAceite, null, lblVinculoAceite);
            btnQuitarOperador.Click += (s, e) => Desvincular(() => _operadorIdPendiente = null, nudSalarioOperador, nudFSR, lblVinculoOperador);
            _vinculoToolTip.SetToolTip(lblVinculoCombustible, "El precio se toma del material mientras esté vinculado.");
            _vinculoToolTip.SetToolTip(lblVinculoAceite, "El precio se toma del material mientras esté vinculado.");
            _vinculoToolTip.SetToolTip(lblVinculoOperador, "Salario base y FSR se toman de esta persona mientras esté vinculada.");
            bool linked = _maquinaria.EsCostoCalculado;
            _materialCombustibleIdPendiente = _maquinaria.MaterialCombustibleId;
            _materialAceiteIdPendiente = _maquinaria.MaterialAceiteId;
            _operadorIdPendiente = _maquinaria.ManoDeObraOperadorId;
            btnVincularCombustible.Enabled = linked;
            btnVincularAceite.Enabled = linked;
            btnVincularOperador.Enabled = linked;
            if (!linked)
            {
                btnQuitarCombustible.Enabled = btnQuitarAceite.Enabled = btnQuitarOperador.Enabled = false;
                return;
            }
            if (_materialCombustibleIdPendiente.HasValue)
            {
                Material? item = _context.Materiales.FirstOrDefault(x => x.Id == _materialCombustibleIdPendiente && x.ProyectoId == _maquinaria.ProyectoId);
                if (item != null)
                {
                    nudPrecioCombustible.Value = Clamp(nudPrecioCombustible, item.PrecioUnitario);
                    lblVinculoCombustible.Text = item.Clave;
                    nudPrecioCombustible.Enabled = false;
                }
                else
                {
                    lblVinculoCombustible.Text = "Vínculo no disponible";
                    nudPrecioCombustible.Enabled = false;
                }
            }
            if (_materialAceiteIdPendiente.HasValue)
            {
                Material? item = _context.Materiales.FirstOrDefault(x => x.Id == _materialAceiteIdPendiente && x.ProyectoId == _maquinaria.ProyectoId);
                if (item != null)
                {
                    nudPrecioAceite.Value = Clamp(nudPrecioAceite, item.PrecioUnitario);
                    lblVinculoAceite.Text = item.Clave;
                    nudPrecioAceite.Enabled = false;
                }
                else
                {
                    lblVinculoAceite.Text = "Vínculo no disponible";
                    nudPrecioAceite.Enabled = false;
                }
            }
            if (_operadorIdPendiente.HasValue)
            {
                ManoDeObra? item = _context.ManoDeObra.FirstOrDefault(x => x.Id == _operadorIdPendiente && x.ProyectoId == _maquinaria.ProyectoId);
                if (item != null)
                {
                    nudSalarioOperador.Value = Clamp(nudSalarioOperador, item.SalarioBase);
                    nudFSR.Value = Clamp(nudFSR, item.FactorSalarioReal);
                    lblVinculoOperador.Text = item.Clave;
                    nudSalarioOperador.Enabled = false;
                    nudFSR.Enabled = false;
                }
                else
                {
                    lblVinculoOperador.Text = "Vínculo no disponible";
                    nudSalarioOperador.Enabled = false;
                    nudFSR.Enabled = false;
                }
            }
            btnQuitarCombustible.Enabled = _materialCombustibleIdPendiente.HasValue && linked;
            btnQuitarAceite.Enabled = _materialAceiteIdPendiente.HasValue && linked;
            btnQuitarOperador.Enabled = _operadorIdPendiente.HasValue && linked;
            Recalcular();
        }

        private static decimal Clamp(NumericUpDown control, decimal value) => Math.Max(control.Minimum, Math.Min(control.Maximum, value));

        internal static IQueryable<Material> ConsultarMaterialesDelPropietario(IQueryable<Material> materiales, int? proyectoId)
        {
            return materiales.Where(x => x.ProyectoId == proyectoId).OrderBy(x => x.Clave);
        }

        internal static IQueryable<ManoDeObra> ConsultarOperadoresDelPropietario(IQueryable<ManoDeObra> personal, int? proyectoId)
        {
            return personal.Where(x => x.ProyectoId == proyectoId).OrderBy(x => x.Clave);
        }

        private void SeleccionarVinculo<T>(IQueryable<T> source, Func<T, string> label, Action<T> apply, string titulo) where T : class
        {
            using var dialog = new Form { Text = titulo, Width = 520, Height = 390, StartPosition = FormStartPosition.CenterParent };
            ListBox list = new ListBox { Dock = DockStyle.Fill, DisplayMember = "Text" };
            List<T> rows = source.ToList();
            foreach (T row in rows) list.Items.Add(new VinculoOpcion<T>(label(row), row));
            list.DisplayMember = nameof(VinculoOpcion<T>.Text);
            FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft };
            Button cancel = new Button { Text = "Cancelar", Width = 90, DialogResult = DialogResult.Cancel };
            Button ok = new Button { Text = "Vincular", Width = 90, DialogResult = DialogResult.OK, Enabled = rows.Count > 0 };
            Label empty = new Label { Text = rows.Count == 0 ? "No hay elementos disponibles en este alcance." : string.Empty, Dock = DockStyle.Top, Height = 28, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            footer.Controls.Add(cancel);
            footer.Controls.Add(ok);
            dialog.Controls.Add(list);
            dialog.Controls.Add(empty);
            dialog.Controls.Add(footer);
            dialog.CancelButton = cancel;
            dialog.AcceptButton = ok;
            if (dialog.ShowDialog(this) == DialogResult.OK && list.SelectedItem is VinculoOpcion<T> selected)
                apply(selected.Value);
        }

        private sealed record VinculoOpcion<T>(string Text, T Value);

        private void btnVincularCombustible_Click(object? sender, EventArgs e)
        {
            SeleccionarVinculo(
                ConsultarMaterialesDelPropietario(_context.Materiales, _maquinaria.ProyectoId),
                material => $"{material.Clave} — {material.Descripcion} (${material.PrecioUnitario:N4})",
                material =>
                {
                    _materialCombustibleIdPendiente = material.Id;
                    nudPrecioCombustible.Value = Clamp(nudPrecioCombustible, material.PrecioUnitario);
                    nudPrecioCombustible.Enabled = false;
                    lblVinculoCombustible.Text = material.Clave;
                    btnQuitarCombustible.Enabled = true;
                    Recalcular();
                },
                "Vincular combustible");
        }
        private void btnVincularAceite_Click(object? sender, EventArgs e)
        {
            SeleccionarVinculo(
                ConsultarMaterialesDelPropietario(_context.Materiales, _maquinaria.ProyectoId),
                material => $"{material.Clave} — {material.Descripcion} (${material.PrecioUnitario:N4})",
                material =>
                {
                    _materialAceiteIdPendiente = material.Id;
                    nudPrecioAceite.Value = Clamp(nudPrecioAceite, material.PrecioUnitario);
                    nudPrecioAceite.Enabled = false;
                    lblVinculoAceite.Text = material.Clave;
                    btnQuitarAceite.Enabled = true;
                    Recalcular();
                },
                "Vincular lubricante");
        }
        private void btnVincularOperador_Click(object? sender, EventArgs e)
        {
            SeleccionarVinculo(
                ConsultarOperadoresDelPropietario(_context.ManoDeObra, _maquinaria.ProyectoId),
                operador => $"{operador.Clave} — {operador.Descripcion} (${operador.SalarioBase:N2}; FSR {operador.FactorSalarioReal:N4})",
                operador =>
                {
                    _operadorIdPendiente = operador.Id;
                    nudSalarioOperador.Value = Clamp(nudSalarioOperador, operador.SalarioBase);
                    nudFSR.Value = Clamp(nudFSR, operador.FactorSalarioReal);
                    nudSalarioOperador.Enabled = false;
                    nudFSR.Enabled = false;
                    lblVinculoOperador.Text = operador.Clave;
                    btnQuitarOperador.Enabled = true;
                    Recalcular();
                },
                "Vincular operador");
        }
        private void Desvincular(Action clear, NumericUpDown first, NumericUpDown? second, Label status)
        {
            clear();
            first.Enabled = true;
            if (second != null)
                second.Enabled = true;
            status.Text = "Sin vínculo";
            if (first == nudPrecioCombustible)
                btnQuitarCombustible.Enabled = false;
            else if (first == nudPrecioAceite)
                btnQuitarAceite.Enabled = false;
            else
                btnQuitarOperador.Enabled = false;
            Recalcular();
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

        private void ConfigurarSeleccionTotal()
        {
            // Tab A — Cargos Fijos
            SeleccionTotalNumericUpDown.Configurar(nudValorAdquisicion);
            SeleccionTotalNumericUpDown.Configurar(nudValorLlantas);
            SeleccionTotalNumericUpDown.Configurar(nudValorPiezasEsp);
            SeleccionTotalNumericUpDown.Configurar(nudFactorRescate);
            SeleccionTotalNumericUpDown.Configurar(nudVidaEconomica);
            SeleccionTotalNumericUpDown.Configurar(nudTasaInteres);
            SeleccionTotalNumericUpDown.Configurar(nudHorasAnio);
            SeleccionTotalNumericUpDown.Configurar(nudPrimaSeguro);
            SeleccionTotalNumericUpDown.Configurar(nudFactorManten);

            // Tab B — Consumos
            SeleccionTotalNumericUpDown.Configurar(nudCantCombustible);
            SeleccionTotalNumericUpDown.Configurar(nudPrecioCombustible);
            SeleccionTotalNumericUpDown.Configurar(nudCantAceite);
            SeleccionTotalNumericUpDown.Configurar(nudPrecioAceite);
            SeleccionTotalNumericUpDown.Configurar(nudNumLlantas);
            SeleccionTotalNumericUpDown.Configurar(nudVidaLlantas);
            SeleccionTotalNumericUpDown.Configurar(nudVidaPiezasEsp);

            // Tab C — Operación
            SeleccionTotalNumericUpDown.Configurar(nudSalarioOperador);
            SeleccionTotalNumericUpDown.Configurar(nudFSR);
            SeleccionTotalNumericUpDown.Configurar(nudHorasTurno);
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
            var result = HourlyCostCalculator.Calculate(new HourlyCostInput
            {
                AcquisitionValue = nudValorAdquisicion.Value,
                TireValue = nudValorLlantas.Value,
                SpecialPartsValue = nudValorPiezasEsp.Value,
                SalvageFactor = nudFactorRescate.Value,
                EconomicLifeHours = nudVidaEconomica.Value,
                InterestRatePercentage = nudTasaInteres.Value,
                EffectiveHoursPerYear = nudHorasAnio.Value,
                InsuranceRatePercentage = nudPrimaSeguro.Value,
                MaintenanceFactor = nudFactorManten.Value,
                FuelQuantity = nudCantCombustible.Value,
                FuelPrice = nudPrecioCombustible.Value,
                OilQuantity = nudCantAceite.Value,
                OilPrice = nudPrecioAceite.Value,
                TireLifeHours = nudVidaLlantas.Value,
                SpecialPartsLifeHours = nudVidaPiezasEsp.Value,
                OperatorSalary = nudSalarioOperador.Value,
                RealSalaryFactor = nudFSR.Value,
                EffectiveHoursPerShift = nudHorasTurno.Value
            });

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
                AplicarPrecioManualSiEstaDesvinculado(_maquinaria, _materialCombustibleIdPendiente, true, nudPrecioCombustible.Value);
                _maquinaria.CantidadAceite         = nudCantAceite.Value;
                AplicarPrecioManualSiEstaDesvinculado(_maquinaria, _materialAceiteIdPendiente, false, nudPrecioAceite.Value);
                _maquinaria.NumeroLlantas          = (int)nudNumLlantas.Value;
                _maquinaria.VidaEconomicaLlantas   = nudVidaLlantas.Value;
                _maquinaria.VidaPiezasEspeciales   = nudVidaPiezasEsp.Value;

                AplicarOperadorManualSiEstaDesvinculado(_maquinaria, _operadorIdPendiente, nudSalarioOperador.Value, nudFSR.Value);
                _maquinaria.HorasEfectivasTurno    = nudHorasTurno.Value;

                _maquinaria.CostoHorario           = _costoHorarioTotal;
                _maquinaria.EsCostoCalculado       = true;
                _maquinaria.FechaCalculoCosto      = DateTime.Now;
                _maquinaria.FechaModificacion      = DateTime.Now;

                var dto = new MaquinariaEditDto
                {
                    Clave = _maquinaria.Clave,
                    Descripcion = _maquinaria.Descripcion,
                    PotenciaNominal = _maquinaria.PotenciaNominal,
                    TipoCombustible = _maquinaria.TipoCombustible,
                    CostoHorario = _maquinaria.CostoHorario,
                    EsCostoCalculado = true,
                    Notas = _maquinaria.Notas ?? string.Empty,
                    GuardarEnMaestro = !_maquinaria.ProyectoId.HasValue,
                    ProyectoId = _maquinaria.ProyectoId,
                    MaterialCombustibleId = _materialCombustibleIdPendiente,
                    MaterialAceiteId = _materialAceiteIdPendiente,
                    ManoDeObraOperadorId = _operadorIdPendiente
                };
                await CatalogItemService.SaveMaquinariaAsync(_context, dto, _maquinaria);

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

        internal static void AplicarPrecioManualSiEstaDesvinculado(Maquinaria maquinaria, int? materialIdPendiente, bool combustible, decimal precio)
        {
            if (materialIdPendiente.HasValue)
                return;
            if (combustible)
                maquinaria.PrecioCombustible = precio;
            else
                maquinaria.PrecioAceite = precio;
        }

        internal static void AplicarOperadorManualSiEstaDesvinculado(Maquinaria maquinaria, int? operadorIdPendiente, decimal salario, decimal fsr)
        {
            if (operadorIdPendiente.HasValue)
                return;
            maquinaria.SalarioOperador = salario;
            maquinaria.FactorSalarioReal = fsr;
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
