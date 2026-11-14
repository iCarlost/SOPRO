using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Workspace;

/// <summary>
/// Issue #121 (lane B): <see cref="ProjectLifecycleService.OpenProject"/> valida el
/// archivo ANTES de crear contexto de escritura o migrar. Una ruta inexistente, un
/// SQLite ajeno, uno vacío o una tabla <c>Proyectos</c> incompleta deben rechazarse
/// sin crear archivos, sin alterar el hash del archivo y sin retener el candado.
/// </summary>
[TestClass]
public sealed class ProjectLifecyclePreflightTests
{
    private static ProjectLifecycleService CrearServicio()
        => new(new ProjectWorkspaceService(), new ProjectDbContextFactory());

    private static string CrearRutaTemporal(string extension = ".soproproj")
        => Path.Combine(Path.GetTempPath(), $"sopro_preflight_{Guid.NewGuid():N}{extension}");

    private static string HashSha256(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static void Ejecutar(string dbPath, string sql)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static Proyecto CrearProyecto(string nombre) => new()
    {
        Nombre = nombre,
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
        FechaInicio = new DateTime(2026, 1, 1),
        FechaTermino = new DateTime(2026, 1, 31),
        PlazoEjecucion = 31
    };

    private static readonly Type[] EntidadesConTablaCore =
    {
        typeof(Proyecto),
        typeof(Material),
        typeof(ManoDeObra),
        typeof(Maquinaria),
        typeof(Matriz),
        typeof(ComponenteMatriz),
        typeof(ConceptoPresupuesto),
        typeof(ColumnaPersonalizada)
    };

    /// <summary>
    /// Columnas mapeadas que solo existen por las migraciones M0xx: no forman
    /// parte del esquema core previo a M001. Deben omitirse para reconstruirlo.
    /// </summary>
    private static readonly (string Tabla, string Columna)[] ColumnasAgregadasPorMigraciones =
    {
        // M002
        ("Proyectos", "ModoCalculoPorcentajes"),
        ("Proyectos", "DecimalesCantidad"),
        ("Proyectos", "DecimalesImporte"),
        ("Proyectos", "DecimalesPorcentaje"),
        ("Proyectos", "ParametrosFSR"),
        ("Proyectos", "BarraLateralColapsada"),
        ("Proyectos", "NodosMenuExpandidos"),
        ("Proyectos", "PorcentajeCargosAdicionales"),
        // M001 y M004
        ("ComponentesMatriz", "HerramientaId"),
        ("ComponentesMatriz", "Rendimiento"),
        // M007
        ("ColumnasPersonalizadas", "WrapTexto"),
        ("ColumnasPersonalizadas", "AlineacionVertical"),
        // (M004 también agrega ColumnasPersonalizadas.Modulo, pero no está mapeada
        // en la entidad: queda fuera de la reconstrucción por construcción.)
        // M008 y M015
        ("Maquinaria", "TipoCombustible"),
        ("Maquinaria", "NumeroLlantas"),
        ("Maquinaria", "EsCostoCalculado"),
        ("Maquinaria", "FechaCalculoCosto"),
        ("Maquinaria", "MaterialCombustibleId"),
        ("Maquinaria", "MaterialAceiteId"),
        ("Maquinaria", "ManoDeObraOperadorId"),
        // M009
        ("ConceptosPresupuesto", "Nivel"),
        ("ConceptosPresupuesto", "CargosAdicionales"),
        ("ConceptosPresupuesto", "ColumnasPersonalizadasJSON"),
        ("ConceptosPresupuesto", "Notas"),
        ("ConceptosPresupuesto", "FechaCreacion"),
        ("ConceptosPresupuesto", "FechaModificacion")
    };

    /// <summary>
    /// Crea las ocho tablas core tomando los nombres de columna del modelo EF
    /// vigente, omitiendo las indicadas y declarando la clave como
    /// <c>INTEGER PRIMARY KEY</c>. Devuelve el total de columnas creadas.
    /// </summary>
    private static int CrearEsquemaDesdeModelo(
        string dbPath, IReadOnlyCollection<(string Tabla, string Columna)> columnasOmitidas)
    {
        using var modelo = new ProjectDbContextFactory().Create(dbPath);
        var totalColumnas = 0;

        foreach (var tipo in EntidadesConTablaCore)
        {
            var entidad = modelo.Model.FindEntityType(tipo)
                ?? throw new InvalidOperationException($"El modelo EF no contiene {tipo.Name}.");
            var tabla = entidad.GetTableName()
                ?? throw new InvalidOperationException($"{tipo.Name} no tiene tabla mapeada.");
            var clave = entidad.FindPrimaryKey();
            var columnaClave = clave is { Properties.Count: 1 }
                ? clave.Properties[0].GetColumnName()
                : null;

            var definiciones = entidad.GetProperties()
                .Select(p => p.GetColumnName())
                .Where(nombre => nombre is not null)
                .Where(nombre => !columnasOmitidas.Any(omitida =>
                    omitida.Tabla == tabla
                    && string.Equals(omitida.Columna, nombre, StringComparison.OrdinalIgnoreCase)))
                .Select(nombre => string.Equals(nombre, columnaClave, StringComparison.OrdinalIgnoreCase)
                    ? $"\"{nombre}\" INTEGER PRIMARY KEY"
                    : $"\"{nombre}\"")
                .ToList();

            totalColumnas += definiciones.Count;
            Ejecutar(dbPath, $"CREATE TABLE \"{tabla}\" ({string.Join(", ", definiciones)});");
        }

        return totalColumnas;
    }

    /// <summary>
    /// Construye un SQLite ajeno con las ocho tablas del modelo core SOPRO y sin
    /// <c>__MigracionesCustom</c>, omitiendo solo las columnas indicadas: si el
    /// preflight lo rechaza, es por la columna ausente y no por otra carencia.
    /// </summary>
    private static void CrearSqliteAjenoConTablasCore(
        string dbPath, params (string Tabla, string Columna)[] columnasOmitidas)
    {
        CrearEsquemaDesdeModelo(dbPath, columnasOmitidas);
        Ejecutar(dbPath, "INSERT INTO \"Proyectos\" DEFAULT VALUES;");
    }

    /// <summary>
    /// Reconstrucción sintética del esquema core previo a M001 (no existe un
    /// fixture real de esa época): las ocho tablas con las 137 columnas base y
    /// sin ninguna columna agregada por migraciones. Devuelve el total de
    /// columnas creadas.
    /// </summary>
    private static int CrearSqliteLegacyReconstruidoPreM001(string dbPath, string nombreProyecto)
    {
        var totalColumnas = CrearEsquemaDesdeModelo(dbPath, ColumnasAgregadasPorMigraciones);
        InsertarProyectoLegacy(dbPath, nombreProyecto);
        return totalColumnas;
    }

    private static void InsertarProyectoLegacy(string dbPath, string nombre)
    {
        using var modelo = new ProjectDbContextFactory().Create(dbPath);
        var entidad = modelo.Model.FindEntityType(typeof(Proyecto))
            ?? throw new InvalidOperationException("El modelo EF no contiene Proyecto.");
        var tabla = entidad.GetTableName()
            ?? throw new InvalidOperationException("Proyecto no tiene tabla mapeada.");

        var propiedades = entidad.GetProperties()
            .Select(p => (Propiedad: p, Columna: p.GetColumnName()))
            .Where(x => x.Columna is not null)
            .Where(x => !ColumnasAgregadasPorMigraciones.Any(omitida =>
                omitida.Tabla == tabla
                && string.Equals(omitida.Columna, x.Columna, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var columnas = string.Join(", ", propiedades.Select(x => $"\"{x.Columna}\""));
        var valores = string.Join(", ", propiedades.Select(x =>
            string.Equals(x.Columna, "Nombre", StringComparison.OrdinalIgnoreCase)
                ? $"'{nombre}'"
                : LiteralSql(x.Propiedad.ClrType, x.Propiedad.IsPrimaryKey())));

        Ejecutar(dbPath, $"INSERT INTO \"{tabla}\" ({columnas}) VALUES ({valores});");
    }

    private static string LiteralSql(Type clrType, bool esClave)
    {
        if (esClave)
        {
            return "1";
        }

        var tipo = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (tipo == typeof(string)) return "''";
        if (tipo == typeof(DateTime)) return "'2026-01-01 00:00:00'";
        if (tipo == typeof(byte[])) return "x''";
        if (tipo == typeof(Guid)) return "'00000000-0000-0000-0000-000000000000'";
        return "0";
    }

    private static SqliteConnection AbrirSoloLectura(string dbPath)
    {
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
        connection.Open();
        return connection;
    }

    private static bool TablaExiste(string dbPath, string tabla)
    {
        using var connection = AbrirSoloLectura(dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = tabla;
        command.Parameters.Add(parameter);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool ColumnaExiste(string dbPath, string tabla, string columna)
    {
        using var connection = AbrirSoloLectura(dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{tabla}')";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columna, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    [TestMethod]
    public void OpenProject_RutaInexistente_RechazaSinCrearArchivoNiCandado()
    {
        var dbPath = CrearRutaTemporal();

        var ex = Assert.ThrowsExactly<FileNotFoundException>(
            () => CrearServicio().OpenProject(dbPath));

        StringAssert.Contains(ex.Message, "No se encontró el archivo del proyecto");
        Assert.IsFalse(File.Exists(dbPath), "no debe crearse una base vacía en la ruta inexistente");
        Assert.IsFalse(File.Exists(dbPath + ".lock"), "no debe crearse el candado");
    }

    [TestMethod]
    public void OpenProject_SqliteAjeno_RechazaSinAlterarArchivo_YLiberaCandado()
    {
        var dbPath = CrearRutaTemporal(".db");
        Ejecutar(dbPath, "CREATE TABLE OtraTabla (Id INTEGER PRIMARY KEY, Valor TEXT);");
        Ejecutar(dbPath, "INSERT INTO OtraTabla (Valor) VALUES ('ajeno');");
        var hashOriginal = HashSha256(dbPath);

        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => CrearServicio().OpenProject(dbPath));

        StringAssert.Contains(ex.Message, "El archivo no contiene un proyecto válido");
        Assert.AreEqual(hashOriginal, HashSha256(dbPath), "el SQLite ajeno no debe migrarse ni modificarse");
        Assert.IsFalse(File.Exists(dbPath + ".lock"), "el candado debe liberarse tras el rechazo");
    }

    [TestMethod]
    public void OpenProject_SqliteVacio_RechazaSinAlterarArchivo_YLiberaCandado()
    {
        var dbPath = CrearRutaTemporal(".db");
        File.WriteAllBytes(dbPath, Array.Empty<byte>());
        var hashOriginal = HashSha256(dbPath);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => CrearServicio().OpenProject(dbPath));

        Assert.AreEqual(hashOriginal, HashSha256(dbPath), "el archivo vacío debe quedar intacto");
        Assert.IsFalse(File.Exists(dbPath + ".lock"), "el candado debe liberarse tras el rechazo");
    }

    [TestMethod]
    public void OpenProject_TablaProyectosSinColumnasRequeridas_RechazaSinAlterarArchivo()
    {
        var dbPath = CrearRutaTemporal(".db");
        Ejecutar(dbPath, "CREATE TABLE Proyectos (Id INTEGER PRIMARY KEY, Titulo TEXT);");
        Ejecutar(dbPath, "INSERT INTO Proyectos (Titulo) VALUES ('sin nombre');");
        var hashOriginal = HashSha256(dbPath);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => CrearServicio().OpenProject(dbPath));

        Assert.AreEqual(hashOriginal, HashSha256(dbPath), "no debe migrarse una tabla Proyectos incompleta");
        Assert.IsFalse(File.Exists(dbPath + ".lock"), "el candado debe liberarse tras el rechazo");
    }

    [TestMethod]
    public void OpenProject_SqliteAjenoConTablaProyectos_RechazaSinMigrarNiModificar()
    {
        var dbPath = CrearRutaTemporal(".db");
        Ejecutar(dbPath, "CREATE TABLE Proyectos (Id INTEGER PRIMARY KEY, Nombre TEXT NOT NULL);");
        Ejecutar(dbPath, "CREATE TABLE Matrices (Id INTEGER PRIMARY KEY);");
        Ejecutar(dbPath, "CREATE TABLE ComponentesMatriz (Id INTEGER PRIMARY KEY);");
        Ejecutar(dbPath, "INSERT INTO Proyectos (Nombre) VALUES ('no es SOPRO');");
        var hashOriginal = HashSha256(dbPath);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => CrearServicio().OpenProject(dbPath));

        Assert.AreEqual(hashOriginal, HashSha256(dbPath), "un lookalike no debe recibir tabla/columnas de migración");
        Assert.IsFalse(File.Exists(dbPath + ".lock"), "el candado debe liberarse tras el rechazo");
    }

    [TestMethod]
    public void OpenProject_SqliteAjenoConTablasCoreSinFactorSalarioReal_RechazaSinMigrarNiModificar()
    {
        var dbPath = CrearRutaTemporal(".db");
        CrearSqliteAjenoConTablasCore(dbPath, ("Proyectos", "FactorSalarioReal"));
        Assert.IsTrue(TablaExiste(dbPath, "Proyectos"), "la fixture debe contener la tabla Proyectos");
        var hashOriginal = HashSha256(dbPath);

        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => CrearServicio().OpenProject(dbPath));

        StringAssert.Contains(ex.Message, "El archivo no contiene un proyecto válido");
        Assert.AreEqual(hashOriginal, HashSha256(dbPath), "el SQLite ajeno no debe migrarse ni modificarse");
        Assert.IsFalse(
            TablaExiste(dbPath, "__MigracionesCustom"),
            "no debe crearse la tabla de migraciones antes de rechazar");
        Assert.IsFalse(File.Exists(dbPath + ".lock"), "el candado debe liberarse tras el rechazo");
    }

    [TestMethod]
    public void OpenProject_SqliteAjenoConTablasCoreSinColumnaBaseNoListada_RechazaSinMigrarNiModificar()
    {
        var dbPath = CrearRutaTemporal(".db");
        CrearSqliteAjenoConTablasCore(dbPath, ("Maquinaria", "VidaEconomicaLlantas"));
        Assert.IsTrue(TablaExiste(dbPath, "Proyectos"), "la fixture debe contener la tabla Proyectos");
        var hashOriginal = HashSha256(dbPath);

        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => CrearServicio().OpenProject(dbPath));

        StringAssert.Contains(ex.Message, "El archivo no contiene un proyecto válido");
        Assert.AreEqual(hashOriginal, HashSha256(dbPath), "el SQLite ajeno no debe migrarse ni modificarse");
        Assert.IsFalse(
            TablaExiste(dbPath, "__MigracionesCustom"),
            "no debe crearse la tabla de migraciones antes de rechazar");
        Assert.IsFalse(File.Exists(dbPath + ".lock"), "el candado debe liberarse tras el rechazo");
    }

    [TestMethod]
    public void OpenProject_ProyectoValido_Abre_YRetieneCandadoHastaCerrar()
    {
        var dbPath = CrearRutaTemporal();
        using (var contexto = TestDbFactory.CreateContextAt(dbPath))
        {
            contexto.Proyectos.Add(CrearProyecto("Proyecto preflight"));
            contexto.SaveChanges();
        }

        var servicio = CrearServicio();
        var sesion = servicio.OpenProject(dbPath);
        try
        {
            Assert.AreEqual("Proyecto preflight", sesion.Project.Nombre);
            Assert.IsTrue(File.Exists(dbPath + ".lock"), "una apertura válida retiene el candado");
        }
        finally
        {
            servicio.CloseProjectSession(sesion);
        }

        Assert.IsFalse(File.Exists(dbPath + ".lock"), "cerrar la sesión libera el candado");
    }

    [TestMethod]
    public void OpenProject_EsquemaLegacySinControlDeVersiones_LlegaAMigracion()
    {
        var origen = Path.Combine(
            AppContext.BaseDirectory, "TestData", "proyecto-sintetico-vial-demo.db");
        if (!File.Exists(origen))
        {
            Assert.Inconclusive($"No hay fixture legacy disponible en {origen}.");
        }

        var dbPath = CrearRutaTemporal();
        File.Copy(origen, dbPath, overwrite: true);
        Ejecutar(dbPath, "DROP TABLE __MigracionesCustom;");

        var servicio = CrearServicio();
        using (var sesion = servicio.OpenProject(dbPath))
        {
            Assert.AreEqual("PROYECTO SINTETICO VIAL DEMO", sesion.Project.Nombre);
        }

        using var verificacion = new ProjectDbContextFactory().CreateReadOnly(dbPath);
        Assert.AreEqual(
            SchemaManager.VersionActual,
            SchemaManager.GetVersionRegistrada(verificacion),
            "la apertura debe aplicar la migración y registrar la versión actual");
        Assert.IsFalse(File.Exists(dbPath + ".lock"), "el candado debe liberarse al cerrar");
    }

    /// <summary>
    /// No existe un fixture real del esquema previo a M001, así que se reconstruye
    /// desde el modelo EF: las ocho tablas core con sus 137 columnas base y sin
    /// ninguna columna agregada por migraciones. <see cref="ProjectLifecycleService.OpenProject"/>
    /// debe reconocerlo como proyecto SOPRO válido y migrarlo hasta
    /// <see cref="SchemaManager.VersionActual"/>.
    /// </summary>
    [TestMethod]
    public void OpenProject_EsquemaCorePreM001Reconstruido_MigraYAbreElProyecto()
    {
        var dbPath = CrearRutaTemporal(".db");
        var columnasCreadas = CrearSqliteLegacyReconstruidoPreM001(dbPath, "Proyecto pre-M001");

        Assert.AreEqual(137, columnasCreadas, "la reconstrucción debe tener las 137 columnas base");
        Assert.IsFalse(
            TablaExiste(dbPath, "__MigracionesCustom"),
            "el esquema reconstruido no debe traer control de versiones");

        var servicio = CrearServicio();
        using (var sesion = servicio.OpenProject(dbPath))
        {
            Assert.AreEqual("Proyecto pre-M001", sesion.Project.Nombre);
            Assert.IsTrue(File.Exists(dbPath + ".lock"), "una apertura válida retiene el candado");
        }

        Assert.IsFalse(File.Exists(dbPath + ".lock"), "cerrar la sesión libera el candado");

        using (var verificacion = new ProjectDbContextFactory().CreateReadOnly(dbPath))
        {
            Assert.AreEqual(
                SchemaManager.VersionActual,
                SchemaManager.GetVersionRegistrada(verificacion),
                "la apertura debe aplicar todas las migraciones y registrar la versión actual");
        }

        Assert.IsTrue(TablaExiste(dbPath, "Herramientas"), "M001 debe crear Herramientas");
        Assert.IsTrue(ColumnaExiste(dbPath, "ComponentesMatriz", "HerramientaId"), "M001 debe agregar HerramientaId");
        Assert.IsTrue(ColumnaExiste(dbPath, "Proyectos", "ModoCalculoPorcentajes"), "M002 debe agregar ModoCalculoPorcentajes");
        Assert.IsTrue(ColumnaExiste(dbPath, "ComponentesMatriz", "Rendimiento"), "M004 debe agregar Rendimiento");
        Assert.IsTrue(ColumnaExiste(dbPath, "ColumnasPersonalizadas", "Modulo"), "M004 debe agregar Modulo");
        Assert.IsTrue(ColumnaExiste(dbPath, "ColumnasPersonalizadas", "WrapTexto"), "M007 debe agregar WrapTexto");
        Assert.IsTrue(ColumnaExiste(dbPath, "ColumnasPersonalizadas", "AlineacionVertical"), "M007 debe agregar AlineacionVertical");
        Assert.IsTrue(ColumnaExiste(dbPath, "Maquinaria", "FechaCalculoCosto"), "M008 debe agregar FechaCalculoCosto");
        Assert.IsTrue(ColumnaExiste(dbPath, "ConceptosPresupuesto", "ColumnasPersonalizadasJSON"), "M009 debe agregar ColumnasPersonalizadasJSON");
        Assert.IsTrue(ColumnaExiste(dbPath, "Maquinaria", "MaterialCombustibleId"), "M015 debe agregar MaterialCombustibleId");
        Assert.IsTrue(ColumnaExiste(dbPath, "Maquinaria", "MaterialAceiteId"), "M015 debe agregar MaterialAceiteId");
        Assert.IsTrue(ColumnaExiste(dbPath, "Maquinaria", "ManoDeObraOperadorId"), "M015 debe agregar ManoDeObraOperadorId");
    }
}
