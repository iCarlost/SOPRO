using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Services;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormPrincipal : Form
    {
        private readonly ProjectWorkspaceService _workspaceService;
        private readonly ProjectLifecycleService _projectLifecycleService;
        private ProjectSession? _currentSession;

        private SOPROContext? _currentContext => _currentSession?.Context;
        private Proyecto? _currentProject => _currentSession?.Project;

        public FormPrincipal() : this(new ProjectWorkspaceService(), new ProjectLifecycleService(new ProjectWorkspaceService()))
        {
        }

        public FormPrincipal(ProjectWorkspaceService workspaceService, ProjectLifecycleService projectLifecycleService)
        {
            _workspaceService = workspaceService;
            _projectLifecycleService = projectLifecycleService;

            InitializeComponent();
            LoadRecentProjects();
            var version = GetCurrentApplicationVersion();

            lblStatus.Text = $"Versión actual: {version}";
            Shown += async (_, __) => await CheckForUpdatesOnStartupAsync();
        }

        private async Task CheckForUpdatesOnStartupAsync()
        {
            try
            {
                var currentVersion = GetCurrentApplicationVersion();
                var updateService = new GitHubUpdateService();
                var result = await updateService.CheckForUpdateAsync(currentVersion);

                if (!result.CheckSucceeded || !result.HasUpdate)
                    return;

                var userResult = MessageBox.Show(
                    $"Hay una nueva versión disponible de SOPRO.\n\n" +
                    $"Versión instalada: v{currentVersion}\n" +
                    $"Versión disponible: {result.LatestTag}\n\n" +
                    "¿Deseas abrir la descarga del instalador ahora?",
                    "Actualización disponible",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (userResult != DialogResult.Yes)
                    return;

                var url = !string.IsNullOrWhiteSpace(result.InstallerUrl)
                    ? result.InstallerUrl
                    : result.ReleaseUrl;

                if (string.IsNullOrWhiteSpace(url))
                    return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // La verificación de actualizaciones no debe impedir que SOPRO abra.
                // Puede fallar por falta de internet, GitHub no disponible o repositorio privado sin autenticación.
            }
        }

        private static Version GetCurrentApplicationVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version == null
                ? new Version(1, 2, 0)
                : new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
        }

        private void LoadRecentProjects()
        {
            if (dgvRecientes == null) return;

            dgvRecientes.Columns.Clear();
            dgvRecientes.Rows.Clear();

            dgvRecientes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colNombre",
                HeaderText = "Nombre del Proyecto",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true
            });

            dgvRecientes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colFecha",
                HeaderText = "Última Modificación",
                Width = 200,
                ReadOnly = true
            });

            dgvRecientes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colRuta",
                HeaderText = "Ruta",
                Width = 150,
                ReadOnly = true,
                Visible = false
            });

            dgvRecientes.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colEliminar",
                HeaderText = string.Empty,
                Text = "🗑",
                UseColumnTextForButtonValue = true,
                Width = 50
            });

            var recentProjects = _workspaceService.GetRecentProjects();
            foreach (var project in recentProjects)
            {
                dgvRecientes.Rows.Add(
                    project.Name,
                    project.LastModified.ToString("dd/MMM/yyyy HH:mm"),
                    project.FilePath);
            }

            if (dgvRecientes.Rows.Count == 0)
            {
                dgvRecientes.Rows.Add("(No hay proyectos recientes)", string.Empty, string.Empty, string.Empty);
            }
        }

        private void BtnNuevoProyecto_Click(object sender, EventArgs e)
        {
            using var formNuevo = new FormDatosProyecto();
            if (formNuevo.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            try
            {
                // N4-3: nunca sobrescribir una sesión abierta: se cierra la anterior
                // primero (libera contexto y candado de workspace).
                CloseCurrentSession();
                _currentSession = _projectLifecycleService.CreateProject(formNuevo.Proyecto);

                MessageBox.Show(
                    $"Proyecto '{_currentProject?.Nombre}' creado exitosamente.",
                    "Proyecto Creado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                LoadRecentProjects();
                AbrirProyecto(_currentProject!);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al crear el proyecto:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void BtnAbrirProyecto_Click(object sender, EventArgs e)
        {
            using var openDialog = new OpenFileDialog
            {
                Title = "Abrir Proyecto SOPRO",
                Filter = "Proyecto SOPRO (*.db)|*.db",
                InitialDirectory = _workspaceService.ProjectsFolder
            };

            if (openDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            OpenProjectPath(openDialog.FileName);
        }

        private void dgvRecientes_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvRecientes.Rows.Count)
                return;

            var nombreProyecto = dgvRecientes.Rows[e.RowIndex].Cells["colNombre"].Value?.ToString();
            if (string.IsNullOrEmpty(nombreProyecto) || nombreProyecto.Contains("(No hay proyectos"))
                return;

            if (e.ColumnIndex == dgvRecientes.Columns["colEliminar"].Index)
                return;

            var projectPath = dgvRecientes.Rows[e.RowIndex].Cells["colRuta"].Value?.ToString();
            if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath))
                return;

            OpenProjectPath(projectPath);
        }

        private void dgvRecientes_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvRecientes.Rows.Count)
                return;

            if (e.ColumnIndex != dgvRecientes.Columns["colEliminar"].Index)
                return;

            var nombreProyecto = dgvRecientes.Rows[e.RowIndex].Cells["colNombre"].Value?.ToString();
            if (string.IsNullOrEmpty(nombreProyecto) || nombreProyecto.Contains("(No hay proyectos"))
                return;

            var projectPath = dgvRecientes.Rows[e.RowIndex].Cells["colRuta"].Value?.ToString();
            if (string.IsNullOrEmpty(projectPath))
                return;

            var result = MessageBox.Show(
                $"¿Está seguro de eliminar el proyecto '{nombreProyecto}'?\n\nEsta acción no se puede deshacer.",
                "Confirmar Eliminación",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            try
            {
                var formsToClose = System.Windows.Forms.Application.OpenForms.Cast<Form>()
                    .Where(f => f is FormProyecto)
                    .ToList();

                foreach (var form in formsToClose)
                {
                    form.Close();
                    form.Dispose();
                }

                CloseCurrentSession();
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                if (_workspaceService.DeleteProjectFile(projectPath))
                {
                    MessageBox.Show(
                        $"Proyecto '{nombreProyecto}' eliminado exitosamente.",
                        "Eliminado",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                LoadRecentProjects();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al eliminar el proyecto:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void BtnCatalogosMaestros_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "Función de Catálogos Maestros próximamente...",
                "En Desarrollo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void AbrirProyecto(Proyecto proyecto)
        {
            this.Hide();

            var formProyecto = new FormProyecto(_currentContext!, proyecto);
            formProyecto.FormClosed += (s, e) =>
            {
                CloseCurrentSession();
                this.Show();
                LoadRecentProjects();
            };

            formProyecto.Show();
        }

        private void OpenProjectPath(string projectPath)
        {
            try
            {
                // N4-3: nunca sobrescribir una sesión abierta: su candado de workspace
                // bloquearía la nueva apertura. Se cierra la anterior primero.
                CloseCurrentSession();
                _currentSession = _projectLifecycleService.OpenProject(projectPath);
                AbrirProyecto(_currentProject!);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir el proyecto:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void CloseCurrentSession()
        {
            // N4-3: la sesión es IDisposable: libera contexto y candado de workspace.
            // El GC.Collect/WaitForPendingFinalizers previos no eran necesarios y
            // ocultaban errores de disposición.
            _projectLifecycleService.CloseProjectSession(_currentSession);
            _currentSession = null;
        }
    }
}
