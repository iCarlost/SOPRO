using SOPRO.Application.Models;

namespace SOPRO.Application.Services
{
    public class ProjectWorkspaceService
    {
        public string SoproFolder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SOPRO");

        public string ProjectsFolder => Path.Combine(SoproFolder, "Proyectos");

        public string LocalDataFolder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SOPRO");

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

            return Directory.GetFiles(ProjectsFolder, "*.db")
                .OrderByDescending(File.GetLastWriteTime)
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
            return Path.Combine(ProjectsFolder, $"{SanitizeFileName(projectName)}.db");
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

        private static string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }
    }
}
