using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using System;

namespace SOPRO.Data.Context
{
    /// <summary>
    /// Contexto de base de datos principal de SOPRO
    /// SQLite con un archivo .db por proyecto
    /// </summary>
    public class SOPROContext : DbContext
    {
        // ═══════════════════════════════════════════════════════════
        // CATÁLOGOS
        // ═══════════════════════════════════════════════════════════
        public DbSet<Proyecto> Proyectos { get; set; }
        public DbSet<Material> Materiales { get; set; }
        public DbSet<ManoDeObra> ManoDeObra { get; set; }
        public DbSet<Maquinaria> Maquinaria { get; set; }
        public DbSet<Herramienta> Herramientas { get; set; }
        
        // ═══════════════════════════════════════════════════════════
        // ANÁLISIS
        // ═══════════════════════════════════════════════════════════
        public DbSet<Matriz> Matrices { get; set; }
        public DbSet<ComponenteMatriz> ComponentesMatriz { get; set; }
        
        // ═══════════════════════════════════════════════════════════
        // PRESUPUESTO
        // ═══════════════════════════════════════════════════════════
        public DbSet<ConceptoPresupuesto> ConceptosPresupuesto { get; set; }

        // ═══════════════════════════════════════════════════════════
        // PROGRAMACIÓN DE OBRA
        // ═══════════════════════════════════════════════════════════
        public DbSet<ProgramaObra> ProgramasObra { get; set; }
        public DbSet<ActividadProgramada> ActividadesProgramadas { get; set; }
        public DbSet<DependenciaActividad> DependenciasActividad { get; set; }
        public DbSet<PeriodoPrograma> PeriodosPrograma { get; set; }
        public DbSet<DistribucionPeriodo> DistribucionesPeriodo { get; set; }
        public DbSet<CalendarioLaboral> CalendariosLaborales { get; set; }
        public DbSet<ExcepcionCalendario> ExcepcionesCalendario { get; set; }
        
        // Configuración de columnas de módulos de programación
        public DbSet<ColumnaProgramaObra> ColumnasProgramaObra { get; set; }
        public DbSet<ColumnaProgramaInsumos> ColumnasProgramaInsumos { get; set; }
        
        // ═══════════════════════════════════════════════════════════
        // PARÁMETROS Y CONFIGURACIÓN
        // ═══════════════════════════════════════════════════════════
        public DbSet<ColumnaPersonalizada> ColumnasPersonalizadas { get; set; }
        public DbSet<SOPRO.Core.Entities.ColumnaExplosion> ColumnasExplosion { get; set; }
        public DbSet<SOPRO.Core.Entities.ColumnaIndirectos> ColumnasIndirectos { get; set; }
        public DbSet<SOPRO.Core.Entities.ColumnaMaterial> ColumnasMaterial { get; set; }
        public DbSet<SOPRO.Core.Entities.ColumnaManoObra> ColumnasManoObra { get; set; }
        public DbSet<SOPRO.Core.Entities.ColumnaHerramienta> ColumnasHerramienta { get; set; }
        public DbSet<SOPRO.Core.Entities.ColumnaMaquinaria> ColumnasMaquinaria { get; set; }
        public DbSet<SOPRO.Core.Entities.ColumnaMatriz> ColumnasMatriz { get; set; }
        public DbSet<SOPRO.Core.Entities.ColumnaFinanciamiento> ColumnasFinanciamiento { get; set; }
        public DbSet<VistaPresupuesto> VistasPresupuesto { get; set; }
        public DbSet<GrupoIndirecto> GruposIndirectos { get; set; }
        public DbSet<ConceptoIndirecto> ConceptosIndirectos { get; set; }
        public DbSet<ConfiguracionIndirectos> ConfiguracionesIndirectos { get; set; }
        public DbSet<ConfiguracionFinanciamiento> ConfiguracionesFinanciamiento { get; set; }
        public DbSet<FilaFlujoCajaFinanciamiento> FilasFlujoCajaFinanciamiento { get; set; }
        
