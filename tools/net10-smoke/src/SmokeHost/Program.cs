using System.Globalization;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
namespace SoproSmoke;
/// <summary>
/// Sondas puras del smoke test. No es SOPRO y no escribe en el perfil:
/// - provider: control del proveedor SQLite 10 (NO mide el comportamiento de la app).
/// - ef: observador de EF Core 10 por ruta/PID.
/// - switch-probe: mecánica del hook.
/// </summary>
internal static class Program
{
    private const string SwitchName = "Microsoft.Data.Sqlite.Pre10TimeZoneHandling";
    private const string ExpectedProjectName = "PROYECTO SINTETICO VIAL DEMO";
    private const string ExpectedFechaInicio = "2026-04-30 14:33:07.534";
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine(
                "uso: SmokeHost switch-probe | " +
                "provider --db <ruta> --expect pre10|modern | ef --db <ruta> [--hold N] | ui --title <titulo>");
            return 2;
        }
        try
        {
            switch (args[0])
            {
                case "switch-probe":
                    return SwitchProbe();
                case "provider":
                    return ProviderProbe(args);
                case "ef":
                    return EfProbe(args);
                case "ui":
                    return UiProbe(args);
                default:
                    Console.Error.WriteLine("modo desconocido: " + args[0]);
                    return 2;
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine("RESULT=FAIL");
            Console.WriteLine("errorType=" + exception.GetType().FullName);
            Console.WriteLine("error=" + exception.Message.Replace('\r', ' ').Replace('\n', ' '));
            return 1;
        }
    }
    private static int SwitchProbe()
    {
        Console.WriteLine("switch.before=" + SwitchText());
        Thread.Sleep(1500);
        AppContext.SetSwitch(SwitchName, isEnabled: true);
        Console.WriteLine("switch.after=" + SwitchText());
        Thread.Sleep(500);
        return 0;
    }
    private static int ProviderProbe(string[] args)
    {
        if (!TryGetOption(args, "--db", out var dbPath) || string.IsNullOrWhiteSpace(dbPath))
        {
            Console.Error.WriteLine("falta --db <ruta>");
            return 2;
        }
        var expect = TryGetOption(args, "--expect", out var expected) ? expected : "pre10";
        var pre10 = !string.Equals(expect, "modern", StringComparison.OrdinalIgnoreCase);
        AppContext.SetSwitch(SwitchName, pre10);
        Print("observer", "provider-control");
        Print("scope", "provider-only; no mide el comportamiento de SOPRO");
        Print("expect", expect);
        Print("switch", SwitchText());
        Print("db", Path.GetFullPath(dbPath));
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
        connection.Open();
        var failures = new List<string>();
        CheckFixture(connection, failures);
        CheckNoOffsetLiteral(connection, pre10, failures);
        if (failures.Count == 0)
        {
            Console.WriteLine("RESULT=PASS");
            return 0;
        }
        Console.WriteLine("RESULT=FAIL");
        foreach (var failure in failures)
        {
            Console.WriteLine("failure=" + failure.Replace('\r', ' ').Replace('\n', ' '));
        }
        return 1;
    }
    private static void CheckFixture(SqliteConnection connection, List<string> failures)
    {
        using (var table = connection.CreateCommand())
        {
            table.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Proyectos'";
            if (Convert.ToInt32(table.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
            {
                failures.Add("la copia no contiene la tabla Proyectos");
                return;
            }
        }
        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Proyectos";
        var projects = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);
        Print("fixture.projects", projects.ToString(CultureInfo.InvariantCulture));
        if (projects != 1)
        {
            failures.Add("el fixture canónico debe tener exactamente 1 proyecto; observado " + projects);
            return;
        }
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Nombre, FechaInicio, typeof(FechaInicio) FROM Proyectos ORDER BY Id LIMIT 1";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            failures.Add("no se pudo leer el proyecto del fixture");
            return;
        }
        var name = reader.IsDBNull(0) ? "<null>" : reader.GetString(0);
        var type = reader.GetString(2);
        var text = reader.IsDBNull(1) ? "<null>" : reader.GetString(1);
        Print("fixture.nombre", name);
        Print("fixture.fechaInicio.type", type);
        Print("fixture.fechaInicio.text", text);
        if (!string.Equals(name, ExpectedProjectName, StringComparison.Ordinal))
        {
            failures.Add("el nombre del proyecto no es el fixture canónico: " + name);
        }
        if (!string.Equals(type, "text", StringComparison.Ordinal))
        {
            failures.Add("FechaInicio no está persistida como TEXT: " + type);
        }
        if (!string.Equals(text, ExpectedFechaInicio, StringComparison.Ordinal))
        {
            failures.Add("el TEXT de FechaInicio no es el del fixture canónico: " + text);
        }
    }
    private static void CheckNoOffsetLiteral(
        SqliteConnection connection, bool pre10, List<string> failures)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT '2026-04-15 13:45:30.1234567'";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            failures.Add("no se pudo leer el literal de fecha sin offset");
            return;
        }
        var value = reader.GetDateTimeOffset(0);
        var localClock = new DateTime(2026, 4, 15, 13, 45, 30, 123).AddTicks(4567);
        var localOffset = TimeZoneInfo.Local.GetUtcOffset(localClock);
        var expectedUtcTicks = pre10
            ? new DateTimeOffset(localClock, localOffset).UtcTicks
            : new DateTimeOffset(localClock, TimeSpan.Zero).UtcTicks;
        Print("literal.utcTicks", value.UtcTicks.ToString(CultureInfo.InvariantCulture));
        Print("literal.expectedUtcTicks", expectedUtcTicks.ToString(CultureInfo.InvariantCulture));
        Print("literal.offsetMinutes", value.Offset.TotalMinutes.ToString(CultureInfo.InvariantCulture));
        Print("literal.expectedOffsetMinutes", localOffset.TotalMinutes.ToString(CultureInfo.InvariantCulture));
        if (value.DateTime.Ticks != localClock.Ticks)
        {
            failures.Add("el literal sin offset no conserva el reloj local");
        }
        if (value.UtcTicks != expectedUtcTicks)
        {
            failures.Add(
                "el instante UTC del literal sin offset no coincide: " +
                value.UtcTicks + " != " + expectedUtcTicks);
        }
        if (pre10 && value.Offset != localOffset)
        {
            failures.Add("pre10: offset local esperado " + localOffset + "; observado " + value.Offset);
        }
        if (!pre10 && value.Offset != TimeSpan.Zero)
        {
            failures.Add("modern: offset UTC esperado; observado " + value.Offset);
        }
    }
    private static int EfProbe(string[] args)
    {
        if (!TryGetOption(args, "--db", out var dbPath) || string.IsNullOrWhiteSpace(dbPath))
        {
            Console.Error.WriteLine("falta --db <ruta>");
            return 2;
        }
        var holdSeconds = TryGetOption(args, "--hold", out var holdRaw) &&
            int.TryParse(holdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
        var fullPath = Path.GetFullPath(dbPath);
        var options = new DbContextOptionsBuilder<ProbeContext>()
            .UseSqlite($"Data Source={fullPath}")
            .Options;
        using (var context = new ProbeContext(options))
        {
            context.Database.EnsureCreated();
            context.Entidades.Add(new ProbeEntity { Nombre = "fila-demo" });
            context.SaveChanges();
        }
        using (var context = new ProbeContext(options))
        {
            var count = context.Entidades.Count();
            Console.WriteLine("EFPROBE=OK");
            Console.WriteLine("db=" + fullPath);
            Console.WriteLine("count=" + count.ToString(CultureInfo.InvariantCulture));
            if (count != 1)
            {
                return 1;
            }
        }
        if (holdSeconds > 0)
        {
            Console.WriteLine("HOLD=ready");
            Console.Out.Flush();
            Thread.Sleep(TimeSpan.FromSeconds(holdSeconds));
        }
        return 0;
    }
    private static int UiProbe(string[] args)
    {
        var title = TryGetOption(args, "--title", out var explicitTitle) && !string.IsNullOrWhiteSpace(explicitTitle)
            ? explicitTitle : "SOPRO UI PROBE";
        var envTitle = Environment.GetEnvironmentVariable("SOPRO_SMOKE_UI_TITLE");
        if (!string.IsNullOrWhiteSpace(envTitle)) title = envTitle;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var form = new Form { Text = title, Width = 480, Height = 240, StartPosition = FormStartPosition.CenterScreen };
        using var timer = new System.Windows.Forms.Timer { Interval = 45_000 };
        timer.Tick += (_, _) => { timer.Stop(); form.Close(); };
        timer.Start();
        Application.Run(form);
        return 0;
    }
    private static string SwitchText()
    {
        return AppContext.TryGetSwitch(SwitchName, out var enabled)
            ? enabled.ToString(CultureInfo.InvariantCulture)
            : "absent";
    }
    private static bool TryGetOption(string[] args, string name, out string value)
    {
        value = string.Empty;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                value = args[i + 1];
                return true;
            }
        }
        return false;
    }
    private static void Print(string key, string value)
    {
        Console.WriteLine(key + "=" + value.Replace('\r', ' ').Replace('\n', ' '));
    }
}
internal sealed class ProbeContext : DbContext
{
    public ProbeContext(DbContextOptions<ProbeContext> options)
        : base(options)
    {
    }
    public DbSet<ProbeEntity> Entidades => Set<ProbeEntity>();
}
internal sealed class ProbeEntity
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}
