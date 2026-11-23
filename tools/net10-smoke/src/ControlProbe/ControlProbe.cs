using System.Globalization;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;

namespace SoproSmoke;

/// <summary>
/// Sonda de control mínima para demostrar que el hook observa switch, EF por
/// ruta/PID y ventana por PID/título. No es SOPRO y no escribe en el perfil.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("uso: ControlProbe switch-probe | ef --db <ruta> [--hold N] | ui");
            return 2;
        }

        try
        {
            return args[0] switch
            {
                "switch-probe" => SwitchProbe(),
                "ef" => EfProbe(args),
                "insert-only" => InsertOnly(args),
                "ui" => UiProbe(),
                _ => Unknown(args[0])
            };
        }
        catch (Exception exception)
        {
            Console.WriteLine("RESULT=FAIL");
            Console.WriteLine("errorType=" + exception.GetType().FullName);
            Console.WriteLine("error=" + exception.Message.Replace('\r', ' ').Replace('\n', ' '));
            return 1;
        }
    }

    private static int Unknown(string mode)
    {
        Console.Error.WriteLine("modo desconocido: " + mode);
        return 2;
    }

    private static int SwitchProbe()
    {
        Console.WriteLine("switch.before=" + SwitchText());
        Thread.Sleep(1500);
        AppContext.SetSwitch("Microsoft.Data.Sqlite.Pre10TimeZoneHandling", isEnabled: true);
        Console.WriteLine("switch.after=" + SwitchText());
        Thread.Sleep(500);
        return 0;
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
            ? parsed : 0;
        var fullPath = Path.GetFullPath(dbPath);
        var options = new DbContextOptionsBuilder<ProbeContext>()
            .UseSqlite($"Data Source={fullPath}").Options;

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
            if (count != 1) return 1;
        }

        if (holdSeconds > 0)
        {
            Console.WriteLine("HOLD=ready");
            Console.Out.Flush();
            Thread.Sleep(TimeSpan.FromSeconds(holdSeconds));
        }

        return 0;
    }

    private static int InsertOnly(string[] args)
    {
        if (!TryGetOption(args, "--db", out var dbPath) || string.IsNullOrWhiteSpace(dbPath))
        {
            Console.Error.WriteLine("falta --db <ruta>");
            return 2;
        }
        var options = new DbContextOptionsBuilder<ProbeContext>()
            .UseSqlite($"Data Source={Path.GetFullPath(dbPath)}").Options;
        using (var context = new ProbeContext(options))
        {
            context.Database.EnsureCreated();
            context.Entidades.Add(new ProbeEntity { Nombre = "solo-insert" });
            context.SaveChanges();
        }
        Console.WriteLine("INSERTONLY=OK");
        Console.Out.Flush();
        Thread.Sleep(TimeSpan.FromSeconds(30));
        return 0;
    }
    private static int UiProbe()    {
        var title = Environment.GetEnvironmentVariable("SOPRO_SMOKE_UI_TITLE");
        if (string.IsNullOrWhiteSpace(title)) title = "SOPRO UI PROBE";
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var form = new Form { Text = title, Width = 480, Height = 240, StartPosition = FormStartPosition.CenterScreen };
        using var timer = new System.Windows.Forms.Timer { Interval = 45_000 };
        timer.Tick += (_, _) => { timer.Stop(); form.Close(); };
        timer.Start();
        Application.Run(form);
        return 0;
    }

    private static string SwitchText() =>
        AppContext.TryGetSwitch("Microsoft.Data.Sqlite.Pre10TimeZoneHandling", out var enabled)
            ? enabled.ToString(CultureInfo.InvariantCulture) : "absent";

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
}

internal sealed class ProbeContext : DbContext
{
    public ProbeContext(DbContextOptions<ProbeContext> options) : base(options) { }
    public DbSet<ProbeEntity> Entidades => Set<ProbeEntity>();
}

internal sealed class ProbeEntity
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}
