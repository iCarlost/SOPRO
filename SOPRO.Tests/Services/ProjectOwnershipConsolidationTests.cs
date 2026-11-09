using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services;

/// <summary>
/// La propiedad de un insumo la define <see cref="Material.ProyectoId"/>: los
/// flags legacy (GuardarEnMaestro / SaveToMaster) no pueden desasignar ni mover
/// un registro a otro proyecto, las filas globales legadas (ProyectoId null)
/// permanecen intactas y la consolidación nunca reescribe ni elimina datos de
/// otro proyecto.
/// </summary>
[TestClass]
public class ProjectOwnershipConsolidationTests
{
    private static readonly IProjectDbContextFactory Fabrica = new ProjectDbContextFactory();

    private static Proyecto CrearProyecto(SOPROContext context, string nombre)
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
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4
        };

        context.Proyectos.Add(proyecto);
        context.SaveChanges();
        return proyecto;
    }

    private static Material CrearMaterial(SOPROContext context, int proyectoId, string clave, OrigenInsumo origen)
    {
        var material = new Material
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = $"Material {clave}",
            Unidad = "pza",
            PrecioUnitario = 1m,
            Notas = string.Empty,
            Origen = origen
        };

        context.Materiales.Add(material);
        context.SaveChanges();
        return material;
    }

    private static Matriz CrearMatriz(SOPROContext context, int proyectoId, string clave, TipoMatriz tipo)
    {
        var matriz = new Matriz
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = $"Matriz {clave}",
            Unidad = "pza",
            Tipo = tipo,
            Notas = string.Empty
        };

        context.Matrices.Add(matriz);
        context.SaveChanges();
        return matriz;
    }

    private static ProjectSessionInfo CrearSesion(SOPROContext context, int proyectoId, string? masterDatabasePath = null)
    {
        var proyecto = context.Proyectos.AsNoTracking().Single(p => p.Id == proyectoId);
        return ProjectSessionInfo.Create(
            ProjectRef.FromEntity(proyecto),
            context.DatabasePath,
            masterDatabasePath,
            proyecto.DecimalesImporte);
    }

    private static MaterialEditDto DtoMaterial(string clave, int? proyectoId, bool guardarEnMaestro)
    {
        return new MaterialEditDto
        {
            Clave = clave,
            Descripcion = $"Material {clave}",
            Unidad = "pza",
            PrecioUnitario = 5m,
            Notas = string.Empty,
            GuardarEnMaestro = guardarEnMaestro,
            ProyectoId = proyectoId
        };
    }

    // ---------- Guardado: propiedad por ProjectId ----------

    [TestMethod]
    public async Task CatalogItemService_FilaDeProyectoOrigenMaestroConFlag_ConservaProyectoYOrigen()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(context, "Proyecto propio");
        var material = CrearMaterial(context, proyecto.Id, "MAT-IMP", OrigenInsumo.Maestro);

        await CatalogItemService.SaveMaterialAsync(
            context,
            DtoMaterial("MAT-IMP", proyecto.Id, guardarEnMaestro: true),
            material);

        var guardado = context.Materiales.AsNoTracking().Single(m => m.Id == material.Id);
        Assert.AreEqual(proyecto.Id, guardado.ProyectoId,
            "El flag legacy no debe desasignar una fila del proyecto.");
        Assert.AreEqual(OrigenInsumo.Maestro, guardado.Origen);
        Assert.IsFalse(context.Materiales.AsNoTracking().Any(m => m.ProyectoId == null),
            "Un guardado de proyecto no debe crear filas globales.");
    }

    [TestMethod]
    public async Task CatalogItemService_FilaNuevaConFlag_SeCreaEnElProyectoYNoComoFilaGlobal()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(context, "Proyecto propio");

        var resultado = await CatalogItemService.SaveMaterialAsync(
            context,
            DtoMaterial("MAT-NUEVO", proyecto.Id, guardarEnMaestro: true));

        var guardado = context.Materiales.AsNoTracking().Single(m => m.Id == resultado.EntityId);
        Assert.AreEqual(proyecto.Id, guardado.ProyectoId,
            "La ruta de guardado del proyecto no puede crear una fila global (ProyectoId null).");
        Assert.AreEqual(OrigenInsumo.Proyecto, guardado.Origen,
            "Una alta desde el proyecto es propiedad y procedencia del proyecto, aunque el flag legacy esté activo.");
        Assert.IsFalse(context.Materiales.AsNoTracking().Any(m => m.ProyectoId == null));
    }

    [TestMethod]
    public async Task CatalogItemService_FilaDeProyectoOrigenMaestroSinFlag_ConservaOrigenLegadoYProyecto()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(context, "Proyecto propio");
        var material = CrearMaterial(context, proyecto.Id, "MAT-IMP", OrigenInsumo.Maestro);

        await CatalogItemService.SaveMaterialAsync(
            context,
            DtoMaterial("MAT-IMP", proyecto.Id, guardarEnMaestro: false),
            material);

        var guardado = context.Materiales.AsNoTracking().Single(m => m.Id == material.Id);
        Assert.AreEqual(proyecto.Id, guardado.ProyectoId,
            "Editar no debe desasignar ni mover la fila del proyecto.");
        Assert.AreEqual(OrigenInsumo.Maestro, guardado.Origen,
            "Editar conserva la procedencia persistida: el flag legacy no migra el Origen en silencio.");
    }

    [TestMethod]
    public async Task CatalogItemService_FilaGlobalLegada_NoSeAdoptaNiSeMueveAlGuardar()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(context, "Proyecto propio");
        var legado = CrearMaterial(context, proyecto.Id, "MAT-LEGADO", OrigenInsumo.Maestro);
        legado.ProyectoId = null;
        context.SaveChanges();
        context.ChangeTracker.Clear();

        var rastreado = context.Materiales.Single(m => m.Id == legado.Id);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            CatalogItemService.SaveMaterialAsync(
                context,
                DtoMaterial("MAT-LEGADO", proyecto.Id, guardarEnMaestro: false),
                rastreado));

        using var verificacion = new SOPROContext(context.DatabasePath);
        var guardado = verificacion.Materiales.AsNoTracking().Single(m => m.Id == legado.Id);
        Assert.IsNull(guardado.ProyectoId,
            "Una fila global legada no debe adoptar el proyecto del contexto de guardado.");
    }

    [TestMethod]
    public async Task CatalogItemService_FilaGlobalLegadaDesdeMaestro_ConservaProyectoNull()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(context, "Proyecto propio");
        var legado = CrearMaterial(context, proyecto.Id, "MAT-GLOBAL", OrigenInsumo.Maestro);
        legado.ProyectoId = null;
        context.SaveChanges();
        context.ChangeTracker.Clear();

        var rastreado = context.Materiales.Single(m => m.Id == legado.Id);
        await CatalogItemService.SaveMaterialAsync(
            context,
            DtoMaterial("MAT-GLOBAL", proyectoId: null, guardarEnMaestro: true),
            rastreado);

        var guardado = context.Materiales.AsNoTracking().Single(m => m.Id == legado.Id);
        Assert.IsNull(guardado.ProyectoId);
    }

    [TestMethod]
    public async Task SaveMaterial_ProyectoOrigenMaestroConSaveToMaster_NoDesasignaLaFilaLocal()
    {
        var masterPath = TestDbFactory.CreateTempDbPath();
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(context, "Proyecto propio");
        var material = CrearMaterial(context, proyecto.Id, "MAT-IMP", OrigenInsumo.Maestro);
        var session = CrearSesion(context, proyecto.Id, masterPath);

        var resultado = await new SaveMaterial(Fabrica).Execute(session, new SaveMaterialRequest(
            MaterialId: material.Id,
            Clave: "MAT-IMP",
            Descripcion: "Importado editado",
            Unidad: "pza",
            PrecioUnitario: 25m,
            Notas: string.Empty,
            SaveToMaster: true,
            ProjectId: proyecto.Id));

        if (!resultado.IsSuccess) Assert.Fail(resultado.Error!.Message);

        var local = context.Materiales.AsNoTracking().Single(m => m.Id == material.Id);
        Assert.AreEqual(proyecto.Id, local.ProyectoId,
            "Guardar en maestro no debe mover la fila del proyecto a la base maestra.");
        Assert.AreEqual(OrigenInsumo.Maestro, local.Origen);
        Assert.IsNotNull(local.MaterialMaestroId);

        using var maestro = new SOPROContext(masterPath);
        var filaMaestra = maestro.Materiales.AsNoTracking().Single(m => m.Id == local.MaterialMaestroId);
        Assert.IsNull(filaMaestra.ProyectoId);
        Assert.AreEqual(OrigenInsumo.Maestro, filaMaestra.Origen);
    }

    [TestMethod]
    public async Task SaveMaterial_ProyectoOrigenMaestroSinSaveToMaster_ConservaProyecto()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(context, "Proyecto propio");
        var material = CrearMaterial(context, proyecto.Id, "MAT-IMP", OrigenInsumo.Maestro);
        var session = CrearSesion(context, proyecto.Id, TestDbFactory.CreateTempDbPath());

        var resultado = await new SaveMaterial(Fabrica).Execute(session, new SaveMaterialRequest(
            MaterialId: material.Id,
            Clave: "MAT-IMP",
            Descripcion: "Importado editado en el proyecto",
            Unidad: "pza",
            PrecioUnitario: 25m,
            Notas: string.Empty,
            SaveToMaster: false,
            ProjectId: proyecto.Id));

        if (!resultado.IsSuccess) Assert.Fail(resultado.Error!.Message);

        var local = context.Materiales.AsNoTracking().Single(m => m.Id == material.Id);
        Assert.AreEqual(proyecto.Id, local.ProyectoId);
        Assert.AreEqual(25m, local.PrecioUnitario);
        Assert.AreEqual(OrigenInsumo.Maestro, local.Origen,
            "La edición conserva la marca de procedencia legacy de la fila del proyecto.");
    }

    // ---------- Consolidación de insumos ----------

    [TestMethod]
    public void InsumoConsolidation_RechazaMaterialSustitutoVinculadoAMaquinaria()
    {
        using var context = TestDbFactory.CreateContext();
        Proyecto proyecto = CrearProyecto(context, "Proyecto propio");
        Material baseInsumo = CrearMaterial(context, proyecto.Id, "MAT-BASE", OrigenInsumo.Proyecto);
        Material sustituido = CrearMaterial(context, proyecto.Id, "MAT-SUST", OrigenInsumo.Proyecto);
        Maquinaria maquinaria = new()
        {
            ProyectoId = proyecto.Id, Clave = "EQ-SUST", Descripcion = "Equipo que lo consume",
            Notas = string.Empty, MaterialAceiteId = sustituido.Id
        };
        context.Maquinaria.Add(maquinaria);
        context.SaveChanges();

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new InsumoConsolidationService().Consolidar(context, proyecto.Id, ConsolidacionInsumoTipo.Material,
                baseInsumo.Id, new[] { baseInsumo.Id, sustituido.Id }));

        StringAssert.Contains(ex.Message, "EQ-SUST");
        StringAssert.Contains(ex.Message, "Desvincúlelo manualmente");
        Assert.IsTrue(context.Materiales.AsNoTracking().Any(m => m.Id == sustituido.Id));
        Assert.AreEqual(sustituido.Id,
            context.Maquinaria.AsNoTracking().Single(m => m.Id == maquinaria.Id).MaterialAceiteId);
    }

    [TestMethod]
    public void InsumoConsolidation_RechazaManoDeObraSustitutaVinculadaComoOperador()
    {
        using var context = TestDbFactory.CreateContext();
        Proyecto proyecto = CrearProyecto(context, "Proyecto propio");
        ManoDeObra baseInsumo = new ManoDeObra
        {
            ProyectoId = proyecto.Id,
            Clave = "MO-BASE",
            Descripcion = "Operador base",
            Unidad = "jor",
            SalarioBase = 100m,
            FactorSalarioReal = 1.5m,
            SalarioReal = 150m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        };
        ManoDeObra sustituido = new ManoDeObra
        {
            ProyectoId = proyecto.Id,
            Clave = "MO-SUST",
            Descripcion = "Operador sustituido",
            Unidad = "jor",
            SalarioBase = 120m,
            FactorSalarioReal = 1.6m,
            SalarioReal = 192m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto
        };
        context.ManoDeObra.AddRange(baseInsumo, sustituido);
        context.SaveChanges();
        Maquinaria maquinaria = new Maquinaria
        {
            ProyectoId = proyecto.Id,
            Clave = "EQ-OP",
            Descripcion = "Equipo con operador",
            Notas = string.Empty,
            ManoDeObraOperadorId = sustituido.Id
        };
        context.Maquinaria.Add(maquinaria);
        context.SaveChanges();

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new InsumoConsolidationService().Consolidar(
                context,
                proyecto.Id,
                ConsolidacionInsumoTipo.ManoDeObra,
                baseInsumo.Id,
                new[] { baseInsumo.Id, sustituido.Id }));

        StringAssert.Contains(ex.Message, "EQ-OP");
        StringAssert.Contains(ex.Message, "Desvincúlelo manualmente");
        Assert.IsTrue(context.ManoDeObra.AsNoTracking().Any(m => m.Id == sustituido.Id));
        Assert.AreEqual(sustituido.Id,
            context.Maquinaria.AsNoTracking().Single(m => m.Id == maquinaria.Id).ManoDeObraOperadorId);
    }

    [TestMethod]
    public void InsumoConsolidation_ConsolidaInsumosDelProyectoConOrigenMaestro()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(context, "Proyecto propio");
        var baseInsumo = CrearMaterial(context, proyecto.Id, "MAT-BASE", OrigenInsumo.Maestro);
        var sustituido = CrearMaterial(context, proyecto.Id, "MAT-SUST", OrigenInsumo.Maestro);
        var matriz = CrearMatriz(context, proyecto.Id, "APU-001", TipoMatriz.APU);

        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = sustituido.Id,
            Cantidad = 1m,
            Orden = 1
        });
        context.SaveChanges();

        var resultado = new InsumoConsolidationService().Consolidar(
            context,
            proyecto.Id,
            ConsolidacionInsumoTipo.Material,
            baseInsumo.Id,
            new[] { baseInsumo.Id, sustituido.Id });

        Assert.AreEqual(1, resultado.RegistrosConsolidados);
        Assert.AreEqual(1, resultado.ComponentesActualizados);
        Assert.AreEqual(1, resultado.MatricesAfectadas);

        var componente = context.ComponentesMatriz.AsNoTracking().Single(c => c.MatrizId == matriz.Id);
        Assert.AreEqual(baseInsumo.Id, componente.MaterialId);
        Assert.IsFalse(context.Materiales.AsNoTracking().Any(m => m.Id == sustituido.Id));
        Assert.IsTrue(context.Materiales.AsNoTracking().Any(m => m.Id == baseInsumo.Id));
    }

    [TestMethod]
    public void InsumoConsolidation_RechazaReferenciaDesdeMatrizDeOtroProyecto()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(context, "Proyecto destino");
        var otroProyecto = CrearProyecto(context, "Proyecto ajeno");
        var baseInsumo = CrearMaterial(context, proyecto.Id, "MAT-BASE", OrigenInsumo.Proyecto);
        var sustituido = CrearMaterial(context, proyecto.Id, "MAT-SUST", OrigenInsumo.Proyecto);
        var matrizAjena = CrearMatriz(context, otroProyecto.Id, "APU-AJENA", TipoMatriz.APU);

        var componenteAjeno = new ComponenteMatriz
        {
            MatrizId = matrizAjena.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = sustituido.Id,
            Cantidad = 1m,
            Orden = 1
        };
        context.ComponentesMatriz.Add(componenteAjeno);
        context.SaveChanges();

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new InsumoConsolidationService().Consolidar(
                context,
                proyecto.Id,
                ConsolidacionInsumoTipo.Material,
                baseInsumo.Id,
                new[] { baseInsumo.Id, sustituido.Id }));

        StringAssert.Contains(ex.Message, "otro proyecto");
        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.AsNoTracking().Any(m => m.Id == sustituido.Id),
            "El rechazo no debe eliminar el insumo seleccionado.");
        Assert.AreEqual(
            sustituido.Id,
            context.ComponentesMatriz.AsNoTracking().Single(c => c.Id == componenteAjeno.Id).MaterialId,
            "El rechazo no debe reescribir referencias de otro proyecto.");
    }

    [TestMethod]
    public void InsumoConsolidation_RechazaSeleccionConInsumoDeOtroProyecto()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(context, "Proyecto destino");
        var otroProyecto = CrearProyecto(context, "Proyecto ajeno");
        var baseInsumo = CrearMaterial(context, proyecto.Id, "MAT-BASE", OrigenInsumo.Proyecto);
        var ajeno = CrearMaterial(context, otroProyecto.Id, "MAT-AJENO", OrigenInsumo.Proyecto);

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new InsumoConsolidationService().Consolidar(
                context,
                proyecto.Id,
                ConsolidacionInsumoTipo.Material,
                baseInsumo.Id,
                new[] { baseInsumo.Id, ajeno.Id }));

        StringAssert.Contains(ex.Message, "proyecto actual");
        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Materiales.AsNoTracking().Any(m => m.Id == ajeno.Id));
    }

    // ---------- Consolidación de matrices ----------

    [TestMethod]
    public void MatrixConsolidation_ReescribeSoloComponentesDelProyectoYNoTocaOtroProyecto()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(context, "Proyecto destino");
        var otroProyecto = CrearProyecto(context, "Proyecto ajeno");

        var matrizBase = CrearMatriz(context, proyecto.Id, "AUX-BASE", TipoMatriz.Basico);
        var matrizSustituida = CrearMatriz(context, proyecto.Id, "AUX-SUST", TipoMatriz.Basico);
        var matrizPadre = CrearMatriz(context, proyecto.Id, "APU-001", TipoMatriz.APU);

        var materialAjeno = CrearMaterial(context, otroProyecto.Id, "MAT-AJENO", OrigenInsumo.Proyecto);
        var matrizAjena = CrearMatriz(context, otroProyecto.Id, "APU-AJENA", TipoMatriz.APU);

        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matrizPadre.Id,
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = matrizSustituida.Id,
            Cantidad = 1m,
            Orden = 1
        });
        var componenteAjeno = new ComponenteMatriz
        {
            MatrizId = matrizAjena.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = materialAjeno.Id,
            Cantidad = 1m,
            Orden = 1
        };
        context.ComponentesMatriz.Add(componenteAjeno);
        context.SaveChanges();

        var resultado = new MatrixConsolidationService().Consolidar(
            context,
            proyecto.Id,
            TipoMatriz.Basico,
            matrizBase.Id,
            new[] { matrizBase.Id, matrizSustituida.Id });

        Assert.AreEqual(1, resultado.RegistrosConsolidados);

        var componentePropio = context.ComponentesMatriz.AsNoTracking().Single(c => c.MatrizId == matrizPadre.Id);
        Assert.AreEqual(matrizBase.Id, componentePropio.AuxiliarId);
        Assert.IsFalse(context.Matrices.AsNoTracking().Any(m => m.Id == matrizSustituida.Id));
        Assert.IsTrue(context.Matrices.AsNoTracking().Any(m => m.Id == matrizBase.Id));

        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Matrices.AsNoTracking().Any(m => m.Id == matrizAjena.Id),
            "El proyecto ajeno no debe perder matrices.");
        Assert.AreEqual(
            materialAjeno.Id,
            context.ComponentesMatriz.AsNoTracking().Single(c => c.Id == componenteAjeno.Id).MaterialId,
            "El proyecto ajeno no debe ver reescritos sus componentes.");
    }

    [TestMethod]
    public void MatrixConsolidation_RechazaAuxiliarReferenciadaPorMatrizDeOtroProyecto()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(context, "Proyecto destino");
        var otroProyecto = CrearProyecto(context, "Proyecto ajeno");

        var matrizBase = CrearMatriz(context, proyecto.Id, "AUX-BASE", TipoMatriz.Basico);
        var matrizSustituida = CrearMatriz(context, proyecto.Id, "AUX-SUST", TipoMatriz.Basico);
        var matrizAjena = CrearMatriz(context, otroProyecto.Id, "APU-AJENA", TipoMatriz.APU);

        var componenteAjeno = new ComponenteMatriz
        {
            MatrizId = matrizAjena.Id,
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = matrizSustituida.Id,
            Cantidad = 1m,
            Orden = 1
        };
        context.ComponentesMatriz.Add(componenteAjeno);
        context.SaveChanges();

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new MatrixConsolidationService().Consolidar(
                context,
                proyecto.Id,
                TipoMatriz.Basico,
                matrizBase.Id,
                new[] { matrizBase.Id, matrizSustituida.Id }));

        StringAssert.Contains(ex.Message, "otro proyecto");
        context.ChangeTracker.Clear();
        Assert.IsTrue(context.Matrices.AsNoTracking().Any(m => m.Id == matrizSustituida.Id),
            "El rechazo no debe eliminar la matriz seleccionada.");
        Assert.AreEqual(
            matrizSustituida.Id,
            context.ComponentesMatriz.AsNoTracking().Single(c => c.Id == componenteAjeno.Id).AuxiliarId,
            "El rechazo no debe reescribir la referencia del proyecto ajeno.");
    }
}
