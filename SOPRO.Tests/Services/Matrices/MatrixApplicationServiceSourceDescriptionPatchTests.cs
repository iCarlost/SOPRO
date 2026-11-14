using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Matrices;

/// <summary>
/// Regresión manual de PR #122: la edición de la descripción de un insumo ya no
/// muta la navegación compartida del contexto de la sesión; viaja como parche
/// explícito en el DTO y se aplica en la transacción por operación, antes de la
/// cascada y con comparación optimista contra la descripción original.
/// </summary>
[TestClass]
public class MatrixApplicationServiceSourceDescriptionPatchTests
{
    private const string DescripcionMatrizOriginal = "APU A original";
    private const string DescripcionMatrizEditada = "APU A editada";
    private const string DescripcionMaterialOriginal = "Acero original";
    private const string DescripcionMaterialEditada = "Acero editado";
    private const string DescripcionMaterialAjeno = "Acero de otro proyecto";
    private const string DescripcionAuxiliarOriginal = "Auxiliar original";
    private const string DescripcionAuxiliarEditada = "Auxiliar editado";
    private const string DescripcionPendienteSesion = "Descripción pendiente en la sesión";
    private const decimal MaterialPrecioOriginal = 100m;
    private const decimal MaterialPrecioPendiente = 999m;
    private const decimal MatrizCostoOriginal = 100m;
    private const decimal MatrizCostoEditado = 350m;
    private const decimal AuxiliarCostoOriginal = 50m;

