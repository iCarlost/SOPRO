using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
/// <summary>
/// Hook externo de observación para el smoke test de #160.
/// No forma parte del producto: se inyecta con DOTNET_STARTUP_HOOKS y solo
/// escribe evidencia en SOPRO_SMOKE_PROBE_DIR. No referencia SQLite ni EF Core.
/// Suscribe DiagnosticSource de EF Core por reflexión para registrar la apertura
/// de la conexión y la ejecución de una consulta contra una ruta concreta.
/// </summary>
internal static class StartupHook
{
    private const string SwitchName = "Microsoft.Data.Sqlite.Pre10TimeZoneHandling";
    private const string NativeModule = "e_sqlite3";
    private const string EfListenerName = "Microsoft.EntityFrameworkCore";
    private const string ConnectionOpeningEvent =
        "Microsoft.EntityFrameworkCore.Database.Connection.ConnectionOpening";
    private const string ConnectionOpenedEvent =
        "Microsoft.EntityFrameworkCore.Database.Connection.ConnectionOpened";
    private const string CommandExecutingEvent =
        "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuting";
    private const string CommandExecutedEvent =
        "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted";
    private static readonly object EfGate = new();
    private static readonly IObserver<DiagnosticListener> s_allListenersObserver =
        new AllListenersObserver();
    private static readonly IObserver<KeyValuePair<string, object?>> s_efEventObserver =
        new EfEventObserver();
    private static IDisposable? s_allListenersSubscription;
    private static string s_probeDir = string.Empty;
    private static int s_efEvents;
    private static int s_projectOpened;
    private static int s_signalConnection;
    private static int s_signalCommand;
    private static readonly List<string> s_windowTitles = new();
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);
    public static void Initialize()
    {
        s_probeDir = Environment.GetEnvironmentVariable("SOPRO_SMOKE_PROBE_DIR")
            ?? Path.Combine(Path.GetTempPath(), "sopro-smoke-probe");
        Directory.CreateDirectory(s_probeDir);
        SubscribeEfDiagnostics();
        TryWrite("hook-startup.txt", Snapshot("startup"));
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            TryWrite("hook-exit.txt", Snapshot("exit"));
        };
        if (TryReadInt("SOPRO_SMOKE_WATCHDOG_SECONDS", out var timeoutSeconds) && timeoutSeconds > 0)
        {
            var thread = new Thread(() => Watchdog(timeoutSeconds))
            {
                IsBackground = true,
                Name = "SoproSmokeWatchdog"
            };
            thread.Start();
        }
    }
    private static void SubscribeEfDiagnostics()
    {
        try
        {
            s_allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(s_allListenersObserver);
        }
        catch (Exception exception)
        {
            TryWrite("hook-ef-subscribe-error.txt", ErrorText(exception));
        }
    }
    private static void Watchdog(int timeoutSeconds)
    {
        var started = Stopwatch.StartNew();
        var deadline = TimeSpan.FromSeconds(timeoutSeconds);
        var grace = TryReadInt("SOPRO_SMOKE_GRACE_SECONDS", out var graceSeconds) && graceSeconds >= 0
            ? graceSeconds
            : 3;
        var expectedDb = Environment.GetEnvironmentVariable("SOPRO_SMOKE_EXPECT_DB");
        var expectedTitle = Environment.GetEnvironmentVariable("SOPRO_SMOKE_EXPECT_TITLE");
        while (started.Elapsed < deadline)
        {
            var projectReady = string.IsNullOrWhiteSpace(expectedDb)
                ? !string.IsNullOrWhiteSpace(expectedTitle)
                : Volatile.Read(ref s_projectOpened) == 1;
            var titleReady = string.IsNullOrWhiteSpace(expectedTitle) || TitlePresent(expectedTitle!);
            if (projectReady && titleReady)
            {
                TryWrite("window-signal.txt", WindowSignal("ready"));
                TryWrite("hook-ready.txt", Snapshot("ready"));
                Thread.Sleep(TimeSpan.FromSeconds(grace));
                TryWrite("window-signal.txt", WindowSignal("terminating"));
                TryWrite("hook-terminating.txt", Snapshot("terminating"));
                Environment.Exit(0);
                return;
            }
            Thread.Sleep(250);
        }
        TryWrite("hook-timeout.txt", Snapshot("timeout"));
        Environment.Exit(86);
    }
    private static string Snapshot(string phase)
    {
        var sb = new StringBuilder();
        Append(sb, "phase", phase);
        Append(sb, "pid", Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        Append(sb, "utc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        Append(sb, "localAppData", SafeFolder(Environment.SpecialFolder.LocalApplicationData));
        Append(sb, "documents", SafeFolder(Environment.SpecialFolder.MyDocuments));
        Append(sb, "userProfile", Environment.GetEnvironmentVariable("USERPROFILE") ?? string.Empty);
        Append(sb, "envLocalAppData", Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? string.Empty);
        Append(sb, "envAppData", Environment.GetEnvironmentVariable("APPDATA") ?? string.Empty);
        Append(sb, "timeZone", TimeZoneInfo.Local.Id);
        Append(sb, "localOffset", TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).ToString());
        Append(sb, "baseDirectory", AppContext.BaseDirectory);
        Append(sb, "commandLine", string.Join(" ", Environment.GetCommandLineArgs()).Replace('\r', ' ').Replace('\n', ' '));
        var present = AppContext.TryGetSwitch(SwitchName, out var enabled);
        Append(sb, "switchPresent", present ? "true" : "false");
        Append(sb, "switchEnabled", enabled ? "true" : "false");
        Append(sb, "sqliteManagedLoaded", ManagedSqliteLoaded() ? "true" : "false");
        Append(sb, "sqliteNativeLoaded", NativeLoaded() ? "true" : "false");
        Append(sb, "nativeModulePath", NativeModulePath());
        Append(sb, "efDiagnosticsSubscribed", s_allListenersSubscription is null ? "false" : "true");
        Append(sb, "efEvents", Volatile.Read(ref s_efEvents).ToString(CultureInfo.InvariantCulture));
        Append(sb, "projectOpenSignal", Volatile.Read(ref s_projectOpened) == 1 ? "true" : "false");
        var expectedTitle = Environment.GetEnvironmentVariable("SOPRO_SMOKE_EXPECT_TITLE");
        Append(sb, "titlePresent", !string.IsNullOrWhiteSpace(expectedTitle) && TitlePresent(expectedTitle!) ? "true" : "false");
        Append(sb, "expectedTitle", expectedTitle ?? string.Empty);
        Append(sb, "expectedDb", Environment.GetEnvironmentVariable("SOPRO_SMOKE_EXPECT_DB") ?? string.Empty);
        Append(sb, "expectedTable", Environment.GetEnvironmentVariable("SOPRO_SMOKE_EXPECT_TABLE") ?? string.Empty);
        Append(sb, "loadedAssemblies", string.Join(";", LoadedAssemblyNames()));
        return sb.ToString();
    }
    private static void Append(StringBuilder sb, string key, string value)
    {
        sb.Append(key).Append('=').Append(value).Append('\n');
    }
    private static string SafeFolder(Environment.SpecialFolder folder)
    {
        try
        {
            return Environment.GetFolderPath(folder);
        }
        catch (Exception exception)
        {
            return "<error:" + exception.GetType().Name + ">";
        }
    }
    private static bool ManagedSqliteLoaded()
    {
        return LoadedAssemblyNames().Any(name =>
            name.StartsWith("Microsoft.Data.Sqlite", StringComparison.OrdinalIgnoreCase));
    }
    private static string[] LoadedAssemblyNames()
    {
        try
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetName().Name ?? string.Empty)
                .Where(name => name.Length > 0)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
    private static bool NativeLoaded() => NativeModulePath().Length > 0;
    private static bool TitlePresent(string expectedTitle) =>
        WindowTitles().Any(title => title.IndexOf(expectedTitle, StringComparison.OrdinalIgnoreCase) >= 0);
    private static string[] WindowTitles()
    {
        lock (s_windowTitles)
        {
            s_windowTitles.Clear();
            try { EnumWindows(EnumWindow, IntPtr.Zero); } catch { }
            return s_windowTitles.ToArray();
        }
    }
    private static bool EnumWindow(IntPtr hWnd, IntPtr lParam)
    {
        try
        {
            GetWindowThreadProcessId(hWnd, out var processId);
            if (processId != (uint)Environment.ProcessId) return true;
            var buffer = new StringBuilder(512);
            if (GetWindowText(hWnd, buffer, buffer.Capacity) > 0) s_windowTitles.Add(buffer.ToString());
        }
        catch { }
        return true;
    }
    private static string WindowSignal(string phase)
    {
        var titles = WindowTitles();
        var expectedTitle = Environment.GetEnvironmentVariable("SOPRO_SMOKE_EXPECT_TITLE") ?? string.Empty;
        var match = titles.FirstOrDefault(title =>
            title.IndexOf(expectedTitle, StringComparison.OrdinalIgnoreCase) >= 0) ?? string.Empty;
        return string.Join("&", new[]
        {
            "pid=" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
            "utc=" + Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
            "phase=" + Uri.EscapeDataString(phase),
            "expectedTitle=" + Uri.EscapeDataString(expectedTitle),
            "titlePresent=" + (match.Length > 0 ? "true" : "false"),
            "titleCount=" + titles.Length.ToString(CultureInfo.InvariantCulture),
            "title=" + Uri.EscapeDataString(match)
        }) + Environment.NewLine;
    }
    private static string NativeModulePath()
    {
        try
        {
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
            {
                var name = module.ModuleName ?? string.Empty;
                if (name.StartsWith(NativeModule, StringComparison.OrdinalIgnoreCase))
                {
                    return module.FileName ?? name;
                }
            }
        }
        catch
        {
        }
        return string.Empty;
    }
    private static bool TryReadInt(string variable, out int value)
    {
        value = 0;
        var raw = Environment.GetEnvironmentVariable(variable);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
    private static void WriteEfEvent(string eventName, string dataSource, string sql)
    {
        try
        {
            var line = string.Join("&", new[]
            {
                "pid=" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                "utc=" + Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
                "event=" + Uri.EscapeDataString(eventName),
                "dataSource=" + Uri.EscapeDataString(dataSource ?? string.Empty),
                "sql=" + Uri.EscapeDataString(sql ?? string.Empty)
            });
            lock (EfGate)
            {
                Directory.CreateDirectory(s_probeDir);
                File.AppendAllText(
                    Path.Combine(s_probeDir, "ef-events.log"),
                    line + Environment.NewLine,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                Interlocked.Increment(ref s_efEvents);
            }
        }
        catch
        {
        }
    }
    private static void WriteFilteredSignal(string eventName, string dataSource, string sql)
    {
        var expectedDb = Environment.GetEnvironmentVariable("SOPRO_SMOKE_EXPECT_DB");
        if (string.IsNullOrWhiteSpace(expectedDb) || !PathsEqual(dataSource, expectedDb))
        {
            return;
        }
        var expectedTable = Environment.GetEnvironmentVariable("SOPRO_SMOKE_EXPECT_TABLE");
        if (string.IsNullOrWhiteSpace(expectedTable))
        {
            expectedTable = "Proyectos";
        }
        string table;
        if (string.Equals(eventName, ConnectionOpenedEvent, StringComparison.Ordinal))
        {
            if (Interlocked.Exchange(ref s_signalConnection, 1) == 1)
            {
                return;
            }
            table = string.Empty;
        }
        else if (string.Equals(eventName, CommandExecutedEvent, StringComparison.Ordinal))
        {
            if (sql.IndexOf(expectedTable, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }
            if (Interlocked.Exchange(ref s_signalCommand, 1) == 1)
            {
                return;
            }
            table = expectedTable;
        }
        else
        {
            return;
        }
        try
        {
            var line = string.Join("&", new[]
            {
                "pid=" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                "utc=" + Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
                "event=" + Uri.EscapeDataString(eventName),
                "dataSource=" + Uri.EscapeDataString(dataSource),
                "table=" + Uri.EscapeDataString(table),
                "sqlDisclosed=false"
            });
            lock (EfGate)
            {
                Directory.CreateDirectory(s_probeDir);
                File.AppendAllText(
                    Path.Combine(s_probeDir, "project-signal.txt"),
                    line + Environment.NewLine,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch
        {
        }
    }
    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
    private static string ExtractDataSource(object? payload)
    {
        var connection = GetProperty(payload, "Connection");
        if (connection is null)
        {
            var command = GetProperty(payload, "Command");
            connection = GetProperty(command, "Connection");
        }
        return GetProperty(connection, "DataSource") as string ?? string.Empty;
    }
    private static string ExtractSql(object? payload)
    {
        var command = GetProperty(payload, "Command");
        return GetProperty(command, "CommandText") as string ?? string.Empty;
    }
    private static object? GetProperty(object? instance, string name)
    {
        if (instance is null)
        {
            return null;
        }
        try
        {
            var property = instance.GetType().GetProperty(
                name,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.IgnoreCase);
            return property?.GetValue(instance);
        }
        catch
        {
            return null;
        }
    }
    private static string ErrorText(Exception exception)
    {
        return "pid=" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) +
               "\ntype=" + exception.GetType().FullName +
               "\nmessage=" + exception.Message.Replace('\r', ' ').Replace('\n', ' ');
    }
    private static void TryWrite(string fileName, string content)
    {
        try
        {
            var path = Path.Combine(s_probeDir, fileName);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
        }
    }
    private sealed class AllListenersObserver : IObserver<DiagnosticListener>
    {
        public void OnNext(DiagnosticListener value)
        {
            if (!string.Equals(value.Name, EfListenerName, StringComparison.Ordinal))
            {
                return;
            }
            try
            {
                value.Subscribe(s_efEventObserver);
                TryWrite("hook-ef-subscribed.txt",
                    "pid=" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) +
                    "\nlistener=" + value.Name);
            }
            catch (Exception exception)
            {
                TryWrite("hook-ef-subscribe-error.txt", ErrorText(exception));
            }
        }
        public void OnCompleted()
        {
        }
        public void OnError(Exception error)
        {
        }
    }
    private sealed class EfEventObserver : IObserver<KeyValuePair<string, object?>>
    {
        public void OnNext(KeyValuePair<string, object?> value)
        {
            var eventName = value.Key ?? string.Empty;
            if (!IsInteresting(eventName))
            {
                return;
            }
            var dataSource = ExtractDataSource(value.Value);
            var sql = ExtractSql(value.Value);
            WriteEfEvent(eventName, dataSource, sql);
            WriteFilteredSignal(eventName, dataSource, sql);
            if (IsExpectedProjectQuery(eventName, dataSource, sql))
            {
                Volatile.Write(ref s_projectOpened, 1);
            }
        }
        private static bool IsExpectedProjectQuery(string eventName, string dataSource, string sql)
        {
            if (!string.Equals(eventName, CommandExecutedEvent, StringComparison.Ordinal))
            {
                return false;
            }
            var expectedDb = Environment.GetEnvironmentVariable("SOPRO_SMOKE_EXPECT_DB");
            if (string.IsNullOrWhiteSpace(expectedDb))
            {
                return false;
            }
            var expectedTable = Environment.GetEnvironmentVariable("SOPRO_SMOKE_EXPECT_TABLE");
            if (string.IsNullOrWhiteSpace(expectedTable))
            {
                expectedTable = "Proyectos";
            }
            return PathsEqual(dataSource, expectedDb) &&
                   sql.IndexOf(expectedTable, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        public void OnCompleted()
        {
        }
        public void OnError(Exception error)
        {
        }
        private static bool IsInteresting(string eventName)
        {
            return string.Equals(eventName, ConnectionOpeningEvent, StringComparison.Ordinal) ||
                   string.Equals(eventName, ConnectionOpenedEvent, StringComparison.Ordinal) ||
                   string.Equals(eventName, CommandExecutingEvent, StringComparison.Ordinal) ||
                   string.Equals(eventName, CommandExecutedEvent, StringComparison.Ordinal);
        }
    }
}
