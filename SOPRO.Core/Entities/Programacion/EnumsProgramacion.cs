namespace SOPRO.Core.Entities
{
    public enum TipoPeriodoPrograma
    {
        Dia = 1,
        Semana = 2,
        Quincena = 3,
        Mes = 4
    }

    public enum TipoDependenciaActividad
    {
        FS = 1,
        SS = 2,
        FF = 3,
        SF = 4
    }

    public enum TipoRestriccionActividad
    {
        LoAntesPosible = 1,
        NoIniciarAntesDe = 2,
        NoFinalizarAntesDe = 3,
        DebeIniciarEl = 4,
        DebeFinalizarEl = 5
    }

    public enum MetodoDistribucionActividad
    {
        Uniforme = 1,
        ManualPorPorcentaje = 2,
        PorRendimiento = 3,
        CurvaCampana = 4
    }

    public enum TipoExcepcionCalendario
    {
        Inhabil = 1,
        LaborableEspecial = 2,
        Suspension = 3,
        Lluvia = 4
    }
}