        // ═══════════════════════════════════════════════════════════
        // REPORTES
        // ═══════════════════════════════════════════════════════════
        public DbSet<PlantillaReporte> PlantillasReporte { get; set; }
        public DbSet<PlantillaReporteElemento> PlantillasReporteElementos { get; set; }
        public DbSet<ConfigColumnaReporte> ConfigColumnasReporte { get; set; }
        public DbSet<ConfiguracionTituloReporte> ConfiguracionesTituloReporte { get; set; }
        
        // Ruta del archivo de base de datos
        private readonly string _dbPath;
        public string DatabasePath => _dbPath;
        
        /// <summary>
        /// Constructor por defecto (para catálogo maestro)
        /// </summary>
        public SOPROContext()
        {
            _dbPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "SOPRO",
                "CatalogoMaestro.db"
            );
        }
        
        /// <summary>
        /// Constructor con ruta específica (para proyectos)
        /// </summary>
        public SOPROContext(string dbPath)
        {
            _dbPath = dbPath;
        }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite($"Data Source={_dbPath}");
            }
        }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // ═══════════════════════════════════════════════════════════
            // CONFIGURACIÓN DE PROYECTO
            // ═══════════════════════════════════════════════════════════
            modelBuilder.Entity<Proyecto>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(200);
                entity.Property(e => e.PorcentajeIndirectosCentral).HasPrecision(18, 4);
                entity.Property(e => e.PorcentajeIndirectosCampo).HasPrecision(18, 4);
                entity.Property(e => e.PorcentajeFinanciamiento).HasPrecision(18, 4);
                entity.Property(e => e.PorcentajeUtilidad).HasPrecision(18, 4);
                entity.Property(e => e.PorcentajeCargosAdicionales).HasPrecision(18, 4);
                entity.Property(e => e.PorcentajeIVA).HasPrecision(18, 4);
                entity.Property(e => e.FactorSalarioReal).HasPrecision(18, 6);
            });
            
            // ═══════════════════════════════════════════════════════════
            // CONFIGURACIÓN DE MATERIALES
            // ═══════════════════════════════════════════════════════════
            modelBuilder.Entity<Material>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Clave).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Descripcion).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Unidad).IsRequired().HasMaxLength(20);
                entity.Property(e => e.PrecioUnitario).HasPrecision(18, 4);
                
                entity.HasOne(e => e.Proyecto)
                    .WithMany(p => p.Materiales)
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(e => new { e.ProyectoId, e.Clave }).IsUnique();
            });
            
            // ═══════════════════════════════════════════════════════════
            // CONFIGURACIÓN DE MANO DE OBRA
            // ═══════════════════════════════════════════════════════════
            modelBuilder.Entity<ManoDeObra>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Clave).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Descripcion).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Unidad).IsRequired().HasMaxLength(20);
                entity.Property(e => e.SalarioBase).HasPrecision(18, 4);
                entity.Property(e => e.FactorSalarioReal).HasPrecision(18, 6);
                entity.Property(e => e.SalarioReal).HasPrecision(18, 4);
                
                entity.HasOne(e => e.Proyecto)
                    .WithMany(p => p.ManoDeObra)
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(e => new { e.ProyectoId, e.Clave }).IsUnique();
            });
            
            // ═══════════════════════════════════════════════════════════
            // CONFIGURACIÓN DE MAQUINARIA
            // ═══════════════════════════════════════════════════════════
            modelBuilder.Entity<Maquinaria>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Clave).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Descripcion).IsRequired().HasMaxLength(500);
                
                // Campos con precisión decimal
                entity.Property(e => e.PotenciaNominal).HasPrecision(18, 2);
                entity.Property(e => e.ValorAdquisicion).HasPrecision(18, 2);
                entity.Property(e => e.ValorLlantas).HasPrecision(18, 2);
                entity.Property(e => e.ValorPiezasEspeciales).HasPrecision(18, 2);
                entity.Property(e => e.FactorRescate).HasPrecision(18, 6);
                entity.Property(e => e.VidaEconomica).HasPrecision(18, 2);
                entity.Property(e => e.TasaInteres).HasPrecision(18, 4);
                entity.Property(e => e.HorasEfectivasAnio).HasPrecision(18, 2);
                entity.Property(e => e.PrimaSeguro).HasPrecision(18, 4);
                entity.Property(e => e.FactorMantenimiento).HasPrecision(18, 6);
                entity.Property(e => e.CantidadCombustible).HasPrecision(18, 4);
                entity.Property(e => e.PrecioCombustible).HasPrecision(18, 4);
                entity.Property(e => e.CantidadAceite).HasPrecision(18, 4);
                entity.Property(e => e.PrecioAceite).HasPrecision(18, 4);
                entity.Property(e => e.VidaEconomicaLlantas).HasPrecision(18, 2);
                entity.Property(e => e.VidaPiezasEspeciales).HasPrecision(18, 2);
                entity.Property(e => e.SalarioOperador).HasPrecision(18, 4);
                entity.Property(e => e.FactorSalarioReal).HasPrecision(18, 6);
                entity.Property(e => e.HorasEfectivasTurno).HasPrecision(18, 2);
                entity.Property(e => e.CostoHorario).HasPrecision(18, 4);
                
                entity.HasOne(e => e.Proyecto)
                    .WithMany(p => p.Maquinaria)
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(e => new { e.ProyectoId, e.Clave }).IsUnique();
            });
            
            // ═══════════════════════════════════════════════════════════
            // CONFIGURACIÓN DE MATRICES (APU / BÁSICOS)
            // ═══════════════════════════════════════════════════════════
            modelBuilder.Entity<Matriz>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Clave).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Descripcion).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Unidad).IsRequired().HasMaxLength(20);
                entity.Property(e => e.CostoDirecto).HasPrecision(18, 4);
                
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(e => new { e.ProyectoId, e.Clave }).IsUnique();
            });
            
            // ═══════════════════════════════════════════════════════════
            // CONFIGURACIÓN DE COMPONENTES DE MATRIZ
            // ═══════════════════════════════════════════════════════════
            modelBuilder.Entity<ComponenteMatriz>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Cantidad).HasPrecision(18, 6);
                entity.Property(e => e.Rendimiento).HasPrecision(18, 5);
                entity.Property(e => e.Importe).HasPrecision(18, 4);
                
                entity.HasOne(e => e.Matriz)
                    .WithMany(m => m.Componentes)
                    .HasForeignKey(e => e.MatrizId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.Material)
                    .WithMany()
                    .HasForeignKey(e => e.MaterialId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.ManoDeObra)
                    .WithMany()
                    .HasForeignKey(e => e.ManoDeObraId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.Maquinaria)
                    .WithMany()
                    .HasForeignKey(e => e.MaquinariaId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.Auxiliar)
                    .WithMany()
                    .HasForeignKey(e => e.AuxiliarId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            
            // ═══════════════════════════════════════════════════════════
            // CONFIGURACIÓN DE CONCEPTOS DE PRESUPUESTO (CON JERARQUÍA)
            // ═══════════════════════════════════════════════════════════
            modelBuilder.Entity<ConceptoPresupuesto>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Clave).HasMaxLength(50);
                entity.Property(e => e.Descripcion).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Unidad).HasMaxLength(20); // No es Required, puede ser null en agrupadores
                
                entity.Property(e => e.Cantidad).HasPrecision(18, 6);
                entity.Property(e => e.CostoDirectoUnitario).HasPrecision(18, 4);
                entity.Property(e => e.CostoDirectoTotal).HasPrecision(18, 4);
                entity.Property(e => e.Indirectos).HasPrecision(18, 4);
                entity.Property(e => e.Financiamiento).HasPrecision(18, 4);
                entity.Property(e => e.Utilidad).HasPrecision(18, 4);
                entity.Property(e => e.CargosAdicionales).HasPrecision(18, 4);
                entity.Property(e => e.PrecioUnitario).HasPrecision(18, 4);
                entity.Property(e => e.ImporteTotal).HasPrecision(18, 4);
                
                // Relación con Proyecto
                entity.HasOne(e => e.Proyecto)
                    .WithMany(p => p.Conceptos)
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // Relación jerárquica (padre-hijos)
                entity.HasOne(e => e.Padre)
                    .WithMany(p => p.Hijos)
                    .HasForeignKey(e => e.PadreId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                // Relación con Matriz (APU)
                entity.HasOne(e => e.Matriz)
                    .WithMany()
                    .HasForeignKey(e => e.MatrizId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasIndex(e => new { e.ProyectoId, e.Orden });
            });
            
            // ═══════════════════════════════════════════════════════════
            // CONFIGURACIÓN DE PROGRAMACIÓN DE OBRA
            // ═══════════════════════════════════════════════════════════
            modelBuilder.Entity<ProgramaObra>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Descripcion).HasMaxLength(500);

                entity.HasOne(e => e.Proyecto)
                    .WithMany(p => p.ProgramasObra)
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.CalendarioLaboral)
                    .WithMany()
                    .HasForeignKey(e => e.CalendarioLaboralId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.ProyectoId, e.Nombre });
            });

            modelBuilder.Entity<ActividadProgramada>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Clave).HasMaxLength(100);
                entity.Property(e => e.Descripcion).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.Unidad).HasMaxLength(50);
                entity.Property(e => e.Notas).HasMaxLength(2000);
                entity.Property(e => e.CantidadTotal).HasPrecision(18, 4);
                entity.Property(e => e.CantidadProgramada).HasPrecision(18, 4);
                entity.Property(e => e.AvanceProgramadoPorcentaje).HasPrecision(18, 4);
                entity.Property(e => e.PrecioUnitario).HasPrecision(18, 4);
                entity.Property(e => e.ImporteTotal).HasPrecision(18, 4);
                entity.Property(e => e.ImporteProgramado).HasPrecision(18, 4);
                entity.Property(e => e.RendimientoDiario).HasPrecision(18, 4);

                entity.HasOne(e => e.ProgramaObra)
                    .WithMany(p => p.Actividades)
                    .HasForeignKey(e => e.ProgramaObraId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ConceptoPresupuesto)
                    .WithMany()
                    .HasForeignKey(e => e.ConceptoPresupuestoId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.ActividadPadre)
                    .WithMany(e => e.Hijas)
                    .HasForeignKey(e => e.ActividadPadreId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => new { e.ProgramaObraId, e.Orden });
                entity.HasIndex(e => new { e.ProgramaObraId, e.ConceptoPresupuestoId });
            });

            modelBuilder.Entity<DependenciaActividad>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.ActividadOrigen)
                    .WithMany(a => a.Sucesoras)
                    .HasForeignKey(e => e.ActividadOrigenId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ActividadDestino)
                    .WithMany(a => a.Predecesoras)
                    .HasForeignKey(e => e.ActividadDestinoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.ActividadOrigenId, e.ActividadDestinoId }).IsUnique();
            });

            modelBuilder.Entity<PeriodoPrograma>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Etiqueta).IsRequired().HasMaxLength(100);

                entity.HasOne(e => e.ProgramaObra)
                    .WithMany(p => p.Periodos)
                    .HasForeignKey(e => e.ProgramaObraId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.ProgramaObraId, e.NumeroPeriodo }).IsUnique();
            });

            modelBuilder.Entity<DistribucionPeriodo>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CantidadProgramada).HasPrecision(18, 4);
                entity.Property(e => e.PorcentajeProgramado).HasPrecision(18, 4);
                entity.Property(e => e.PrecioUnitario).HasPrecision(18, 4);
                entity.Property(e => e.ImporteProgramado).HasPrecision(18, 4);

                entity.HasOne(e => e.ActividadProgramada)
                    .WithMany(a => a.Distribuciones)
                    .HasForeignKey(e => e.ActividadProgramadaId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.PeriodoPrograma)
                    .WithMany(p => p.Distribuciones)
                    .HasForeignKey(e => e.PeriodoProgramaId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.ActividadProgramadaId, e.PeriodoProgramaId }).IsUnique();
            });

            modelBuilder.Entity<CalendarioLaboral>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(150);

                entity.HasOne(e => e.Proyecto)
                    .WithMany(p => p.CalendariosLaborales)
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.ProyectoId, e.Nombre });
            });

            modelBuilder.Entity<ExcepcionCalendario>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Descripcion).HasMaxLength(300);

                entity.HasOne(e => e.CalendarioLaboral)
                    .WithMany(c => c.Excepciones)
                    .HasForeignKey(e => e.CalendarioLaboralId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.CalendarioLaboralId, e.Fecha }).IsUnique();
            });

            modelBuilder.Entity<ColumnaProgramaObra>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(200);
                entity.Property(e => e.NombreInterno).IsRequired().HasMaxLength(200);
                entity.Property(e => e.FormatoNumerico).HasMaxLength(50);
                entity.Property(e => e.NombreFuente).HasMaxLength(100);
                entity.Property(e => e.ColorFuente).HasMaxLength(20);
                entity.Property(e => e.ColorFondo).HasMaxLength(20);

                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.ProyectoId, e.NombreInterno }).IsUnique();
            });

            modelBuilder.Entity<ColumnaProgramaInsumos>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(200);
                entity.Property(e => e.NombreInterno).IsRequired().HasMaxLength(200);
                entity.Property(e => e.FormatoNumerico).HasMaxLength(50);
                entity.Property(e => e.NombreFuente).HasMaxLength(100);
                entity.Property(e => e.ColorFuente).HasMaxLength(20);
                entity.Property(e => e.ColorFondo).HasMaxLength(20);

                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.ProyectoId, e.NombreInterno }).IsUnique();
            });

            // ═══════════════════════════════════════════════════════════
            // CONFIGURACIÓN DE COLUMNAS PERSONALIZADAS
            // ═══════════════════════════════════════════════════════════
            modelBuilder.Entity<ColumnaPersonalizada>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.NombreInterno).IsRequired().HasMaxLength(50);
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(e => new { e.ProyectoId, e.NombreInterno }).IsUnique();
            });
            
            // ═══════════════════════════════════════════════════════════
            // CONFIGURACIÓN DE VISTAS DE PRESUPUESTO
            // ═══════════════════════════════════════════════════════════
            modelBuilder.Entity<SOPRO.Core.Entities.ColumnaIndirectos>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.NombreInterno).IsRequired().HasMaxLength(50);
                entity.HasOne(e => e.Proyecto).WithMany()
                    .HasForeignKey(e => e.ProyectoId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.ProyectoId, e.NombreInterno }).IsUnique();
            });

            modelBuilder.Entity<SOPRO.Core.Entities.ColumnaExplosion>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.NombreInterno).IsRequired().HasMaxLength(50);
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.ProyectoId, e.NombreInterno }).IsUnique();
            });

            modelBuilder.Entity<SOPRO.Core.Entities.ColumnaMaterial>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.NombreInterno).IsRequired().HasMaxLength(50);
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.ProyectoId, e.NombreInterno }).IsUnique();
            });

            modelBuilder.Entity<SOPRO.Core.Entities.ColumnaManoObra>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.NombreInterno).IsRequired().HasMaxLength(50);
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.ProyectoId, e.NombreInterno }).IsUnique();
            });

            modelBuilder.Entity<SOPRO.Core.Entities.ColumnaHerramienta>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.NombreInterno).IsRequired().HasMaxLength(50);
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.ProyectoId, e.NombreInterno }).IsUnique();
            });

            modelBuilder.Entity<SOPRO.Core.Entities.ColumnaMaquinaria>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.NombreInterno).IsRequired().HasMaxLength(50);
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.ProyectoId, e.NombreInterno }).IsUnique();
            });

            modelBuilder.Entity<SOPRO.Core.Entities.ColumnaProgramaObra>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.NombreInterno).IsRequired().HasMaxLength(50);
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.ProyectoId, e.NombreInterno }).IsUnique();
            });

            modelBuilder.Entity<SOPRO.Core.Entities.ColumnaProgramaInsumos>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.NombreInterno).IsRequired().HasMaxLength(50);
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.ProyectoId, e.NombreInterno }).IsUnique();
            });

            modelBuilder.Entity<SOPRO.Core.Entities.ColumnaMatriz>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(e => e.NombreInterno).IsRequired().HasMaxLength(50);
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.ProyectoId, e.NombreInterno }).IsUnique();
            });

            modelBuilder.Entity<VistaPresupuesto>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
                
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            
            // ═══════════════════════════════════════════════════════════
            // CONFIGURACIÓN DE INDIRECTOS
            // ═══════════════════════════════════════════════════════════
            modelBuilder.Entity<GrupoIndirecto>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired().HasMaxLength(200);
                
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(e => new { e.ProyectoId, e.Orden });
            });
            
            modelBuilder.Entity<ConceptoIndirecto>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Concepto).IsRequired().HasMaxLength(200);
                entity.Property(e => e.ImporteMensual).HasPrecision(18, 2);
                
                entity.HasOne(e => e.GrupoIndirecto)
                    .WithMany(g => g.Conceptos)
                    .HasForeignKey(e => e.GrupoIndirectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(e => new { e.GrupoIndirectoId, e.Orden });
            });
            
            modelBuilder.Entity<ConfiguracionFinanciamiento>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TasaTIIE).HasPrecision(18, 4);
                entity.Property(e => e.PuntosAdicionales).HasPrecision(18, 4);
                entity.Property(e => e.PorcentajeAnticipo).HasPrecision(18, 4);
                entity.Property(e => e.InteresesNegativos).HasPrecision(18, 4);
                entity.Property(e => e.InteresesPositivos).HasPrecision(18, 4);
                entity.Property(e => e.FinanciamientoNeto).HasPrecision(18, 4);
                entity.Property(e => e.PorcentajeCalculado).HasPrecision(18, 5);
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<FilaFlujoCajaFinanciamiento>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Egresos).HasPrecision(18, 2);
                entity.Property(e => e.AnticipoRecibido).HasPrecision(18, 2);
                entity.Property(e => e.EstimacionCobrada).HasPrecision(18, 2);
                entity.Property(e => e.AmortizacionAnticipo).HasPrecision(18, 2);
                entity.Property(e => e.FlujoNeto).HasPrecision(18, 2);
                entity.Property(e => e.SaldoAcumulado).HasPrecision(18, 2);
                entity.Property(e => e.InteresPeriodo).HasPrecision(18, 4);
                entity.HasOne(e => e.Configuracion)
                    .WithMany(c => c.FilasFlujo)
                    .HasForeignKey(e => e.ConfiguracionFinanciamientoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ConfiguracionIndirectos>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.VolumenAnualObra).HasPrecision(18, 2);
                entity.Property(e => e.CostoDirectoObra).HasPrecision(18, 2);
                entity.Property(e => e.TotalOficinaCentralAnual).HasPrecision(18, 2);
                entity.Property(e => e.TotalCampo).HasPrecision(18, 2);
                entity.Property(e => e.PorcentajeOficinaCentral).HasPrecision(18, 4);
                entity.Property(e => e.PorcentajeCampo).HasPrecision(18, 4);
                
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            
            // ═══════════════════════════════════════════════════════════
            // CONFIGURACIÓN DE REPORTES
            // ═══════════════════════════════════════════════════════════
            modelBuilder.Entity<PlantillaReporte>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.ProyectoId).IsUnique(); // Una plantilla por proyecto
            });

            modelBuilder.Entity<PlantillaReporteElemento>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.PlantillaReporte)
                    .WithMany()
                    .HasForeignKey(e => e.PlantillaReporteId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(e => e.Zona).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Tipo).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Fuente).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ColorTextoHex).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Alineacion).IsRequired().HasMaxLength(30);
                // BLOB y strings opcionales explícitamente nullable
                entity.Property(e => e.ImagenBytes).IsRequired(false);
                entity.Property(e => e.ImagenNombreOrigen).IsRequired(false);
                entity.Property(e => e.ImagenRutaOrigen).IsRequired(false);
                entity.Property(e => e.ImagenMimeType).IsRequired(false);
            });
            
            modelBuilder.Entity<ConfigColumnaReporte>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TipoReporte).IsRequired().HasMaxLength(50);
                entity.Property(e => e.NombreInterno).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Encabezado).HasMaxLength(200);
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.ProyectoId, e.TipoReporte, e.NombreInterno }).IsUnique();
            });

            modelBuilder.Entity<ConfiguracionTituloReporte>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Modulo).IsRequired().HasMaxLength(100);
                entity.Property(e => e.TextoTitulo).IsRequired();
                entity.Property(e => e.NombreFuente).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ColorTexto).IsRequired().HasMaxLength(20);
                entity.HasOne(e => e.Proyecto)
                    .WithMany()
                    .HasForeignKey(e => e.ProyectoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.ProyectoId, e.Modulo }).IsUnique();
            });
        }
    }
}
