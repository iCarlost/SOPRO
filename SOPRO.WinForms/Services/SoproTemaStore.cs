using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SOPRO.Application.Contracts;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Persistencia local del preset de tema elegido por el usuario (issue
    /// #203). Guarda un ID estable en JSON dentro de
    /// <see cref="WorkspacePaths.LocalDataFolder"/> con escritura atomica
    /// (temporal + move).
    ///
    /// Contrato de tolerancia: la lectura NUNCA bloquea el arranque ni reescribe
    /// datos invalidos; ausente, corrupto, desconocido o ilegible cae al preset
    /// por omision (azul historico). El guardado informa exito/fracaso con un
    /// booleano para que la UI pueda avisar visiblemente.
    /// </summary>
    internal sealed class SoproTemaStore
    {
        /// <summary>Archivo de preferencia dentro de la carpeta de datos locales.</summary>
        internal const string NombreArchivo = "tema-preferido.json";

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private readonly string _localDataDirectory;

        /// <summary>
        /// Crea el store. Sin argumento usa <see cref="WorkspacePaths.LocalDataFolder"/>
        /// (los tests inyectan una carpeta temporal).
        /// </summary>
        internal SoproTemaStore(string? localDataDirectory = null)
            => _localDataDirectory = localDataDirectory ?? WorkspacePaths.LocalDataFolder;

        /// <summary>Ruta completa del archivo de preferencia.</summary>
        internal string ArchivoPreferencia => Path.Combine(_localDataDirectory, NombreArchivo);

        /// <summary>
        /// Lee el preset persistido. Ausente, corrupto, con ID desconocido o
        /// ilegible devuelve <see cref="SoproTemaPreset.ClaroAzul"/> sin lanzar
        /// ni modificar el archivo.
        /// </summary>
        internal SoproTemaPreset Load()
        {
            try
            {
                var ruta = ArchivoPreferencia;
                if (!File.Exists(ruta))
                    return SoproTemaPreset.ClaroAzul;

                string json = File.ReadAllText(ruta, Encoding.UTF8);
                var preferencia = JsonSerializer.Deserialize<PreferenciaTema>(json, _jsonOptions);
                return preferencia is not null
                    && SoproTheme.TryParsePreset(preferencia.Tema, out var preset)
                        ? preset
                        : SoproTemaPreset.ClaroAzul;
            }
            catch (IOException) { return SoproTemaPreset.ClaroAzul; }
            catch (UnauthorizedAccessException) { return SoproTemaPreset.ClaroAzul; }
            catch (JsonException) { return SoproTemaPreset.ClaroAzul; }
            catch (NotSupportedException) { return SoproTemaPreset.ClaroAzul; }
        }

        /// <summary>
        /// Guarda el preset. Devuelve false ante cualquier fallo de escritura
        /// (permisos, disco, ruta invalida) sin propagar la excepcion: el
        /// llamador debe informar el fallo al usuario.
        /// </summary>
        internal bool TrySave(SoproTemaPreset preset)
        {
            string? temporal = null;
            try
            {
                Directory.CreateDirectory(_localDataDirectory);

                var ruta = ArchivoPreferencia;
                temporal = ruta + "." + Guid.NewGuid().ToString("N") + ".tmp";

                var bytes = JsonSerializer.SerializeToUtf8Bytes(
                    new PreferenciaTema { Tema = SoproTheme.GetPresetId(preset) },
                    _jsonOptions);

                using (var stream = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes);
                    stream.Flush(flushToDisk: true);
                }

                File.Move(temporal, ruta, overwrite: true);
                temporal = null;
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (NotSupportedException) { return false; }
            finally
            {
                if (temporal is not null)
                {
                    try { File.Delete(temporal); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        private sealed class PreferenciaTema
        {
            [JsonPropertyName("tema")]
            public string? Tema { get; set; }
        }
    }
}
