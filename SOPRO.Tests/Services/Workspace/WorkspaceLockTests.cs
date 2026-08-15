using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Workspace;

/// <summary>
/// Gate N4-2: candado exclusivo de workspace (.lock + FileShare.None).
/// El conflicto de uso compartido lo impone el sistema operativo, de modo que
/// estos tests son deterministas sin necesidad de procesos auxiliares: dos
/// adquisiciones simultáneas sobre el mismo proyecto SIEMPRE chocan.
/// </summary>
[TestClass]
public class WorkspaceLockTests
{
    private static ProjectLifecycleService CrearServicio()
        => new(new ProjectWorkspaceService(), new ProjectDbContextFactory());

    private static string CrearDbConProyecto()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        using (var contexto = TestDbFactory.CreateContextAt(dbPath))
        {
            contexto.Proyectos.Add(new Proyecto
            {
                Nombre = "Proyecto bloqueo",
                Descripcion = string.Empty,
                Ubicacion = string.Empty,
                Convocante = string.Empty,
                Contratista = string.Empty,
                ApoderadoLegal = string.Empty,
                FechaInicio = new DateTime(2026, 1, 1),
                FechaTermino = new DateTime(2026, 1, 31),
                PlazoEjecucion = 31
            });
            contexto.SaveChanges();
        }

        return dbPath;
    }

    [TestMethod]
    public void Acquire_CreaArchivoLock_JuntoALaBase()
    {
        var dbPath = CrearDbConProyecto();

        using var candado = WorkspaceLock.Acquire(dbPath);

        Assert.AreEqual(dbPath + ".lock", candado.LockFilePath);
        Assert.IsTrue(File.Exists(dbPath + ".lock"), "el archivo .lock debe crearse junto a la base");
    }

    [TestMethod]
    public void Acquire_SegundaAdquisicion_DelMismoProyecto_FallaConBloqueo()
    {
        var dbPath = CrearDbConProyecto();

        using var candado = WorkspaceLock.Acquire(dbPath);

        var ex = Assert.ThrowsException<WorkspaceLockedException>(
            () => WorkspaceLock.Acquire(dbPath));
        StringAssert.Contains(ex.Message, dbPath + ".lock",
            "el error debe identificar el archivo de bloqueo");
    }

    [TestMethod]
    public void Dispose_LiberaElCandado_EliminaElArchivo_YPermiteReAdquirir()
    {
        var dbPath = CrearDbConProyecto();

        using (var candado = WorkspaceLock.Acquire(dbPath))
        {
            Assert.ThrowsException<WorkspaceLockedException>(() => WorkspaceLock.Acquire(dbPath));
        }

        Assert.IsFalse(File.Exists(dbPath + ".lock"), "al liberar el candado se elimina el archivo .lock");

        using var reintento = WorkspaceLock.Acquire(dbPath);
        Assert.IsTrue(File.Exists(dbPath + ".lock"));
    }

    [TestMethod]
    public void Dispose_Doble_EsIdempotente()
    {
        var dbPath = CrearDbConProyecto();

        var candado = WorkspaceLock.Acquire(dbPath);
        candado.Dispose();
        candado.Dispose();

        using var nuevo = WorkspaceLock.Acquire(dbPath);
    }

    [TestMethod]
    public void OpenProject_MientrasOtraSesionAbierta_Rechazada_YReabreAlCerrar()
    {
        var dbPath = CrearDbConProyecto();
        var servicio = CrearServicio();

        var sesion = servicio.OpenProject(dbPath);
        Assert.ThrowsException<WorkspaceLockedException>(() => servicio.OpenProject(dbPath));

        servicio.CloseProjectSession(sesion);

        using var reabierta = servicio.OpenProject(dbPath);
        Assert.AreEqual("Proyecto bloqueo", reabierta.Project.Nombre);
    }

    [TestMethod]
    public void OpenProject_SinProyectoValido_NoDejaCandadoRetenido()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        using (TestDbFactory.CreateContextAt(dbPath))
        {
        }

        var servicio = CrearServicio();

        Assert.ThrowsException<InvalidOperationException>(() => servicio.OpenProject(dbPath));
        Assert.IsFalse(File.Exists(dbPath + ".lock"),
            "una apertura fallida no debe dejar el candado retenido");
    }

    [TestMethod]
    public void CloseProjectSession_SesionNula_NoFalla()
    {
        var servicio = CrearServicio();

        servicio.CloseProjectSession(null);
    }
}