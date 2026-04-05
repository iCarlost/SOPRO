using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    internal static class OpenFormsRefreshHelper
    {
        public static void RefrescarPresupuestosAbiertos()
        {
            foreach (Form form in System.Windows.Forms.Application.OpenForms)
            {
                if (form is Forms.FormPresupuesto presupuesto)
                    presupuesto.RefrescarPreciosDesdeDB();
            }
        }

        public static void RefrescarCatalogosMaterialesAbiertos()
        {
            foreach (Form form in System.Windows.Forms.Application.OpenForms)
            {
                if (form is Forms.FormCatalogoMateriales catalogo)
                    catalogo.RecargarCatalogo();
            }
        }

        public static void RefrescarCatalogosManoDeObraAbiertos()
        {
            foreach (Form form in System.Windows.Forms.Application.OpenForms)
            {
                if (form is Forms.FormCatalogoManoObra catalogo)
                    catalogo.RecargarCatalogo();
            }
        }

        public static void RefrescarCatalogosMaquinariaAbiertos()
        {
            foreach (Form form in System.Windows.Forms.Application.OpenForms)
            {
                if (form is Forms.FormCatalogoMaquinaria catalogo)
                    catalogo.RecargarCatalogo();
            }
        }

        public static void RefrescarCatalogosHerramientasAbiertos()
        {
            foreach (Form form in System.Windows.Forms.Application.OpenForms)
            {
                if (form is Forms.FormCatalogoHerramientas catalogo)
                    catalogo.RecargarCatalogo();
            }
        }

        public static void RefrescarCatalogoPorTipo(SOPRO.Core.Entities.TipoComponenteMatriz tipo)
        {
            switch (tipo)
            {
                case SOPRO.Core.Entities.TipoComponenteMatriz.Material:
                    RefrescarCatalogosMaterialesAbiertos();
                    break;
                case SOPRO.Core.Entities.TipoComponenteMatriz.ManoDeObra:
                    RefrescarCatalogosManoDeObraAbiertos();
                    break;
                case SOPRO.Core.Entities.TipoComponenteMatriz.Maquinaria:
                    RefrescarCatalogosMaquinariaAbiertos();
                    break;
                case SOPRO.Core.Entities.TipoComponenteMatriz.Herramienta:
                    RefrescarCatalogosHerramientasAbiertos();
                    break;
            }
        }
    }
}
