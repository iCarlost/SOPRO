using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;
using SOPRO.Application.Models;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
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
        private readonly ProjectDuplicationService _projectDuplicationService;
        private ProjectSession? _currentSession;

        /// <summary>
        /// Ruta de proyecto solicitada al arrancar (shell / doble clic). Se
        /// consume una sola vez desde <see cref="Form.Shown"/>, nunca desde el
        /// constructor, para que la ventana ya exista al abrirla.
        /// </summary>
        private string? _initialProjectPath;

        /// <summary>
        /// Evita repetir la verificación de actualizaciones cuando el arranque
        /// es directo a un proyecto: se lanza antes de que
        /// <see cref="FormPrincipal"/> llegue a mostrarse.
        /// </summary>
        private bool _startupUpdateCheckStarted;

        private SOPROContext? _currentContext => _currentSession?.Context;
        private Proyecto? _currentProject => _currentSession?.Project;

        public FormPrincipal()
            : this(new ProjectWorkspaceService(), new ProjectLifecycleService(new ProjectWorkspaceService()), initialProjectPath: null)
        {
        }

        public FormPrincipal(ProjectWorkspaceService workspaceService, ProjectLifecycleService projectLifecycleService)
            : this(workspaceService, projectLifecycleService, initialProjectPath: null)
        {
        }

        public FormPrincipal(
            ProjectWorkspaceService workspaceService,
            ProjectLifecycleService projectLifecycleService,
            string? initialProjectPath)
        {
            _workspaceService = workspaceService;
            _projectLifecycleService = projectLifecycleService;
            _projectDuplicationService = new ProjectDuplicationService(_workspaceService);
            _initialProjectPath = initialProjectPath;

            InitializeComponent();
            SoproTheme.ApplyFormPrincipal(this);
            dgvRecientes.CellToolTipTextNeeded += DgvRecientes_CellToolTipTextNeeded;
            LoadRecentProjects();
            var version = GetCurrentApplicationVersion();

            lblStatus.Text = $"Versión actual: {version}";
            Shown += async (_, __) =>
            {
                // Fase 2.2: abre primero el proyecto pedido por el shell (si lo
                // hay) y luego verifica actualizaciones, sin cambiar el arranque
                // sin argumentos.
                OpenInitialProjectIfAny();
                await CheckForUpdatesOnStartupAsync();
            };
        }

        /// <summary>
        /// Lanza la verificación de actualizaciones al arrancar sin necesidad de
        /// que <see cref="FormPrincipal"/> se muestre. Se usa en el arranque
        /// directo a un proyecto (shell / doble clic) para conservar la
        /// comprobación de actualizaciones del arranque normal.
        /// </summary>
        public void StartStartupUpdateCheck()
        {
            _ = CheckForUpdatesOnStartupAsync();
        }

        private async Task CheckForUpdatesOnStartupAsync()
        {
            // Una sola comprobación por proceso: en el arranque directo se lanza
            // antes de que el selector se muestre, y al volver a él no debe repetirse.
            if (_startupUpdateCheckStarted)
            {
                return;
            }

            _startupUpdateCheckStarted = true;

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
                // Puede fallar por falta de internet, GitHub no disponible o falta de autenticación.
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
                Name = "colDuplicar",
                HeaderText = string.Empty,
                Text = "Duplicar",
                UseColumnTextForButtonValue = true,
                Width = 50,
                Resizable = DataGridViewTriState.False
            });

            dgvRecientes.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colEliminar",
                HeaderText = string.Empty,
                Text = "Eliminar",
                UseColumnTextForButtonValue = true,
                Width = 50,
                Resizable = DataGridViewTriState.False
            });

            var recentProjects = _workspaceService.GetRecentProjects();
            foreach (var project in recentProjects)
            {
                dgvRecientes.Rows.Add(
                    project.Name,
                    project.LastModified.ToString("dd/MMM/yyyy HH:mm"),
                    project.FilePath,
                    string.Empty,
                    string.Empty);
            }

            if (dgvRecientes.Rows.Count == 0)
            {
                dgvRecientes.Rows.Add("(No hay proyectos recientes)", string.Empty, string.Empty, string.Empty, string.Empty);
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
                Filter = ProjectFileExtensions.BuildFileDialogFilter(),
                InitialDirectory = _workspaceService.ProjectsFolder
            };

            if (openDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            OpenProjectPath(openDialog.FileName);
        }

        /// <summary>
        /// Tooltip accesible en espanol para las columnas de accion del grid de
        /// recientes: los glifos se pintan a mano, sin texto visible. La
        /// etiqueta invisible del boton queda disponible para lectores de pantalla.
        /// </summary>
        private void DgvRecientes_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0)
                return;

            if (EsColumnaAccion(e.ColumnIndex))
                e.ToolTipText = dgvRecientes.Columns[e.ColumnIndex].Name == "colDuplicar"
                    ? "Duplicar proyecto"
                    : "Eliminar proyecto";
        }

        private void dgvRecientes_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvRecientes.Rows.Count)
                return;

            var nombreProyecto = dgvRecientes.Rows[e.RowIndex].Cells["colNombre"].Value?.ToString();
            if (string.IsNullOrEmpty(nombreProyecto) || nombreProyecto.Contains("(No hay proyectos"))
                return;

            if (EsColumnaAccion(e.ColumnIndex))
                return;

            var projectPath = dgvRecientes.Rows[e.RowIndex].Cells["colRuta"].Value?.ToString();
            if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath))
                return;

            OpenProjectPath(projectPath);
        }

        /// <summary>
        /// true si la columna es un boton de accion (duplicar/eliminar) gestionado
        /// por otro manejador: el doble clic no debe abrir el proyecto.
        /// </summary>
        private bool EsColumnaAccion(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= dgvRecientes.Columns.Count)
                return false;

            var nombre = dgvRecientes.Columns[columnIndex].Name;
            return nombre is "colDuplicar" or "colEliminar";
        }

        private void dgvRecientes_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvRecientes.Rows.Count)
                return;

            if (e.ColumnIndex == dgvRecientes.Columns["colDuplicar"].Index)
            {
                var nombreProyectoParaDuplicar = dgvRecientes.Rows[e.RowIndex].Cells["colNombre"].Value?.ToString();
                if (string.IsNullOrEmpty(nombreProyectoParaDuplicar) || nombreProyectoParaDuplicar.Contains("(No hay proyectos"))
                    return;

                var rutaProyecto = dgvRecientes.Rows[e.RowIndex].Cells["colRuta"].Value?.ToString();
                if (!string.IsNullOrWhiteSpace(rutaProyecto))
                    DuplicarProyecto(rutaProyecto);

                return;
            }

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

        private void DuplicarProyecto(string sourceProjectPath)
        {
            string destinationPath;
            try
            {
                using var draft = _projectDuplicationService.PrepareDuplicate(sourceProjectPath);
                draft.Proyecto.Nombre = SugerirNombreCopia(draft.Proyecto.Nombre);

                using var formDatos = new FormDatosProyecto(
                    draft.Proyecto,
                    suppressGlobalFormatUpdates: true);

                if (formDatos.ShowDialog(this) != DialogResult.OK)
                    return;

                destinationPath = draft.Publish(formDatos.Proyecto);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al duplicar el proyecto:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show(
                $"Proyecto '{Path.GetFileNameWithoutExtension(destinationPath)}' duplicado exitosamente.",
                "Proyecto duplicado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            try
            {
                LoadRecentProjects();
                SeleccionarProyectoReciente(destinationPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"El proyecto '{Path.GetFileNameWithoutExtension(destinationPath)}' se creó correctamente en:\n{destinationPath}\n\nNo se pudo actualizar la lista de proyectos recientes:\n{ex.Message}",
                    "Proyecto creado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private string SugerirNombreCopia(string projectName)
        {
            var nombreBase = string.IsNullOrWhiteSpace(projectName)
                ? "Proyecto"
                : projectName.Trim();
            var nombresExistentes = ProjectWorkspaceService
                .EnumerateProjectFiles(_workspaceService.ProjectsFolder)
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .ToArray();

            var numeroCopia = 1;
            while (true)
            {
                var sufijo = numeroCopia == 1
                    ? "(copia)"
                    : $"(copia {numeroCopia})";
                var candidato = $"{nombreBase} {sufijo}";
                var nombreArchivo = Path.GetFileNameWithoutExtension(
                    _workspaceService.BuildProjectDatabasePath(candidato));

                if (!nombresExistentes.Contains(nombreArchivo, StringComparer.OrdinalIgnoreCase))
                    return candidato;

                numeroCopia++;
            }
        }

        private void SeleccionarProyectoReciente(string projectPath)
        {
            dgvRecientes.ClearSelection();

            foreach (DataGridViewRow row in dgvRecientes.Rows)
            {
                var rowPath = row.Cells["colRuta"].Value?.ToString();
                if (!string.Equals(rowPath, projectPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                row.Selected = true;
                dgvRecientes.CurrentCell = row.Cells["colNombre"];
                break;
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

        private void BtnApoyarKoFi_Click(object sender, EventArgs e)
        {
            const string url = "https://ko-fi.com/icarlost";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo abrir la página de apoyo:\n{ex.Message}",
                    "Error al abrir enlace",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private FormProyecto AbrirProyecto(Proyecto proyecto)
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
            return formProyecto;
        }

        /// <summary>
        /// Abre el proyecto recibido por línea de comandos, una única vez y
        /// únicamente desde <see cref="Form.Shown"/>. Ante cualquier fallo
        /// (ruta inexistente, archivo que no es un proyecto SQLite válido o
        /// proyecto bloqueado) muestra el error y deja la ventana utilizable.
        /// </summary>
        private void OpenInitialProjectIfAny()
        {
            var initialPath = _initialProjectPath;
            _initialProjectPath = null;
            TryOpenProject(initialPath);
        }

        /// <summary>
        /// Abre el proyecto indicado y devuelve su formulario, sin depender de
        /// que <see cref="FormPrincipal"/> esté visible. Pensado para el arranque
        /// directo (shell / doble clic): permite abrir el proyecto antes de
        /// mostrar cualquier ventana. Ante cualquier fallo (ruta inexistente,
        /// archivo que no es un proyecto SQLite válido o proyecto bloqueado)
        /// muestra el error recuperable y devuelve <c>null</c>.
        /// </summary>
        public FormProyecto? TryOpenProject(string? projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                return null;
            }

            if (!File.Exists(projectPath))
            {
                MessageBox.Show(
                    $"No se encontró el archivo del proyecto:\n{projectPath}",
                    "Error al abrir el proyecto",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return null;
            }

            return OpenProjectPath(projectPath);
        }

        private FormProyecto? OpenProjectPath(string projectPath)
        {
            try
            {
                // N4-3: nunca sobrescribir una sesión abierta: su candado de workspace
                // bloquearía la nueva apertura. Se cierra la anterior primero.
                CloseCurrentSession();
                _currentSession = _projectLifecycleService.OpenProject(projectPath);
                return AbrirProyecto(_currentProject!);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir el proyecto:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return null;
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