    [TestMethod]
    public async Task SaveAsync_DescripcionDeFuenteYCambioDeCantidad_PersistenJuntosEnUnaSolaGuarda()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);

        var dto = CrearDto(escenario);
        // El mismo parche repetido con baseline y destino iguales se deduplica.
        dto.SourceDescriptionEdits.Add(Patch(
            TipoComponenteMatriz.Material,
            escenario.Material.Id,
            DescripcionMaterialOriginal,
            DescripcionMaterialEditada));
        dto.SourceDescriptionEdits.Add(Patch(
            TipoComponenteMatriz.Material,
            escenario.Material.Id,
            DescripcionMaterialOriginal,
            DescripcionMaterialEditada));

        var result = await MatrixApplicationService.SaveAsync(context, dto, escenario.Matriz);

        Assert.AreEqual(escenario.Matriz.Id, result.MatrixId);
        Assert.IsFalse(result.IsNew);

        var estado = await LeerEstadoAsync(dbPath, escenario);

        Assert.AreEqual(DescripcionMatrizEditada, estado.MatrizDescripcion);
        Assert.AreEqual(2, estado.Componentes.Count, "Debe persistirse el lote reemplazado.");
        Assert.AreEqual(5m, estado.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material).Cantidad);
        Assert.AreEqual(DescripcionMaterialEditada, estado.MaterialDescripcion,
            "La descripción de la fuente debe persistirse con la matriz.");
        Assert.AreEqual(MaterialPrecioOriginal, estado.MaterialPrecio,
            "El parche no debe tocar otras propiedades de la fuente.");
    }

    [TestMethod]
    public async Task SaveAsync_FuenteDeOtroProyecto_RechazaSinEscribirNada()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);

        var dto = CrearDto(escenario);
        dto.SourceDescriptionEdits.Add(Patch(
            TipoComponenteMatriz.Material,
            escenario.MaterialAjeno.Id,
            DescripcionMaterialAjeno,
            "Descripción infiltrada"));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => MatrixApplicationService.SaveAsync(context, dto, escenario.Matriz));

        StringAssert.Contains(ex.Message, "ya no existe en el proyecto");
        await AssertEstadoOriginalAsync(dbPath, escenario);
        Assert.AreEqual(DescripcionMaterialAjeno, (await LeerMaterialAsync(dbPath, escenario.MaterialAjeno.Id)).Descripcion,
            "La fuente del otro proyecto no debe modificarse.");
    }

    [TestMethod]
    public async Task SaveAsync_FuenteInexistente_RechazaSinEscribirNada()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);

        var dto = CrearDto(escenario);
        dto.SourceDescriptionEdits.Add(Patch(
            TipoComponenteMatriz.Material,
            escenario.Material.Id + 10_000,
            DescripcionMaterialOriginal,
            "Descripción inexistente"));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => MatrixApplicationService.SaveAsync(context, dto, escenario.Matriz));

        StringAssert.Contains(ex.Message, "ya no existe en el proyecto");
        await AssertEstadoOriginalAsync(dbPath, escenario);
    }

    [TestMethod]
    public async Task SaveAsync_DescripcionOriginalObsoleta_RechazaSinEscribirNada()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);

        var dto = CrearDto(escenario);
        dto.SourceDescriptionEdits.Add(Patch(
            TipoComponenteMatriz.Material,
            escenario.Material.Id,
            "Baseline obsoleto",
            DescripcionMaterialEditada));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => MatrixApplicationService.SaveAsync(context, dto, escenario.Matriz));

        StringAssert.Contains(ex.Message, "su descripción cambió");
        await AssertEstadoOriginalAsync(dbPath, escenario);
        Assert.AreEqual(DescripcionMaterialOriginal, (await LeerMaterialAsync(dbPath, escenario.Material.Id)).Descripcion);
    }

    [TestMethod]
    public async Task SaveAsync_FalloEnCascadaDespuesDelParche_RevierteDescripcionMatrizYComponentes()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);

        // La matriz, sus componentes y la descripción de la fuente ya se
        // escribieron cuando la cascada intenta actualizar la dependiente.
        context.Database.ExecuteSqlRaw(
            $@"CREATE TRIGGER fail_cascada_tras_parche
               BEFORE UPDATE ON Matrices
               WHEN NEW.Id = {escenario.Dependiente.Id}
               BEGIN
                   SELECT RAISE(ABORT, 'fallo inyectado en la cascada');
               END;");

        var dto = CrearDto(escenario);
        dto.SourceDescriptionEdits.Add(Patch(
            TipoComponenteMatriz.Material,
            escenario.Material.Id,
            DescripcionMaterialOriginal,
            DescripcionMaterialEditada));

        await Assert.ThrowsExactlyAsync<DbUpdateException>(
            () => MatrixApplicationService.SaveAsync(context, dto, escenario.Matriz));

        await AssertEstadoOriginalAsync(dbPath, escenario);
        Assert.AreEqual(DescripcionMaterialOriginal, (await LeerMaterialAsync(dbPath, escenario.Material.Id)).Descripcion,
            "La descripción parcheada debe revertirse junto con la matriz.");
    }

    [TestMethod]
    public async Task SaveAsync_ParcheDeAuxiliar_SoloCambiaLaDescripcionYNoElCostoNiOtrosCampos()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);

        var dto = CrearDto(escenario);
        // El valor llega con espacios: se normaliza igual que la edición de la
        // cuadrícula (Trim) antes de comparar/escribir.
        dto.SourceDescriptionEdits.Add(Patch(
            TipoComponenteMatriz.Auxiliar,
            escenario.Auxiliar.Id,
            DescripcionAuxiliarOriginal,
            $"  {DescripcionAuxiliarEditada}  "));

        var result = await MatrixApplicationService.SaveAsync(context, dto, escenario.Matriz);
        Assert.AreEqual(escenario.Matriz.Id, result.MatrixId);

        await using var verification = new SOPROContext(dbPath);
        var auxiliar = await verification.Matrices.AsNoTracking().SingleAsync(m => m.Id == escenario.Auxiliar.Id);

        Assert.AreEqual(DescripcionAuxiliarEditada, auxiliar.Descripcion);
        Assert.AreEqual(AuxiliarCostoOriginal, auxiliar.CostoDirecto,
            "El parche de descripción no debe alterar el costo del auxiliar.");
        Assert.AreEqual("notas auxiliar", auxiliar.Notas);
        Assert.AreEqual("m2", auxiliar.Unidad);
        Assert.AreEqual(TipoMatriz.Basico, auxiliar.Tipo);
        Assert.AreEqual(DescripcionMatrizEditada, (await verification.Matrices.AsNoTracking()
            .SingleAsync(m => m.Id == escenario.Matriz.Id)).Descripcion,
            "La matriz objetivo debe persistirse en la misma transacción.");
    }

    [TestMethod]
    public async Task SaveAsync_ParcheDeAuxiliarSobreLaPropiaMatriz_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);

        var dto = CrearDto(escenario);
        dto.SourceDescriptionEdits.Add(Patch(
            TipoComponenteMatriz.Auxiliar,
            escenario.Matriz.Id,
            DescripcionMatrizOriginal,
            "Auto edición"));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => MatrixApplicationService.SaveAsync(context, dto, escenario.Matriz));

        StringAssert.Contains(ex.Message, "propia descripción");
        await AssertEstadoOriginalAsync(dbPath, escenario);
    }

    [TestMethod]
    public async Task SaveAsync_DescripcionDeFuentePendienteEnSesion_RechazaSinEscribirYConservaElPendiente()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);

        escenario.Material.Descripcion = DescripcionPendienteSesion;
        context.ChangeTracker.DetectChanges();

        var dto = CrearDto(escenario);
        dto.SourceDescriptionEdits.Add(Patch(
            TipoComponenteMatriz.Material,
            escenario.Material.Id,
            DescripcionMaterialOriginal,
            DescripcionMaterialEditada));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => MatrixApplicationService.SaveAsync(context, dto, escenario.Matriz));

        StringAssert.Contains(ex.Message, "cambios sin guardar");
        StringAssert.Contains(ex.Message, escenario.Material.Id.ToString());

        await AssertEstadoOriginalAsync(dbPath, escenario);
        Assert.AreEqual(EntityState.Modified, context.Entry(escenario.Material).State,
            "El pendiente de la sesión debe seguir rastreado.");
        Assert.AreEqual(DescripcionPendienteSesion, escenario.Material.Descripcion,
            "La guarda no debe mutar la copia de la sesión.");
    }

    [TestMethod]
    public async Task SaveAsync_FuenteEliminadaEnSesion_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);

        context.Materiales.Remove(escenario.Material);
        context.ChangeTracker.DetectChanges();

        var dto = CrearDto(escenario);
        dto.SourceDescriptionEdits.Add(Patch(
            TipoComponenteMatriz.Material,
            escenario.Material.Id,
            DescripcionMaterialOriginal,
            DescripcionMaterialEditada));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => MatrixApplicationService.SaveAsync(context, dto, escenario.Matriz));

        StringAssert.Contains(ex.Message, "cambios sin guardar");
        await AssertEstadoOriginalAsync(dbPath, escenario);
        Assert.AreEqual(EntityState.Deleted, context.Entry(escenario.Material).State,
            "La baja pendiente debe seguir rastreada y sin persistir.");
    }

    [TestMethod]
    public async Task SaveAsync_PropiedadAjenaPendienteEnSesion_NoRechazaYNoSePersiste()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);

        // Modificación de una propiedad distinta de Descripcion: no es conflicto
        // para el parche, debe quedar pendiente y sin guardar.
        escenario.Material.PrecioUnitario = MaterialPrecioPendiente;
        context.ChangeTracker.DetectChanges();

        var dto = CrearDto(escenario);
        dto.SourceDescriptionEdits.Add(Patch(
            TipoComponenteMatriz.Material,
            escenario.Material.Id,
            DescripcionMaterialOriginal,
            DescripcionMaterialEditada));

        var result = await MatrixApplicationService.SaveAsync(context, dto, escenario.Matriz);
        Assert.AreEqual(escenario.Matriz.Id, result.MatrixId);

        var materialPersistido = await LeerMaterialAsync(dbPath, escenario.Material.Id);
        Assert.AreEqual(DescripcionMaterialEditada, materialPersistido.Descripcion);
        Assert.AreEqual(MaterialPrecioOriginal, materialPersistido.PrecioUnitario,
            "El pendiente ajeno de la sesión no debe persistirse.");

        Assert.AreEqual(EntityState.Modified, context.Entry(escenario.Material).State,
            "El pendiente ajeno debe seguir rastreado.");
        Assert.AreEqual(MaterialPrecioPendiente, escenario.Material.PrecioUnitario);
        Assert.AreEqual(DescripcionMaterialOriginal, escenario.Material.Descripcion,
            "El servicio no debe mutar la descripción rastreada por el llamador.");
        Assert.IsTrue(context.ChangeTracker.HasChanges());
    }

    [TestMethod]
    public async Task SaveAsync_ParchesContradictoriosParaLaMismaFuente_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);

        var dto = CrearDto(escenario);
        dto.SourceDescriptionEdits.Add(Patch(
            TipoComponenteMatriz.Material,
            escenario.Material.Id,
            DescripcionMaterialOriginal,
            "Primera versión"));
        dto.SourceDescriptionEdits.Add(Patch(
            TipoComponenteMatriz.Material,
            escenario.Material.Id,
            DescripcionMaterialOriginal,
            "Segunda versión"));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => MatrixApplicationService.SaveAsync(context, dto, escenario.Matriz));

        StringAssert.Contains(ex.Message, "contradictorias");
        await AssertEstadoOriginalAsync(dbPath, escenario);
    }

    // ── Escenario ─────────────────────────────────────────────────────────────

    private sealed record Escenario(
        Proyecto Proyecto,
        Proyecto OtroProyecto,
        Material Material,
        Material MaterialAjeno,
        Matriz Matriz,
        Matriz Auxiliar,
        Matriz Dependiente);

    private static Escenario CrearEscenario(SOPROContext context)
    {
        var proyecto = CrearProyecto("Proyecto origen");
        var otroProyecto = CrearProyecto("Proyecto ajeno");

        var material = new Material
        {
            Clave = "MAT-01",
            Descripcion = DescripcionMaterialOriginal,
            Unidad = "kg",
            PrecioUnitario = MaterialPrecioOriginal,
            Notas = "notas material"
        };

        var materialAjeno = new Material
        {
            Clave = "MAT-02",
            Descripcion = DescripcionMaterialAjeno,
            Unidad = "kg",
            PrecioUnitario = 10m,
            Notas = string.Empty
        };

        context.Proyectos.AddRange(proyecto, otroProyecto);
        context.SaveChanges();

        material.ProyectoId = proyecto.Id;
        materialAjeno.ProyectoId = otroProyecto.Id;
        context.Materiales.AddRange(material, materialAjeno);
        context.SaveChanges();

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-A",
            Descripcion = DescripcionMatrizOriginal,
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = MatrizCostoOriginal,
            Notas = string.Empty
        };
        context.Matrices.Add(matriz);
        context.SaveChanges();

        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Cantidad = 2m,
            Importe = 200m,
            Orden = 1,
            Notas = string.Empty
        });

        var auxiliar = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "BAS-B",
            Descripcion = DescripcionAuxiliarOriginal,
            Unidad = "m2",
            Tipo = TipoMatriz.Basico,
            CostoDirecto = AuxiliarCostoOriginal,
            Notas = "notas auxiliar"
        };
        context.Matrices.Add(auxiliar);

        var dependiente = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-C",
            Descripcion = "Dependiente",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 0m,
            Notas = string.Empty
        };
        dependiente.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = matriz.Id,
            Cantidad = 2m,
            Importe = 0m,
            Orden = 1,
            Notas = string.Empty
        });
        context.Matrices.Add(dependiente);
        context.SaveChanges();

        return new Escenario(proyecto, otroProyecto, material, materialAjeno, matriz, auxiliar, dependiente);
    }

    private static Proyecto CrearProyecto(string nombre) => new Proyecto
    {
        Nombre = nombre,
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
        FechaInicio = new DateTime(2026, 1, 1),
        FechaTermino = new DateTime(2026, 1, 31),
        PlazoEjecucion = 31,
        DecimalesCantidad = 2,
        DecimalesImporte = 2,
        DecimalesPorcentaje = 4
    };

    private static MatrixEditDto CrearDto(Escenario escenario) => new MatrixEditDto
    {
        ProyectoId = escenario.Proyecto.Id,
        Clave = "APU-A",
        Descripcion = DescripcionMatrizEditada,
        Unidad = "m3",
        Tipo = TipoMatriz.APU,
        CostoDirecto = MatrizCostoEditado,
        Componentes =
        {
            new MatrixComponentEditDto
            {
                TipoComponente = TipoComponenteMatriz.Material,
                MaterialId = escenario.Material.Id,
                Cantidad = 5m,
                Importe = 500m,
                Orden = 1,
                Notas = string.Empty
            },
            new MatrixComponentEditDto
            {
                TipoComponente = TipoComponenteMatriz.Auxiliar,
                AuxiliarId = escenario.Auxiliar.Id,
                Cantidad = 1m,
                Importe = AuxiliarCostoOriginal,
                Orden = 2,
                Notas = string.Empty
            }
        }
    };

    private static MatrixSourceDescriptionEditDto Patch(
        TipoComponenteMatriz tipo,
        int sourceId,
        string original,
        string nueva) => new MatrixSourceDescriptionEditDto
    {
        TipoComponente = tipo,
        SourceId = sourceId,
        OriginalDescription = original,
        NewDescription = nueva
    };

    // ── Verificación desde un contexto separado ───────────────────────────────

    private sealed record EstadoPersistido(
        string MatrizDescripcion,
        decimal MatrizCosto,
        IReadOnlyList<ComponenteMatriz> Componentes,
        string MaterialDescripcion,
        decimal MaterialPrecio,
        decimal DependienteCosto);

    private static async Task<EstadoPersistido> LeerEstadoAsync(string dbPath, Escenario escenario)
    {
        await using var verification = new SOPROContext(dbPath);

        var matriz = await verification.Matrices.AsNoTracking().SingleAsync(m => m.Id == escenario.Matriz.Id);
        var material = await verification.Materiales.AsNoTracking().SingleAsync(m => m.Id == escenario.Material.Id);
        var dependiente = await verification.Matrices.AsNoTracking().SingleAsync(m => m.Id == escenario.Dependiente.Id);

        var componentes = await verification.ComponentesMatriz.AsNoTracking()
            .Where(c => c.MatrizId == escenario.Matriz.Id)
            .OrderBy(c => c.Id)
            .ToListAsync();

        return new EstadoPersistido(
            matriz.Descripcion,
            matriz.CostoDirecto,
            componentes,
            material.Descripcion,
            material.PrecioUnitario,
            dependiente.CostoDirecto);
    }

    private static async Task<Material> LeerMaterialAsync(string dbPath, int materialId)
    {
        await using var verification = new SOPROContext(dbPath);
        return await verification.Materiales.AsNoTracking().SingleAsync(m => m.Id == materialId);
    }

    private static async Task AssertEstadoOriginalAsync(string dbPath, Escenario escenario)
    {
        var estado = await LeerEstadoAsync(dbPath, escenario);

        Assert.AreEqual(DescripcionMatrizOriginal, estado.MatrizDescripcion,
            "La edición de la matriz debe revertirse.");
        Assert.AreEqual(MatrizCostoOriginal, estado.MatrizCosto);
        Assert.AreEqual(1, estado.Componentes.Count,
            "Debe conservarse el componente original, no el lote nuevo.");
        Assert.AreEqual(2m, estado.Componentes[0].Cantidad);
        Assert.AreEqual(200m, estado.Componentes[0].Importe);
        Assert.AreEqual(DescripcionMaterialOriginal, estado.MaterialDescripcion,
            "La descripción de la fuente no debe quedar escrita.");
        Assert.AreEqual(MaterialPrecioOriginal, estado.MaterialPrecio);
        Assert.AreEqual(0m, estado.DependienteCosto,
            "La matriz dependiente no debe quedar con el costo cascado.");
    }
}
