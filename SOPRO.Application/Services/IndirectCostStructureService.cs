using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    /// <summary>
    /// Crea la estructura predeterminada de indirectos según la configuración base del sistema.
    /// </summary>
    public static class IndirectCostStructureService
    {
        public static void CrearEstructuraPredeterminada(SOPROContext context, int proyectoId)
        {
            if (context.GruposIndirectos.Any(g => g.ProyectoId == proyectoId))
                return;

            var grupos = new List<GrupoIndirecto>();

            var grupoHonorarios = new GrupoIndirecto
            {
                ProyectoId = proyectoId,
                Nombre = "HONORARIOS, SUELDOS Y PRESTACIONES",
                Tipo = TipoIndirecto.OficinaCentral,
                Orden = 1,
                Conceptos = new List<ConceptoIndirecto>
                {
                    new() { Concepto = "Gerente General", ImporteMensual = 0, Orden = 1, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Asesoría Legal", ImporteMensual = 0, Orden = 2, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Auditoría Externa", ImporteMensual = 0, Orden = 3, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Superintendente de Obra", ImporteMensual = 0, Orden = 4, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Ingeniero de Proyectos", ImporteMensual = 0, Orden = 5, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Contador", ImporteMensual = 0, Orden = 6, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Secretaria", ImporteMensual = 0, Orden = 7, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Almacenista", ImporteMensual = 0, Orden = 8, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Velador", ImporteMensual = 0, Orden = 9, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Intendente", ImporteMensual = 0, Orden = 10, Tipo = TipoIndirecto.OficinaCentral }
                }
            };

            var grupoAlquileres = new GrupoIndirecto
            {
                ProyectoId = proyectoId,
                Nombre = "ALQUILERES Y DEPRECIACIONES",
                Tipo = TipoIndirecto.OficinaCentral,
                Orden = 2,
                Conceptos = new List<ConceptoIndirecto>
                {
                    new() { Concepto = "Renta de Edificio", ImporteMensual = 0, Orden = 1, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Renta de Bodega", ImporteMensual = 0, Orden = 2, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Depreciación Equipo de Oficina", ImporteMensual = 0, Orden = 3, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Depreciación Vehículos", ImporteMensual = 0, Orden = 4, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Depreciación Mobiliario", ImporteMensual = 0, Orden = 5, Tipo = TipoIndirecto.OficinaCentral }
                }
            };

            var grupoServicios = new GrupoIndirecto
            {
                ProyectoId = proyectoId,
                Nombre = "SERVICIOS",
                Tipo = TipoIndirecto.OficinaCentral,
                Orden = 3,
                Conceptos = new List<ConceptoIndirecto>
                {
                    new() { Concepto = "Consultoría Técnica", ImporteMensual = 0, Orden = 1, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Laboratorios", ImporteMensual = 0, Orden = 2, Tipo = TipoIndirecto.OficinaCentral }
                }
            };

            var grupoGastosOficina = new GrupoIndirecto
            {
                ProyectoId = proyectoId,
                Nombre = "GASTOS DE OFICINA",
                Tipo = TipoIndirecto.OficinaCentral,
                Orden = 4,
                Conceptos = new List<ConceptoIndirecto>
                {
                    new() { Concepto = "Teléfono e Internet", ImporteMensual = 0, Orden = 1, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Luz", ImporteMensual = 0, Orden = 2, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Agua", ImporteMensual = 0, Orden = 3, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Papelería y Consumibles", ImporteMensual = 0, Orden = 4, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Mantenimiento", ImporteMensual = 0, Orden = 5, Tipo = TipoIndirecto.OficinaCentral }
                }
            };

            var grupoCapacitacion = new GrupoIndirecto
            {
                ProyectoId = proyectoId,
                Nombre = "CAPACITACIÓN Y PROMOCIÓN",
                Tipo = TipoIndirecto.OficinaCentral,
                Orden = 5,
                Conceptos = new List<ConceptoIndirecto>
                {
                    new() { Concepto = "Cursos y Congresos", ImporteMensual = 0, Orden = 1, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Atención a Clientes", ImporteMensual = 0, Orden = 2, Tipo = TipoIndirecto.OficinaCentral },
                    new() { Concepto = "Gastos en Concursos", ImporteMensual = 0, Orden = 3, Tipo = TipoIndirecto.OficinaCentral }
                }
            };

            var grupoTecnicosCampo = new GrupoIndirecto
            {
                ProyectoId = proyectoId,
                Nombre = "TÉCNICOS Y ADMINISTRATIVOS",
                Tipo = TipoIndirecto.Campo,
                Orden = 6,
                Conceptos = new List<ConceptoIndirecto>
                {
                    new() { Concepto = "Residente de Obra", ImporteMensual = 0, Orden = 1, Tipo = TipoIndirecto.Campo },
                    new() { Concepto = "Sobrestante", ImporteMensual = 0, Orden = 2, Tipo = TipoIndirecto.Campo },
                    new() { Concepto = "Topógrafo", ImporteMensual = 0, Orden = 3, Tipo = TipoIndirecto.Campo },
                    new() { Concepto = "Velador de Obra", ImporteMensual = 0, Orden = 4, Tipo = TipoIndirecto.Campo },
                    new() { Concepto = "Almacenista de Obra", ImporteMensual = 0, Orden = 5, Tipo = TipoIndirecto.Campo }
                }
            };

            var grupoTraslados = new GrupoIndirecto
            {
                ProyectoId = proyectoId,
                Nombre = "TRASLADOS Y COMUNICACIÓN",
                Tipo = TipoIndirecto.Campo,
                Orden = 7,
                Conceptos = new List<ConceptoIndirecto>
                {
                    new() { Concepto = "Pasajes del Personal", ImporteMensual = 0, Orden = 1, Tipo = TipoIndirecto.Campo },
                    new() { Concepto = "Viáticos", ImporteMensual = 0, Orden = 2, Tipo = TipoIndirecto.Campo },
                    new() { Concepto = "Teléfono/Radio de Obra", ImporteMensual = 0, Orden = 3, Tipo = TipoIndirecto.Campo },
                    new() { Concepto = "Combustible", ImporteMensual = 0, Orden = 4, Tipo = TipoIndirecto.Campo }
                }
            };

            var grupoConstrucciones = new GrupoIndirecto
            {
                ProyectoId = proyectoId,
                Nombre = "CONSTRUCCIONES PROVISIONALES",
                Tipo = TipoIndirecto.Campo,
                Orden = 8,
                Conceptos = new List<ConceptoIndirecto>
                {
                    new() { Concepto = "Oficina de Campo", ImporteMensual = 0, Orden = 1, Tipo = TipoIndirecto.Campo },
                    new() { Concepto = "Almacén", ImporteMensual = 0, Orden = 2, Tipo = TipoIndirecto.Campo },
                    new() { Concepto = "Comedor", ImporteMensual = 0, Orden = 3, Tipo = TipoIndirecto.Campo },
                    new() { Concepto = "Sanitarios", ImporteMensual = 0, Orden = 4, Tipo = TipoIndirecto.Campo },
                    new() { Concepto = "Instalaciones (Luz, Agua)", ImporteMensual = 0, Orden = 5, Tipo = TipoIndirecto.Campo }
                }
            };

            var grupoConsumos = new GrupoIndirecto
            {
                ProyectoId = proyectoId,
                Nombre = "CONSUMOS",
                Tipo = TipoIndirecto.Campo,
                Orden = 9,
                Conceptos = new List<ConceptoIndirecto>
                {
                    new() { Concepto = "Papelería de Obra", ImporteMensual = 0, Orden = 1, Tipo = TipoIndirecto.Campo },
                    new() { Concepto = "Uniformes", ImporteMensual = 0, Orden = 2, Tipo = TipoIndirecto.Campo },
                    new() { Concepto = "Equipo de Seguridad", ImporteMensual = 0, Orden = 3, Tipo = TipoIndirecto.Campo }
                }
            };

            grupos.Add(grupoHonorarios);
            grupos.Add(grupoAlquileres);
            grupos.Add(grupoServicios);
            grupos.Add(grupoGastosOficina);
            grupos.Add(grupoCapacitacion);
            grupos.Add(grupoTecnicosCampo);
            grupos.Add(grupoTraslados);
            grupos.Add(grupoConstrucciones);
            grupos.Add(grupoConsumos);

            context.GruposIndirectos.AddRange(grupos);

            var config = new ConfiguracionIndirectos
            {
                ProyectoId = proyectoId,
                VolumenAnualObra = 0,
                CostoDirectoObra = 0,
                TotalOficinaCentralAnual = 0,
                TotalCampo = 0,
                PorcentajeOficinaCentral = 0,
                PorcentajeCampo = 0
            };

            context.ConfiguracionesIndirectos.Add(config);
            context.SaveChanges();
        }
    }
}
