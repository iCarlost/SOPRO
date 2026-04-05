using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.IO;

namespace SOPRO.Data.Context
{
    /// <summary>
    /// Factory para crear el DbContext en tiempo de diseño
    /// Necesario para que las migraciones de EF Core funcionen
    /// </summary>
    public class SOPROContextFactory : IDesignTimeDbContextFactory<SOPROContext>
    {
        public SOPROContext CreateDbContext(string[] args)
        {
            // Ruta temporal para las migraciones
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SOPRO");
            
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            
            var dbPath = Path.Combine(folder, "SOPRO_Design.db");
            
            return new SOPROContext(dbPath);
        }
    }
}
