using SOPRO.Application.Contracts;
using SOPRO.Core.Entities;
using SOPRO.Data.Factories;
using System;
using System.Collections.Generic;
using System.IO;

namespace SOPRO.Application.Services;

/// <summary>
/// Borrador aislado de una duplicación de proyecto. Mantiene una copia temporal
/// en la carpeta de destino (mismo volumen que el archivo final) y la metadata
/// desacoplada para que el diálogo la edite.
///
/// Dispose sin <see cref="Publish"/> cancela y elimina el temporal, sus sidecars
/// y su candado. Después de publicar, dispose es inofensivo: el clon ya es un
/// archivo de proyecto válido y no se borra aunque la apertura posterior de la UI
/// falle.
/// </summary>
public sealed class ProjectDuplicationDraft : IDisposable
{
    private const int Active = 0;
    private const int Publishing = 1;
    private const int Published = 2;
    private const int Disposed = 3;

    private readonly ProjectWorkspaceService _workspaceService;
    private readonly IProjectDbContextFactory _dbContextFactory;
    private readonly IProjectDraftFileOperations _fileOperations;
    private readonly IWorkspaceLock _draftLock;
    private readonly object _gate = new();

    // Valores del borrador capturados al prepararlo. Son la fuente de verdad
    // para Publish: aunque el llamador edite estas propiedades en el Proyecto
    // expuesto (o pase otra instancia), nunca se aceptan cambios de IVA ni de
    // decimales en la frontera pública.
    private readonly decimal _porcentajeIva;
    private readonly int _decimalesCantidad;
    private readonly int _decimalesImporte;
    private readonly int _decimalesPorcentaje;

    private string? _draftPath;
    private int _state;

    internal ProjectDuplicationDraft(
        ProjectWorkspaceService workspaceService,
        IProjectDbContextFactory dbContextFactory,
        IProjectDraftFileOperations fileOperations,
        string draftPath,
        IWorkspaceLock draftLock,
        Proyecto proyecto)
    {
        _workspaceService = workspaceService;
        _dbContextFactory = dbContextFactory;
        _fileOperations = fileOperations;
        _draftPath = draftPath;
        _draftLock = draftLock;
        Proyecto = proyecto;

        _porcentajeIva = proyecto.PorcentajeIVA;
        _decimalesCantidad = proyecto.DecimalesCantidad;
        _decimalesImporte = proyecto.DecimalesImporte;
        _decimalesPorcentaje = proyecto.DecimalesPorcentaje;
    }

    /// <summary>
    /// Metadata desacoplada del proyecto copiado. El diálogo puede modificarla
    /// sin tocar el origen ni el borrador hasta que se llame a <see cref="Publish"/>.
    /// </summary>
    public Proyecto Proyecto { get; }

    /// <summary>
    /// Confirma el borrador: aplica la metadata editable, cierra el WAL y publica
    /// el archivo canónico <c>.soproproj</c> sin sobrescribir uno existente.
    /// </summary>
    /// <returns>Ruta absoluta del proyecto publicado.</returns>
    /// <exception cref="ArgumentNullException">Falta el proyecto editado.</exception>
    /// <exception cref="ArgumentException">El nombre está vacío.</exception>
    /// <exception cref="InvalidOperationException">
    /// El borrador ya se publicó/canceló, ya existe un proyecto con el nombre
    /// saneado, o el destino está abierto en otra ventana/instancia.
    /// </exception>
    public string Publish(Proyecto editedProject)
    {
        ArgumentNullException.ThrowIfNull(editedProject);

        lock (_gate)
        {
            if (_state != Active)
            {
                throw new InvalidOperationException(
                    "El borrador ya fue publicado o cancelado; no puede volver a usarse.");
            }

            _state = Publishing;
        }

        IWorkspaceLock? destinationLock = null;
        var published = false;
        try
        {
            var name = editedProject.Nombre?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "El nombre del proyecto duplicado es obligatorio.", nameof(editedProject));
            }

            var destinationPath = _workspaceService.BuildProjectDatabasePath(name);
            var sanitizedBase = Path.GetFileNameWithoutExtension(destinationPath);
            if (string.IsNullOrWhiteSpace(sanitizedBase))
            {
                throw new ArgumentException(
                    "El nombre del proyecto duplicado no genera un archivo válido.", nameof(editedProject));
            }

            EnsureDestinationAvailable(destinationPath);

            destinationLock = WorkspaceLock.Acquire(destinationPath);

            // Revalidar dentro del candado: otra ventana pudo crear el destino
            // entre la comprobación inicial y la toma del candado.
            EnsureDestinationAvailable(destinationPath);

            ApplyMetadata(editedProject, _draftPath!);
            ProjectDuplicationService.CheckpointAndClose(_draftPath!);

            // Si algún sidecar temporal no se puede borrar, esto lanza antes del
            // Move: nunca se declara publicada una copia con residuos.
            ProjectDuplicationService.DeleteSidecars(_draftPath!, _fileOperations);

            // El temporal vive en la misma carpeta/volumen que el destino. Move
            // sin overwrite es la frontera de publicación: nunca reemplaza.
            File.Move(_draftPath!, destinationPath, overwrite: false);

