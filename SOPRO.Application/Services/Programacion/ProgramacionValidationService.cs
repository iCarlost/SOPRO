using SOPRO.Application.DTOs.Programacion;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class ProgramacionValidationService
    {
        private const int MaxDuracionDiasHabiles = 36500;
        private const decimal MinRendimientoDiario = 0.0001m;
        private readonly ProgramacionCalculationService _calculationService = new();

        public (bool Ok, string Error) ValidateActivity(ActivityEditDto dto)
        {
            if (dto.ProgramaObraId <= 0)
                return (false, "El programa de obra es obligatorio.");

            if (string.IsNullOrWhiteSpace(dto.Descripcion))
                return (false, "La descripción de la actividad es obligatoria.");

            if (dto.CantidadTotal < 0)
                return (false, "La cantidad no puede ser negativa.");

            if (dto.FrentesTrabajo <= 0)
                return (false, "Los frentes de trabajo deben ser mayores a cero.");

            if (dto.DuracionDiasHabiles < 0)
                return (false, "La duración no puede ser negativa.");

            if (dto.DuracionDiasHabiles > MaxDuracionDiasHabiles)
                return (false, $"La duración no puede ser mayor a {MaxDuracionDiasHabiles:N0} días hábiles. Revisa el rendimiento diario, los frentes o las fechas capturadas.");

            if (dto.RendimientoDiario < 0)
                return (false, "El rendimiento diario no puede ser negativo.");

            if (dto.RendimientoDiario > 0 && dto.RendimientoDiario < MinRendimientoDiario)
                return (false, $"El rendimiento diario es demasiado pequeño. Captura un valor mayor o igual a {MinRendimientoDiario}." );

            if (dto.CantidadTotal > 0 && dto.RendimientoDiario > 0)
            {
                var frentes = Math.Max(1, dto.FrentesTrabajo);
                var duracionEstimada = (int)Math.Ceiling(dto.CantidadTotal / (dto.RendimientoDiario * frentes));
                if (duracionEstimada > MaxDuracionDiasHabiles)
                    return (false, $"La combinación de cantidad, rendimiento diario y frentes genera un plazo irreal ({duracionEstimada:N0} días hábiles). Revisa los valores capturados.");
            }

            if (dto.FechaInicioProgramada.HasValue && dto.FechaFinProgramada.HasValue && dto.FechaFinProgramada.Value < dto.FechaInicioProgramada.Value)
                return (false, "La fecha fin no puede ser menor a la fecha inicio.");

            return (true, string.Empty);
        }

        public (bool Ok, string Error) ValidateActivity(SOPROContext context, ActivityEditDto dto)
        {
            var validation = ValidateActivity(dto);
            if (!validation.Ok)
                return validation;

            if (dto.FechaInicioProgramada.HasValue && !_calculationService.IsWorkingDay(context, dto.ProgramaObraId, dto.FechaInicioProgramada))
                return (false, $"La fecha inicio {dto.FechaInicioProgramada:dd/MM/yyyy} es inhábil según el calendario laboral del programa.");

            if (dto.FechaFinProgramada.HasValue && !_calculationService.IsWorkingDay(context, dto.ProgramaObraId, dto.FechaFinProgramada))
                return (false, $"La fecha fin {dto.FechaFinProgramada:dd/MM/yyyy} es inhábil según el calendario laboral del programa.");

            return (true, string.Empty);
        }
    }
}
