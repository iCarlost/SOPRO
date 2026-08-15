using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SOPRO.Application.Contracts;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Formulario para gestionar el catálogo maestro global.
    /// Permite editar materiales, mano de obra, maquinaria y matrices compartidas.
    /// </summary>
    public partial class FormCatalogoMaestro : Form
    {
        private SOPROContext _masterContext;
        private TabControl tabControl;
        private Button btnCerrar;
        
        public FormCatalogoMaestro()
        {
            InitializeComponent();
            InicializarMasterContext();
        }
        
        private void InitializeComponent()
        {
            SuspendLayout();
            
            this.Text = "Catálogo Maestro Global";
            this.Size = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            
            // Header
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(63, 81, 181)
            };
            
            var lblTitulo = new Label
            {
                Text = "📚 CATÁLOGO MAESTRO GLOBAL",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 10),
                AutoSize = true
            };
            
            var lblSubtitulo = new Label
            {
                Text = "Gestiona insumos y matrices compartidos entre todos tus proyectos",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(200, 200, 255),
                Location = new Point(20, 45),
                AutoSize = true
            };
            
            panelHeader.Controls.AddRange(new Control[] { lblTitulo, lblSubtitulo });
            
            // TabControl
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F)
            };
            
            // Tabs embebidos
            tabControl.TabPages.Add(CrearTab("Materiales", TipoInsumo.Material));
            tabControl.TabPages.Add(CrearTab("Mano de Obra", TipoInsumo.ManoDeObra));
            tabControl.TabPages.Add(CrearTab("Maquinaria", TipoInsumo.Maquinaria));
            tabControl.TabPages.Add(CrearTabMatrices());
            
            // Botón cerrar
            btnCerrar = new Button
            {
                Text = "✖ Cerrar",
                Dock = DockStyle.Bottom,
                Height = 50,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(240, 240, 240)
            };
            btnCerrar.Click += (s, e) => Close();
            
            Controls.AddRange(new Control[] { tabControl, panelHeader, btnCerrar });
            ResumeLayout(false);
        }
        
        private TabPage CrearTab(string titulo, TipoInsumo tipo)
        {
            var tab = new TabPage(titulo);
            
            // Reutilizar los catálogos existentes pero con contexto maestro
            Form catalogo = tipo switch
            {
                TipoInsumo.Material => new FormCatalogoMateriales(_masterContext, proyectoId: null),
                TipoInsumo.ManoDeObra => new FormCatalogoManoObra(_masterContext, proyectoId: null),
                TipoInsumo.Maquinaria => new FormCatalogoMaquinaria(_masterContext, proyectoId: null),
                _ => null
            };
            
            if (catalogo != null)
            {
                catalogo.TopLevel = false;
                catalogo.FormBorderStyle = FormBorderStyle.None;
                catalogo.Dock = DockStyle.Fill;
                tab.Controls.Add(catalogo);
                catalogo.Show();
            }
            else
            {
                var lbl = new Label
                {
                    Text = $"Catálogo de {titulo} - Próximamente",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 12F)
                };
                tab.Controls.Add(lbl);
            }
            
            return tab;
        }
        
        private TabPage CrearTabMatrices()
        {
            var tab = new TabPage("Matrices (APUs)");
            
            // Reutilizar FormMatrices con contexto maestro
            var formMatrices = new FormMatrices(_masterContext, proyectoId: 0, modoEmbebido: true);
            formMatrices.TopLevel = false;
            formMatrices.FormBorderStyle = FormBorderStyle.None;
            formMatrices.Dock = DockStyle.Fill;
            tab.Controls.Add(formMatrices);
            formMatrices.Show();
            
            return tab;
        }
        
        private void InicializarMasterContext()
        {
            // N4-4: la ruta del maestro vive en Application (WorkspacePaths) y el
            // contexto se abre con la fábrica (una sola forma de construir contextos).
            var masterDbPath = WorkspacePaths.MasterDatabasePath;

            // Crear directorio si no existe
            var dir = Path.GetDirectoryName(masterDbPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Crear base de datos maestra si no existe
            if (!File.Exists(masterDbPath))
            {
                MessageBox.Show(
                    "Se creará un nuevo catálogo maestro en:\n" + masterDbPath,
                    "Nuevo Catálogo Maestro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }

            _masterContext = new ProjectDbContextFactory().Create(masterDbPath);
            _masterContext.Database.EnsureCreated();
            SOPRO.Application.Services.SchemaManager.EnsureCurrentSchema(_masterContext);
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _masterContext?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
