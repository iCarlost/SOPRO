using Microsoft.Data.Sqlite;
using System;

namespace SOPRO.Data.Context;

/// <summary>
/// Operaciones auxiliares para liberar handles SQLite retenidos por el pool
/// después de cerrar contextos de proyecto.
/// </summary>
public static class SqliteConnectionPool
{
    public static void Clear(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            return;

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            SqliteConnection.ClearPool(connection);
        }
        catch (Exception)
        {
            // Liberar el pool es best-effort; no debe impedir el cierre de sesión.
        }
    }
}