            published = true;
            _draftPath = null;

            lock (_gate)
            {
                _state = Published;
            }

            try
            {
                _draftLock.Dispose();
            }
            catch (Exception)
            {
                // El candado temporal solo era necesario durante el borrador.
            }

            return destinationPath;
        }
        catch (Exception original)
        {
            if (!published)
            {
                lock (_gate)
                {
                    _state = Disposed;
                }

                try
                {
                    CleanupDraft();
                }
                catch (ProjectDraftCleanupException cleanup)
                {
                    // Conservar el fallo original, las rutas residuales exactas y,
                    // si la hubo, el error al liberar el candado.
                    throw new ProjectDraftCleanupException(
                        cleanup.ResidualPaths,
                        original,
                        cleanup.LockException);
                }
            }

            throw;
        }
        finally
        {
            try
            {
                destinationLock?.Dispose();
            }
            catch (Exception)
            {
                // El candado ya cumplió su función; no debe invalidar una
                // publicación que ya se movió correctamente.
            }
        }
    }

    public void Dispose()
    {
        bool shouldCleanup;

        lock (_gate)
        {
            // Mientras Publish está en curso, él es dueño de la limpieza.
            if (_state == Publishing || _state == Published)
                return;

            if (_state == Active)
                _state = Disposed;

            // Una limpieza previa pudo fallar: conservar _draftPath permite
            // reintentarla en una segunda llamada a Dispose.
            shouldCleanup = _draftPath is not null;
        }

        if (shouldCleanup)
        {
            CleanupDraft();
        }
    }

    private void CleanupDraft()
    {
        var draftPath = _draftPath;
        if (draftPath is null)
            return;

        IReadOnlyList<string> residual = Array.Empty<string>();
        Exception? cleanupError = null;
        Exception? lockError = null;

        try
        {
            residual = ProjectDuplicationService.TryDeleteDraftFiles(draftPath, _fileOperations);
            if (residual.Count > 0)
                cleanupError = new ProjectDraftCleanupException(residual);
        }
        catch (Exception ex)
        {
            // Una limpieza inesperada tampoco debe ocultar el error del candado
            // ni las rutas que podrían quedar. Se exponen todas las candidatas.
            cleanupError = ex;
            residual = ProjectDuplicationService.GetDraftFileCandidates(draftPath);
        }
        finally
        {
            // El candado SIEMPRE se intenta liberar; su fallo se captura en vez
            // de reemplazar (mascarar) el resultado de la limpieza de archivos.
            try
            {
                _draftLock.Dispose();
            }
            catch (Exception ex)
            {
                lockError = ex;
            }
        }

        // Solo se olvida la ruta cuando no quedan archivos residuales.
        if (residual.Count == 0)
            _draftPath = null;

        if (residual.Count > 0 || cleanupError is not null || lockError is not null)
            throw new ProjectDraftCleanupException(residual, cleanupError, lockError);
    }

    private static void EnsureDestinationAvailable(string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;

        var baseName = Path.GetFileNameWithoutExtension(destinationPath);

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (!ProjectFileExtensions.IsSupportedForOpen(file))
                continue;

            if (string.Equals(
                    Path.GetFileNameWithoutExtension(file),
                    baseName,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Ya existe un proyecto con el nombre '{baseName}'. "
                    + "Elige otro nombre para la copia.");
            }
        }
    }

    private void ApplyMetadata(Proyecto editedProject, string draftPath)
    {
        using var context = _dbContextFactory.Create(draftPath);

        var projects = context.Proyectos
            .Take(2)
            .ToList();

        if (projects.Count != 1)
        {
            throw new InvalidOperationException(
                "El borrador no contiene exactamente un proyecto válido para publicar.");
        }

        var target = projects[0];

        // Solo la metadata editable por FormDatosProyecto. Se preservan Id,
        // claves foráneas, porcentajes no editables, fechas de creación y el
        // resto de tablas del proyecto.
        target.Nombre = editedProject.Nombre.Trim();
        target.Descripcion = editedProject.Descripcion ?? string.Empty;
        target.Ubicacion = editedProject.Ubicacion ?? string.Empty;
        target.Convocante = editedProject.Convocante ?? string.Empty;
        target.Contratista = editedProject.Contratista ?? string.Empty;
        target.ApoderadoLegal = editedProject.ApoderadoLegal ?? string.Empty;
        target.FechaInicio = editedProject.FechaInicio;
        target.FechaTermino = editedProject.FechaTermino;
        target.PlazoEjecucion = editedProject.PlazoEjecucion;

        // IVA y decimales NO son metadata editable en la duplicación: afectan
        // cálculos y deben conservarse exactamente como estaban en el borrador,
        // sin importar lo que el llamador haya puesto en editedProject.
        target.PorcentajeIVA = _porcentajeIva;
        target.DecimalesCantidad = _decimalesCantidad;
        target.DecimalesImporte = _decimalesImporte;
        target.DecimalesPorcentaje = _decimalesPorcentaje;
        target.FechaModificacion = DateTime.Now;

        context.SaveChanges();
    }
}
