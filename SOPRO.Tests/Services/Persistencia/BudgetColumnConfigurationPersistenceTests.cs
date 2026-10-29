using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Persistencia;

/// <summary>
/// Contrato del servicio de columnas del presupuesto (ADR-0001): contexto NUEVO por
/// operación, una transacción corta y un único SaveChanges. Cubre que ninguna
/// escritura confirme cambios pendientes del contexto compartido, el lote atómico de
/// orden, el bootstrap legacy 17→23 preservando configuración del usuario, el reset
/// atómico con índice único, la reversión íntegra ante trigger y las operaciones del
/// diálogo acotadas al proyecto. Solo SQLite temporal.
/// </summary>
[TestClass]
public class BudgetColumnConfigurationPersistenceTests
{
    [TestMethod]
    public void UpdateWidth_NoConfirmaUnConceptoPendienteDelContextoCompartido()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            int columnaId;

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto ancho").Id;
                conceptoId = AddConcept(seed, proyectoId, 5m).Id;
                columnaId = AddColumn(seed, proyectoId, "Tipo", "Tipo", 120).Id;
            }

            var factory = new CountingFactory();

            // Contexto compartido con un Concepto modificado y SIN guardar: simula la
            // vista con cambios pendientes de otra operación.
            using (var compartido = new SOPROContext(dbPath))
            {
                var concepto = compartido.ConceptosPresupuesto.Single(c => c.Id == conceptoId);
                concepto.Cantidad = 999m;
                Assert.AreEqual(EntityState.Modified, compartido.Entry(concepto).State);

                var resultado = BudgetColumnConfigurationService.UpdateWidth(
                    factory, dbPath, proyectoId, columnaId, 333);

                Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);
                Assert.AreEqual(1, factory.SaveCount, "La operación debe hacer exactamente un SaveChanges.");
                Assert.AreEqual(EntityState.Modified, compartido.Entry(concepto).State,
                    "El servicio no debe guardar ni limpiar cambios del contexto compartido.");
            }

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(333, verificacion.ColumnasPersonalizadas.AsNoTracking().Single(c => c.Id == columnaId).AnchoColumna);
            Assert.AreEqual(5m, verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId).Cantidad,
                "El Concepto pendiente del contexto compartido no debe persistirse.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void UpdateOrder_LoteConColumnaAjena_EsAtomicoYNoEscribeNada()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoAId;
            int proyectoBId;
            int columnaAId;
            int columnaBId;

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoAId = AddProject(seed, "Proyecto A").Id;
                proyectoBId = AddProject(seed, "Proyecto B").Id;
                columnaAId = AddColumn(seed, proyectoAId, "Tipo", "Tipo", 120, orden: 1).Id;
                columnaBId = AddColumn(seed, proyectoBId, "Tipo", "Tipo", 120, orden: 1).Id;
            }

            var factory = new CountingFactory();
            var lote = new[]
            {
                new BudgetColumnOrderEntry(columnaAId, 5),
                new BudgetColumnOrderEntry(columnaBId, 9)
            };

            var resultado = BudgetColumnConfigurationService.UpdateOrder(factory, dbPath, proyectoAId, lote);

            Assert.AreEqual(BudgetOperationStatus.Conflict, resultado.Status, resultado.Message);
            Assert.AreEqual(0, factory.SaveCount, "Un lote inválido no debe escribir nada.");

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(1, verificacion.ColumnasPersonalizadas.AsNoTracking().Single(c => c.Id == columnaAId).Orden);
            Assert.AreEqual(1, verificacion.ColumnasPersonalizadas.AsNoTracking().Single(c => c.Id == columnaBId).Orden);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void UpdateOrder_SinCambiosReales_NoEscribe()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int columnaId;

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto orden").Id;
                columnaId = AddColumn(seed, proyectoId, "Tipo", "Tipo", 120, orden: 3).Id;
            }

            var factory = new CountingFactory();
            var resultado = BudgetColumnConfigurationService.UpdateOrder(
                factory, dbPath, proyectoId, new[] { new BudgetColumnOrderEntry(columnaId, 3) });

            Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);
            Assert.AreEqual(0, factory.SaveCount, "Un no-op no debe hacer SaveChanges.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void EnsureColumns_SetLegacy17_ReemplazaPor23YLaSegundaVezEsNoOp()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto legacy").Id;
                foreach (var (nombre, interno, orden) in Legacy17())
                    AddColumn(seed, proyectoId, nombre, interno, 100, orden: orden);
            }

            var factory = new CountingFactory();

            var primer = BudgetColumnConfigurationService.EnsureColumns(
                factory, dbPath, proyectoId, Definiciones23());

            Assert.AreEqual(BudgetOperationStatus.Committed, primer.Status, primer.Message);
            Assert.AreEqual(23, primer.ConfirmedColumns.Count);
            Assert.AreEqual(23, primer.ConfirmedColumns.Select(c => c.NombreInterno).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.AreEqual(1, factory.SaveCount);

            var segundo = BudgetColumnConfigurationService.EnsureColumns(
                factory, dbPath, proyectoId, Definiciones23());

            Assert.AreEqual(BudgetOperationStatus.Committed, segundo.Status, segundo.Message);
            Assert.AreEqual(1, factory.SaveCount, "El aseguramiento ya completo no debe escribir.");
            CollectionAssert.AreEquivalent(
                primer.ConfirmedColumns.Select(c => c.Id).ToArray(),
                segundo.ConfirmedColumns.Select(c => c.Id).ToArray());
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void EnsureColumns_SoloExtraLegacyNoOficial_EliminaYConfirmaConSaveChanges()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int otroProyectoId;

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto con extra legacy").Id;
                otroProyectoId = AddProject(seed, "Proyecto ajeno").Id;

                foreach (var definicion in Definiciones23())
                {
                    AddColumn(seed, proyectoId, definicion.Nombre, definicion.NombreInterno,
                        definicion.AnchoColumna, orden: definicion.Orden);
                    AddColumn(seed, otroProyectoId, definicion.Nombre, definicion.NombreInterno,
                        definicion.AnchoColumna, orden: definicion.Orden);
                }

                // Extra legacy no oficial en AMBOS proyectos: el borrado del proyecto
                // asegurado es DML inmediato (ExecuteDelete) y debe confirmarse.
                AddColumn(seed, proyectoId, "Importe Final", "ImporteFinal", 140, orden: 30);
                AddColumn(seed, otroProyectoId, "Importe Final", "ImporteFinal", 140, orden: 30);
            }

            var factory = new CountingFactory();
            var resultado = BudgetColumnConfigurationService.EnsureColumns(
                factory, dbPath, proyectoId, Definiciones23());

            Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);
            Assert.AreEqual(1, factory.SaveCount,
                "Una ruta con DML (borrado del extra legacy) debe pasar por SaveChanges, no por NoChanges.");
            Assert.AreEqual(23, resultado.ConfirmedColumns.Count);
            Assert.IsFalse(resultado.ConfirmedColumns.Any(c =>
                    string.Equals(c.NombreInterno, "ImporteFinal", StringComparison.OrdinalIgnoreCase)),
                "La instantánea confirmada no debe incluir el extra legacy eliminado.");

            using var verificacion = new SOPROContext(dbPath);
            var delProyecto = verificacion.ColumnasPersonalizadas.AsNoTracking()
                .Where(c => c.ProyectoId == proyectoId)
                .ToList();

            Assert.AreEqual(23, delProyecto.Count);
            Assert.IsFalse(delProyecto.Any(c =>
                    string.Equals(c.NombreInterno, "ImporteFinal", StringComparison.OrdinalIgnoreCase)),
                "El extra legacy no oficial debe quedar eliminado en la base.");

            var delOtroProyecto = verificacion.ColumnasPersonalizadas.AsNoTracking()
                .Where(c => c.ProyectoId == otroProyectoId)
                .ToList();

            Assert.AreEqual(24, delOtroProyecto.Count,
                "El aseguramiento no debe tocar las columnas de otro proyecto.");
            Assert.IsTrue(delOtroProyecto.Any(c =>
                    string.Equals(c.NombreInterno, "ImporteFinal", StringComparison.OrdinalIgnoreCase)),
                "El extra legacy de otro proyecto no debe eliminarse.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void EnsureColumns_ConfiguracionParcial_PreservaAnchoOrdenFormatoYAgregaFaltantes()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto parcial").Id;

                var tipo = AddColumn(seed, proyectoId, "Tipo", "Tipo", 222, orden: 50);
                tipo.NombreFuente = "Arial";
                tipo.Cursiva = true;
                tipo.ColorFondo = "#FFEEDD";

                AddColumn(seed, proyectoId, "Clave", "Clave", 100, orden: -6);
                AddColumn(seed, proyectoId, "Mi columna", "Col_MiColumna", 150, orden: 70);
                seed.SaveChanges();
            }

            var factory = new CountingFactory();
            var resultado = BudgetColumnConfigurationService.EnsureColumns(
                factory, dbPath, proyectoId, Definiciones23());

            Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);
            Assert.AreEqual(24, resultado.ConfirmedColumns.Count, "23 oficiales + 1 personalizada del usuario.");

            var tipoConfirmada = resultado.ConfirmedColumns.Single(c => c.NombreInterno == "Tipo");
            Assert.AreEqual(222, tipoConfirmada.AnchoColumna);
            Assert.AreEqual(50, tipoConfirmada.Orden);
            Assert.AreEqual("Arial", tipoConfirmada.NombreFuente);
            Assert.IsTrue(tipoConfirmada.Cursiva);
            Assert.AreEqual("#FFEEDD", tipoConfirmada.ColorFondo);

            var personalizada = resultado.ConfirmedColumns.Single(c => c.NombreInterno == "Col_MiColumna");
            Assert.AreEqual(150, personalizada.AnchoColumna);
            Assert.AreEqual(70, personalizada.Orden);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ResetToDefaults_RecreaLas23AtomicamenteConNombresInternosUnicos()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto reset").Id;
                AddColumn(seed, proyectoId, "Solo mía", "Col_SoloMia", 150, orden: 1);
                AddColumn(seed, proyectoId, "Tipo", "Tipo", 40, orden: 2);
            }

            var factory = new CountingFactory();
            var resultado = BudgetColumnConfigurationService.ResetToDefaults(
                factory, dbPath, proyectoId, Definiciones23());

            Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);
            Assert.AreEqual(1, factory.SaveCount);

            using var verificacion = new SOPROContext(dbPath);
            var columnas = verificacion.ColumnasPersonalizadas.AsNoTracking()
                .Where(c => c.ProyectoId == proyectoId)
                .ToList();

            Assert.AreEqual(23, columnas.Count);
            Assert.AreEqual(23, columnas.Select(c => c.NombreInterno).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.IsFalse(columnas.Any(c => c.NombreInterno == "Col_SoloMia"));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ResetToDefaults_TriggerQueAborta_NoDejaCambiosParcialesNiTocaOtrasEntidades()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;
            int columnaId;

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto trigger").Id;
                conceptoId = AddConcept(seed, proyectoId, 7m).Id;
                columnaId = AddColumn(seed, proyectoId, "Tipo", "Tipo", 120).Id;

                seed.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER abortar_columnas BEFORE INSERT ON ColumnasPersonalizadas " +
                    "BEGIN SELECT RAISE(ABORT, 'prueba de rollback'); END;");
            }

            var resultado = BudgetColumnConfigurationService.ResetToDefaults(
                new ProjectDbContextFactory(), dbPath, proyectoId, Definiciones23());

            Assert.AreNotEqual(BudgetOperationStatus.Committed, resultado.Status);
            Assert.IsNotNull(resultado.Diagnostic);
            Assert.AreEqual(0, resultado.ConfirmedColumns.Count);

            using var verificacion = new SOPROContext(dbPath);
            var columnas = verificacion.ColumnasPersonalizadas.AsNoTracking()
                .Where(c => c.ProyectoId == proyectoId)
                .ToList();

            Assert.AreEqual(1, columnas.Count, "El delete del reset debe revertirse junto con el insert fallido.");
            Assert.AreEqual(columnaId, columnas[0].Id);
            Assert.AreEqual("Tipo", columnas[0].NombreInterno);
            Assert.AreEqual(7m, verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == conceptoId).Cantidad);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void OperacionesDelDialogo_EscribenSoloElProyectoYUnSavePorOperacion()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoAId;
            int proyectoBId;
            int columnaAId;
            int columnaBId;
            int originalBOrden;
            string originalBFuente;

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoAId = AddProject(seed, "Proyecto A").Id;
                proyectoBId = AddProject(seed, "Proyecto B").Id;
                columnaAId = AddColumn(seed, proyectoAId, "Tipo", "Tipo", 120, orden: 1).Id;
                var columnaBSeed = AddColumn(seed, proyectoBId, "Tipo", "Tipo", 90, orden: 1);
                columnaBId = columnaBSeed.Id;
                originalBOrden = columnaBSeed.Orden;
                originalBFuente = columnaBSeed.NombreFuente;
            }

            var factory = new CountingFactory();

            var alta = BudgetColumnConfigurationService.AddColumn(
                factory, dbPath, proyectoAId, new BudgetColumnDefinitionDto
                {
                    Nombre = "Nueva personal",
                    TipoColumna = TipoColumnaPersonalizada.Personal,
                    TipoDato = TipoDatoColumna.Texto,
                    AnchoColumna = 110,
                    Visible = true
                });
            Assert.AreEqual(BudgetOperationStatus.Committed, alta.Status, alta.Message);
            var nueva = alta.ConfirmedColumns.Single(c => c.Nombre == "Nueva personal");

            var renombrar = BudgetColumnConfigurationService.RenameColumn(
                factory, dbPath, proyectoAId, nueva.Id, "Renombrada");
            Assert.AreEqual(BudgetOperationStatus.Committed, renombrar.Status, renombrar.Message);

            var visibilidad = BudgetColumnConfigurationService.SetColumnVisibility(
                factory, dbPath, proyectoAId, nueva.Id, visible: false);
            Assert.AreEqual(BudgetOperationStatus.Committed, visibilidad.Status, visibilidad.Message);

            var formato = BudgetColumnConfigurationService.ApplyColumnFormat(
                factory, dbPath, proyectoAId, nueva.Id, new BudgetColumnFormatDto
                {
                    NombreFuente = "Arial",
                    TamanoFuente = 11,
                    Negrita = true,
                    Cursiva = true,
                    Alineacion = AlineacionColumna.Centro,
                    ColorFondo = "#112233",
                    ColorFuente = "#FFFFFF",
                    WrapTexto = true,
                    AlineacionVertical = 2
                });
            Assert.AreEqual(BudgetOperationStatus.Committed, formato.Status, formato.Message);

            var global = BudgetColumnConfigurationService.ApplyGlobalColumnFormat(
                factory, dbPath, proyectoAId,
                new BudgetColumnFormatDto
                {
                    NombreFuente = "Verdana",
                    TamanoFuente = 10,
                    Negrita = false,
                    Cursiva = false,
                    Alineacion = AlineacionColumna.Derecha,
                    ColorFondo = "#000000",
                    ColorFuente = "#00FF00",
                    WrapTexto = false,
                    AlineacionVertical = 1
                },
                BudgetGlobalColumnFormatScope.Full);
            Assert.AreEqual(BudgetOperationStatus.Committed, global.Status, global.Message);

            var eliminar = BudgetColumnConfigurationService.DeleteColumn(
                factory, dbPath, proyectoAId, nueva.Id);
            Assert.AreEqual(BudgetOperationStatus.Committed, eliminar.Status, eliminar.Message);

            Assert.AreEqual(6, factory.SaveCount, "Cada operación confirmada debe hacer un único SaveChanges.");

            using var verificacion = new SOPROContext(dbPath);
            var columnaA = verificacion.ColumnasPersonalizadas.AsNoTracking().Single(c => c.Id == columnaAId);
            Assert.AreEqual("Verdana", columnaA.NombreFuente);
            Assert.AreEqual(AlineacionColumna.Derecha, columnaA.Alineacion);
            Assert.AreEqual("#00FF00", columnaA.ColorFuente);
            Assert.AreEqual("#FFFFFF", columnaA.ColorFondo, "El formato global no debe copiar el color de fondo.");

            var columnaB = verificacion.ColumnasPersonalizadas.AsNoTracking().Single(c => c.Id == columnaBId);
            Assert.AreEqual(originalBOrden, columnaB.Orden);
            Assert.AreEqual(originalBFuente, columnaB.NombreFuente);
            Assert.AreEqual(90, columnaB.AnchoColumna);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void AddColumn_NombreNormalizadoDuplicado_DevuelveConflictoSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto duplicado").Id;
                AddColumn(seed, proyectoId, "Observaciones", "Observaciones", 120);
            }

            var factory = new CountingFactory();
            var resultado = BudgetColumnConfigurationService.AddColumn(
                factory, dbPath, proyectoId, new BudgetColumnDefinitionDto
                {
                    Nombre = "  observaciones ",
                    AnchoColumna = 100
                });

            Assert.AreEqual(BudgetOperationStatus.Conflict, resultado.Status, resultado.Message);
            Assert.AreEqual(0, factory.SaveCount);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    private static Proyecto AddProject(SOPROContext context, string nombre)
    {
        var proyecto = new Proyecto
        {
            Nombre = nombre,
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 12, 31),
            PlazoEjecucion = 365
        };

        context.Proyectos.Add(proyecto);
        context.SaveChanges();
        return proyecto;
    }

    private static ConceptoPresupuesto AddConcept(SOPROContext context, int proyectoId, decimal cantidad)
    {
        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = "CON-1",
            Descripcion = "Concepto de prueba",
            Unidad = "m2",
            Cantidad = cantidad,
            CostoDirectoUnitario = 10m,
            CostoDirectoTotal = cantidad * 10m,
            PrecioUnitario = 10m,
            ImporteTotal = cantidad * 10m,
            Nivel = 1,
            Orden = 1,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };

        context.ConceptosPresupuesto.Add(concepto);
        context.SaveChanges();
        return concepto;
    }

    private static ColumnaPersonalizada AddColumn(
        SOPROContext context,
        int proyectoId,
        string nombre,
        string nombreInterno,
        int ancho,
        int orden = 0)
    {
        var columna = new ColumnaPersonalizada
        {
            ProyectoId = proyectoId,
            Nombre = nombre,
            NombreInterno = nombreInterno,
            TipoColumna = TipoColumnaPersonalizada.Personal,
            TipoDato = TipoDatoColumna.Texto,
            AnchoColumna = ancho,
            Visible = true,
            Orden = orden,
            Formula = string.Empty,
            FormatoNumerico = string.Empty,
            FormatoFecha = string.Empty,
            CondicionTotalizacion = string.Empty
        };

        context.ColumnasPersonalizadas.Add(columna);
        context.SaveChanges();
        return columna;
    }

    private static IReadOnlyList<(string Nombre, string Interno, int Orden)> Legacy17() => new[]
    {
        ("Tipo", "Tipo", 1),
        ("Clave", "Clave", 2),
        ("Descripción", "Descripcion", 3),
        ("Unidad", "Unidad", 4),
        ("Cantidad", "Cantidad", 5),
        ("P.U.", "PrecioUnitario", 6),
        ("Importe", "Importe", 7),
        ("% Indirectos", "PorcentajeIndirectos", 8),
        ("Indirectos", "Indirectos", 9),
        ("% Financiamiento", "PorcentajeFinanciamiento", 10),
        ("Financiamiento", "Financiamiento", 11),
        ("% Utilidad", "PorcentajeUtilidad", 12),
        ("Utilidad", "Utilidad", 13),
        ("Cargos Adicionales", "CargosAdicionales", 14),
        ("P.U. Final", "PrecioUnitarioFinal", 15),
        ("% Cargos Adicionales", "PorcentajeCargosAdicionales", 16),
        ("Importe Final", "ImporteFinal", 17)
    };

    private static IReadOnlyList<BudgetColumnDefinitionDto> Definiciones23()
    {
        var nombres = new[]
        {
            ("Tipo", "Tipo", -7),
            ("Clave", "Clave", -6),
            ("Descripción", "Descripcion", -5),
            ("Unidad", "Unidad", -4),
            ("Cantidad", "Cantidad", -3),
            ("P.U.", "PrecioUnitario", -2),
            ("Importe", "Importe", -1),
            ("% Indirectos", "PorcentajeIndirectos", 1),
            ("Indirectos", "Indirectos", 2),
            ("% Financiamiento", "PorcentajeFinanciamiento", 3),
            ("Financiamiento", "Financiamiento", 4),
            ("% Utilidad", "PorcentajeUtilidad", 5),
            ("Utilidad", "Utilidad", 6),
            ("Cargos Adicionales", "CargosAdicionales", 7),
            ("P.U. Final", "PrecioUnitarioFinal", 8),
            ("Subtotal", "Subtotal", 9),
            ("IVA", "IVA", 10),
            ("Total", "Total", 11),
            ("Incidencia %", "IncidenciaPorcentaje", 12),
            ("Observaciones", "Observaciones", 13),
            ("Importe M.O.", "ImporteManoObra", 14),
            ("P.U. en Letra", "PrecioUnitarioLetra", 15),
            ("Total en Letra", "TotalLetra", 16)
        };

        return nombres
            .Select(n => new BudgetColumnDefinitionDto
            {
                Nombre = n.Item1,
                NombreInterno = n.Item2,
                Orden = n.Item3,
                AnchoColumna = 100,
                Visible = n.Item3 < 0
            })
            .ToArray();
    }

    private static void DeleteQuietly(string dbPath)
    {
        try { File.Delete(dbPath); } catch { }
    }

    /// <summary>Fábrica que cuenta SaveChanges en todos los contextos que crea.</summary>
    private sealed class CountingFactory : IProjectDbContextFactory
    {
        public int SaveCount { get; private set; }

        public SOPROContext Create(string databasePath) => new CountingContext(databasePath, this);

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));

        private sealed class CountingContext : SOPROContext
        {
            private readonly CountingFactory _owner;

            public CountingContext(string databasePath, CountingFactory owner) : base(databasePath)
            {
                _owner = owner;
            }

            public override int SaveChanges(bool acceptAllChangesOnSuccess)
            {
                _owner.SaveCount++;
                return base.SaveChanges(acceptAllChangesOnSuccess);
            }
        }
    }
}
