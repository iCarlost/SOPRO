// ============================================================
// SOPRO POC v9 — ImagenCache
// Fuente de verdad: ImagenBytes del elemento.
// La ruta es solo metadato — nunca se lee para renderizar.
// ============================================================
namespace SOPRO.WinForms.Forms.Disenador;

public sealed class ImagenCache : IDisposable
{
    private readonly Dictionary<Guid, Image> _cache = new();

    /// <summary>
    /// Obtiene la Image en memoria para un elemento, cargándola desde
    /// ImagenBytes si no está en cache. Nunca accede a disco para renderizar.
    /// </summary>
    public Image? ObtenerImagen(ElementoCanvas el)
    {
        if (el.Tipo != TipoElemento.Imagen) return null;
        if (_cache.TryGetValue(el.Id, out var cached)) return cached;

        if (el.ImagenBytes is not { Length: > 0 }) return null;

        try
        {
            using var ms = new MemoryStream(el.ImagenBytes);
            var img = Image.FromStream(ms);
            _cache[el.Id] = img;
            return img;
        }
        catch { return null; }
    }

    /// <summary>
    /// Carga una imagen desde disco, lee sus bytes y los almacena en el elemento.
    /// Después de esto el elemento es autónomo — no necesita el archivo original.
    /// </summary>
    public bool ImportarDesdeRuta(ElementoCanvas el, string ruta)
    {
        try
        {
            var bytes    = File.ReadAllBytes(ruta);
            var info     = new FileInfo(ruta);
            var ext      = info.Extension.ToLowerInvariant();
            var mime     = ext switch
            {
                ".png"  => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".bmp"  => "image/bmp",
                ".gif"  => "image/gif",
                _       => "image/png",
            };

            el.ImagenBytes        = bytes;
            el.ImagenNombreOrigen = info.Name;
            el.ImagenRutaOrigen   = ruta;       // solo informativo
            el.ImagenMimeType     = mime;

            _cache.Remove(el.Id); // forzar recarga desde nuevos bytes
            return true;
        }
        catch { return false; }
    }

    public void Invalidar(Guid id) => _cache.Remove(id);
    public void LimpiarTodo()
    {
        foreach (var img in _cache.Values) img.Dispose();
        _cache.Clear();
    }

    public void Dispose() => LimpiarTodo();
}
