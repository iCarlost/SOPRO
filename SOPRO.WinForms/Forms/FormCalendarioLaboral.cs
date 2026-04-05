using System.ComponentModel;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Forms
{
    public partial class FormCalendarioLaboral : Form
    {
        private readonly SOPROContext _context;
        private readonly Proyecto _proyecto;
        private readonly int? _calendarioId;
        private readonly ProgramacionLoadService _loadService = new();
        private readonly ProgramacionPersistenceService _persistenceService = new();
        private BindingList<CalendarExceptionEditDto> _excepciones = new();

        public bool CalendarioActualizado { get; private set; }

        public FormCalendarioLaboral(SOPROContext context, Proyecto proyecto, int? calendarioId)
        {
            _context = context;
            _proyecto = proyecto;
            _calendarioId = calendarioId;
            InitializeComponent();
            ConfigurarFormulario();
        }

        private void ConfigurarFormulario()
        {
            FormRenderHelper.OptimizarRender(this);
            dgvExcepciones.AutoGenerateColumns = false;
            dgvExcepciones.AplicarEstiloSOPRO();
            DgvCeldaHelper.Aplicar(dgvExcepciones);

            colTipoExcepcion.DataSource = Enum.GetValues(typeof(TipoExcepcionCalendario));
        }

        private void FormCalendarioLaboral_Load(object sender, EventArgs e)
        {
            var dto = _loadService.LoadCalendar(_context, _proyecto.Id, _calendarioId);

            txtNombre.Text = dto.Nombre;
            chkLunes.Checked = dto.Lunes;
            chkMartes.Checked = dto.Martes;
            chkMiercoles.Checked = dto.Miercoles;
            chkJueves.Checked = dto.Jueves;
            chkViernes.Checked = dto.Viernes;
            chkSabado.Checked = dto.Sabado;
            chkDomingo.Checked = dto.Domingo;
            dtpHoraInicio.Value = DateTime.Today.Add(dto.HoraInicio);
            dtpHoraFin.Value = DateTime.Today.Add(dto.HoraFin);

            _excepciones = new BindingList<CalendarExceptionEditDto>(dto.Excepciones.OrderBy(x => x.Fecha).ToList());
            dgvExcepciones.DataSource = _excepciones;
        }

        private void btnAgregarExcepcion_Click(object sender, EventArgs e)
        {
            _excepciones.Add(new CalendarExceptionEditDto
            {
                Fecha = DateTime.Today,
                Tipo = TipoExcepcionCalendario.Inhabil
            });
        }

        private void btnEliminarExcepcion_Click(object sender, EventArgs e)
        {
            if (dgvExcepciones.CurrentRow?.DataBoundItem is CalendarExceptionEditDto dto)
            {
                _excepciones.Remove(dto);
            }
        }

        private void btnGuardar_Click(object sender, EventArgs e)
        {
            try
            {
                var dto = new CalendarEditDto
                {
                    Id = _calendarioId,
                    ProyectoId = _proyecto.Id,
                    Nombre = string.IsNullOrWhiteSpace(txtNombre.Text) ? "Calendario General" : txtNombre.Text.Trim(),
                    Lunes = chkLunes.Checked,
                    Martes = chkMartes.Checked,
                    Miercoles = chkMiercoles.Checked,
                    Jueves = chkJueves.Checked,
                    Viernes = chkViernes.Checked,
                    Sabado = chkSabado.Checked,
                    Domingo = chkDomingo.Checked,
                    HoraInicio = dtpHoraInicio.Value.TimeOfDay,
                    HoraFin = dtpHoraFin.Value.TimeOfDay,
                    Excepciones = _excepciones
                        .Where(x => x.Fecha != default)
                        .OrderBy(x => x.Fecha)
                        .ToList()
                };

                if (dto.HoraFin <= dto.HoraInicio)
                {
                    MessageBox.Show("La hora fin debe ser mayor a la hora inicio.", "Calendario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var result = _persistenceService.SaveCalendar(_context, dto);
                if (!result.Ok)
                {
                    MessageBox.Show(result.Error, "Calendario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                CalendarioActualizado = true;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo guardar el calendario: {ex.Message}", "Calendario", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
