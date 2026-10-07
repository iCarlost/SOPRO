using SOPRO.Application.Contracts;
using SOPRO.Application.Models;

namespace SOPRO.Application.Services
{
    public class ProjectWorkspaceService
    {
        public string SoproFolder { get; }

        public string ProjectsFolder { get; }

        public string LocalDataFolder { get; }

        /// <summary>
        /// Constructor por defecto: usa la convención de carpetas del workspace
        /// (<see cref="WorkspacePaths"/>). No crea directorios por sí solo.
        /// </summary>
        public ProjectWorkspaceService()
            : this(WorkspacePaths.SoproFolder, WorkspacePaths.LocalDataFolder)
        {
        }

        /// <summary>
        /// Seam de rutas para tests y composición: permite apuntar el workspace a
        /// carpetas temporales sin tocar <c>%USERPROFILE%\Documents\SOPRO</c>.
        /// </summary>
        public ProjectWorkspaceService(string soproFolder, string localDataFolder)
        {
            SoproFolder = ResolveFolder(soproFolder, nameof(soproFolder));
            ProjectsFolder = Path.Combine(SoproFolder, "Proyectos");
            LocalDataFolder = ResolveFolder(localDataFolder, nameof(localDataFolder));
        }

        public void EnsureWorkspaceExists()
        {
            if (!Directory.Exists(SoproFolder))
            {
                Directory.CreateDirectory(SoproFolder);
            }

            if (!Directory.Exists(ProjectsFolder))
            {
                Directory.CreateDirectory(ProjectsFolder);
            }

            if (!Directory.Exists(LocalDataFolder))
            {
                Directory.CreateDirectory(LocalDataFolder);
            }
        }

        public IReadOnlyList<RecentProjectInfo> GetRecentProjects(int take = 10)
        {
            EnsureWorkspaceExists();

            return EnumerateProjectFiles(ProjectsFolder)
                .OrderByDescending(File.GetLastWriteTime)
                .ThenBy(file => file, StringComparer.OrdinalIgnoreCase)
                .Take(take)
                .Select(file => new RecentProjectInfo
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FilePath = file,
                    LastModified = File.GetLastWriteTime(file)
                })
                .ToList();
        }

        public string BuildProjectDatabasePath(string projectName)
        {
            EnsureWorkspaceExists();
            return Path.Combine(
                ProjectsFolder,
                $"{SanitizeFileName(projectName)}{ProjectFileExtensions.Canonical}");
        }

        /// <summary>
        /// Enumera los archivos de proyecto admitidos en <paramref name="folder"/>
        /// (canónica + legacy + transicional), sin duplicados y en orden estable
        /// (por ruta, case-insensitive). La lista de patrones proviene de la fuente
        /// única <see cref="ProjectFileExtensions.GetOpenSearchPatterns"/>.
        /// </summary>
        public static IReadOnlyList<string> EnumerateProjectFiles(string folder)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folder);

            return ProjectFileExtensions.GetOpenSearchPatterns()
                .SelectMany(pattern => Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public bool DeleteProjectFile(string projectPath)
        {
            if (!File.Exists(projectPath))
            {
                return false;
            }

            bool deleted = false;
            int attempts = 0;
            const int maxAttempts = 5;

            while (!deleted && attempts < maxAttempts)
            {
                try
                {
                    File.Delete(projectPath);
                    deleted = true;
                }
                catch (IOException)
                {
                    attempts++;
                    if (attempts >= maxAttempts)
                    {
                        throw;
                    }

                    Thread.Sleep(500);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            return deleted;
        }

        private static string ResolveFolder(string folder, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("La carpeta del workspace no puede estar vacía.", parameterName);
            }

            return Path.GetFullPath(folder.Trim());
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }
    }
}
