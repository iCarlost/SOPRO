using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System;
using System.IO;
using System.Linq;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Inicializa la base de datos y crea catálogos maestros con datos de ejemplo
    /// </summary>
    public static class DatabaseInitializer
    {
        private static readonly string SoproFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SOPRO"
        );
        
        private static readonly string MasterDbPath = Path.Combine(SoproFolder, "CatalogoMaestro.db");
        
        /// <summary>
        /// Asegura que existe la carpeta SOPRO
        /// </summary>
        public static void EnsureFolderExists()
        {
            if (!Directory.Exists(SoproFolder))
            {
                Directory.CreateDirectory(SoproFolder);
            }
            
            var projectsFolder = Path.Combine(SoproFolder, "Proyectos");
            if (!Directory.Exists(projectsFolder))
            {
                Directory.CreateDirectory(projectsFolder);
            }
        }
        
        /// <summary>
        /// Inicializa el catálogo maestro si no existe
        /// </summary>
        public static void InitializeMasterCatalog()
        {
            EnsureFolderExists();
            
            // Si ya existe, no hacer nada
            if (File.Exists(MasterDbPath))
                return;
            
            using var context = new SOPROContext(MasterDbPath);
            
            // Crear la base de datos
            context.Database.EnsureCreated();

            // Fuente única de verdad del schema
            SOPRO.Application.Services.SchemaManager.EnsureCurrentSchema(context);

            // Seed con datos básicos
            SeedMasterCatalog(context);
        }
        
        /// <summary>
        /// Crea un nuevo proyecto con su base de datos
        /// </summary>
        public static SOPROContext CreateNewProject(string projectName)
        {
            var sanitizedName = SanitizeFileName(projectName);
            var dbPath = Path.Combine(SoproFolder, "Proyectos", $"{sanitizedName}.db");
            
            // Crear carpeta de proyectos si no existe
            var projectsFolder = Path.Combine(SoproFolder, "Proyectos");
            if (!Directory.Exists(projectsFolder))
            {
                Directory.CreateDirectory(projectsFolder);
            }
            
            var context = new SOPROContext(dbPath);
            context.Database.EnsureCreated();

            // Fuente única de verdad del schema
            SOPRO.Application.Services.SchemaManager.EnsureCurrentSchema(context);

            return context;
        }
        

        /// <summary>
        /// Aplica upgrades de schema para BDs existentes.
        /// Delega a SchemaManager — fuente única de verdad del schema.
        /// </summary>
        public static void UpgradeSchema(SOPROContext context)
        {
            SOPRO.Application.Services.SchemaManager.EnsureCurrentSchema(context);
        }

        private static void SeedMasterCatalog(SOPROContext context)
        {
            // ═══════════════════════════════════════════════════════════
            // MATERIALES MAESTROS
            // ═══════════════════════════════════════════════════════════
            var materiales = new[]
            {
                new Material
                {
                    Clave = "MT-ARENA",
                    Descripcion = "Arena cribada puesta en obra",
                    Unidad = "m3",
                    PrecioUnitario = 457.00m,
                    Origen = OrigenInsumo.Maestro
                },
                new Material
                {
                    Clave = "MT-CAL",
                    Descripcion = "Cal - Hidra",
                    Unidad = "ton",
                    PrecioUnitario = 3784.50m,
                    Origen = OrigenInsumo.Maestro
                },
                new Material
                {
                    Clave = "MT-CEMENT",
                    Descripcion = "Cemento gris CPC 30 R",
                    Unidad = "ton",
                    PrecioUnitario = 4700.00m,
                    Origen = OrigenInsumo.Maestro
                },
                new Material
                {
                    Clave = "MT-CLAVO",
                    Descripcion = "Clavos de 2 1/2\"",
                    Unidad = "kg",
                    PrecioUnitario = 21.55m,
                    Origen = OrigenInsumo.Maestro
                },
                new Material
                {
                    Clave = "MT-CONC200",
                    Descripcion = "Concreto premezclado f'c=200 kg/cm2",
                    Unidad = "m3",
                    PrecioUnitario = 2850.00m,
                    Origen = OrigenInsumo.Maestro
                },
                new Material
                {
                    Clave = "MT-MADERA",
                    Descripcion = "Madera de pino tercera",
                    Unidad = "pt",
                    PrecioUnitario = 20.00m,
                    Origen = OrigenInsumo.Maestro
                },
                new Material
                {
                    Clave = "MT-GRAVA",
                    Descripcion = "Grava 3/4\"",
                    Unidad = "m3",
                    PrecioUnitario = 487.00m,
                    Origen = OrigenInsumo.Maestro
                },
                new Material
                {
                    Clave = "MT-ACERO",
                    Descripcion = "Acero de refuerzo fy=4200 kg/cm2",
                    Unidad = "ton",
                    PrecioUnitario = 18500.00m,
                    Origen = OrigenInsumo.Maestro
                }
            };
            
            context.Materiales.AddRange(materiales);
            
            // ═══════════════════════════════════════════════════════════
            // MANO DE OBRA MAESTRA
            // ═══════════════════════════════════════════════════════════
            var manoDeObra = new[]
            {
                new ManoDeObra
                {
                    Clave = "MO-PEON",
                    Descripcion = "Peón",
                    Unidad = "jor",
                    SalarioBase = 450.00m,
                    FactorSalarioReal = 1.6543m,
                    SalarioReal = 744.44m, // Calculado
                    Origen = OrigenInsumo.Maestro
                },
                new ManoDeObra
                {
                    Clave = "MO-CARP",
                    Descripcion = "Carpintero",
                    Unidad = "jor",
                    SalarioBase = 550.00m,
                    FactorSalarioReal = 1.6543m,
                    SalarioReal = 909.87m,
                    Origen = OrigenInsumo.Maestro
                },
                new ManoDeObra
                {
                    Clave = "MO-ALB",
                    Descripcion = "Albañil",
                    Unidad = "jor",
                    SalarioBase = 580.00m,
                    FactorSalarioReal = 1.6543m,
                    SalarioReal = 959.49m,
                    Origen = OrigenInsumo.Maestro
                },
                new ManoDeObra
                {
                    Clave = "MO-SOLD",
                    Descripcion = "Soldador",
                    Unidad = "jor",
                    SalarioBase = 650.00m,
                    FactorSalarioReal = 1.6543m,
                    SalarioReal = 1075.30m,
                    Origen = OrigenInsumo.Maestro
                },
                new ManoDeObra
                {
                    Clave = "MO-OFICIAL",
                    Descripcion = "Oficial albañil",
                    Unidad = "jor",
                    SalarioBase = 520.00m,
                    FactorSalarioReal = 1.6543m,
                    SalarioReal = 860.24m,
                    Origen = OrigenInsumo.Maestro
                },
                new ManoDeObra
                {
                    Clave = "MO-FIERRERO",
                    Descripcion = "Fierrero",
                    Unidad = "jor",
                    SalarioBase = 570.00m,
                    FactorSalarioReal = 1.6543m,
                    SalarioReal = 942.95m,
                    Origen = OrigenInsumo.Maestro
                },
                new ManoDeObra
                {
                    Clave = "MO-CABESTRANTE",
                    Descripcion = "Cabestrante",
                    Unidad = "jor",
                    SalarioBase = 480.00m,
                    FactorSalarioReal = 1.6543m,
                    SalarioReal = 794.06m,
                    Origen = OrigenInsumo.Maestro
                }
            };
            
            context.ManoDeObra.AddRange(manoDeObra);
            
            // ═══════════════════════════════════════════════════════════
            // MAQUINARIA MAESTRA (Simplificada - solo con costo manual)
            // ═══════════════════════════════════════════════════════════
            var maquinaria = new[]
            {
                new Maquinaria
                {
                    Clave = "MQ-B2",
                    Descripcion = "Bomba autocebante 2\"",
                    PotenciaNominal = 4.00m,
                    TipoCombustible = TipoCombustible.Gasolina,
                    CostoHorario = 50.00m,
                    EsCostoCalculado = false,
                    Origen = OrigenInsumo.Maestro
                },
                new Maquinaria
                {
                    Clave = "MQ-CM",
                    Descripcion = "Compactador Manual Wacker",
                    PotenciaNominal = 4.00m,
                    TipoCombustible = TipoCombustible.Gasolina,
                    CostoHorario = 55.00m,
                    EsCostoCalculado = false,
                    Origen = OrigenInsumo.Maestro
                },
                new Maquinaria
                {
                    Clave = "MQ-CV",
                    Descripcion = "Camión Volteo 6m3",
                    PotenciaNominal = 110.00m,
                    TipoCombustible = TipoCombustible.Diesel,
                    CostoHorario = 285.50m,
                    EsCostoCalculado = false,
                    Origen = OrigenInsumo.Maestro
                },
                new Maquinaria
                {
                    Clave = "MQ-RT",
                    Descripcion = "Retroexcavadora Caterpillar",
                    PotenciaNominal = 94.00m,
                    TipoCombustible = TipoCombustible.Diesel,
                    CostoHorario = 395.00m,
                    EsCostoCalculado = false,
                    Origen = OrigenInsumo.Maestro
                },
                new Maquinaria
                {
                    Clave = "MQ-REV",
                    Descripcion = "Revolvedora de concreto 1 saco",
                    PotenciaNominal = 8.00m,
                    TipoCombustible = TipoCombustible.Gasolina,
                    CostoHorario = 65.00m,
                    EsCostoCalculado = false,
                    Origen = OrigenInsumo.Maestro
                },
                new Maquinaria
                {
                    Clave = "MQ-TOPO",
                    Descripcion = "Equipo topográfico SOKKIA",
                    PotenciaNominal = 0m,
                    TipoCombustible = TipoCombustible.SinMotor,
                    CostoHorario = 120.00m,
                    EsCostoCalculado = false,
                    Origen = OrigenInsumo.Maestro
                }
            };
            
            context.Maquinaria.AddRange(maquinaria);
            
            // Guardar cambios
            context.SaveChanges();
        }
        
        private static string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }
    }
}
