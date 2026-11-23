using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Compara dos PDF semanticamente: estructura de objetos, diccionarios, recursos y TODAS
/// las streams decodificadas deben coincidir exactamente, y el cierre del documento
/// (tabla xref, startxref, %%EOF) debe ser valido y no tener bytes extra.
///
/// Se ignoran UNICAMENTE las fuentes de no-determinismo ya conocidas y acotadas:
///   - la compresion de las streams (se comparan los bytes decodificados, no el contenedor);
///   - /Length (longitud del contenedor comprimido); las longitudes semanticas de fuente
///     (/Length1-3) SI se comparan;
///   - la tabla xref y el startxref como valores absolutos (se validan pero no se comparan
///     entre documentos: los objetos se leen del cuerpo);
///   - el offset de zona / fechas volatiles del Info y las fechas/UUIDs del XMP,
///     siempre validados antes de ignorarlos;
///   - /ID del trailer y los valores /CreationDate /ModDate del objeto Info (varian por segundo),
///     pero SOLO despues de validar su tipo y forma; una forma desconocida es fail-closed.
///     La PRESENCIA de /ID si se compara: solo sus valores son volatiles.
///   - el tag de subconjunto tipografico se normaliza UNICAMENTE cuando es valor de
///     /BaseFont o /FontName de un diccionario de fuente; nunca dentro de streams, strings
///     ni contenido arbitrario (un decoy /FontName/ABCDEF+ en contenido sigue siendo
///     semantico y debe fallar si cambia).
///   - las fechas/UUIDs volatiles se normalizan UNICAMENTE en la stream /Type/Metadata
///     /Subtype/XML, con validacion de forma y fail-closed.
/// Los comentarios PDF (p. ej. la version de .NET que firma PdfSharp) no forman parte del
/// modelo de objetos y no se comparan.
///
/// Cualquier forma sintactica desconocida, filtro de stream distinto de FlateDecode/flujo sin
/// filtro, /DecodeParms, object streams (ObjStm), xref streams, xref/startxref/%%EOF invalidos,
/// bytes despues de %%EOF o actualizaciones incrementales provocan fallo rapido (fail-closed):
/// un PdfSharp distinto o un PDF manipulado debe romper el gate, nunca pasar silenciosamente.
/// </summary>
internal static class PdfStructuralComparador
{
    public static void AssertEquivalent(byte[] pdfReferencia, byte[] pdfCandidato, string contexto)
    {
        string referencia = Canonicalizar(pdfReferencia);
        string candidato = Canonicalizar(pdfCandidato);
        if (string.Equals(referencia, candidato, StringComparison.Ordinal)) return;
        throw new PdfStructuralMismatchException(
            $"El PDF candidato de '{contexto}' no es semanticamente equivalente al PDF de referencia EF8."
            + Environment.NewLine + PrimeraDiferencia(referencia, candidato));
    }

    internal static string Canonicalizar(byte[] pdf)
    {
        if (pdf.Length == 0)
            throw new InvalidDataException("El PDF esta vacio.");

        // Una unica pasada sobre el PDF crudo: valida estructura/cierre/xref y la forma de los
        // metadatos volatiles antes de ignorarlos. Los tags de subconjunto y el XMP se
        // normalizan despues, SOLO en sus campos reales (nunca por regex global del archivo).
        PdfDocumento documento = new LectorPdf(Encoding.Latin1.GetString(pdf)).LeerDocumento();
        return LectorPdf.Serializar(documento);
    }

    private static string PrimeraDiferencia(string referencia, string candidato)
    {
        string[] lineasRef = referencia.Split('\n');
        string[] lineasCan = candidato.Split('\n');
        int maximo = Math.Max(lineasRef.Length, lineasCan.Length);
        for (int i = 0; i < maximo; i++)
        {
            string r = i < lineasRef.Length ? lineasRef[i] : "<sin linea>";
            string c = i < lineasCan.Length ? lineasCan[i] : "<sin linea>";
            if (!string.Equals(r, c, StringComparison.Ordinal))
            {
                return $"Primera diferencia en la linea {i + 1}:"
                    + $"{Environment.NewLine}  EF8:       {Truncar(r)}"
                    + $"{Environment.NewLine}  candidato: {Truncar(c)}";
            }
        }
        return "Los documentos canonicos difieren solo en la longitud total.";
    }

    private static string Truncar(string valor) =>
        valor.Length <= 500 ? valor : valor.Substring(0, 500) + "...";
}

internal sealed class PdfStructuralMismatchException : Exception
{
    public PdfStructuralMismatchException(string message) : base(message) { }
}

internal abstract class PdfValor
{
}

internal sealed class PdfNulo : PdfValor
{
    public static readonly PdfNulo Instancia = new();
    private PdfNulo() { }
}

internal sealed class PdfBooleano : PdfValor
{
    public PdfBooleano(bool valor) => Valor = valor;
    public bool Valor { get; }
}

internal sealed class PdfNumero : PdfValor
{
    public PdfNumero(decimal valor) => Valor = valor;
    public decimal Valor { get; }
}

internal sealed class PdfNombre : PdfValor
{
    public PdfNombre(string valor) => Valor = valor;
    public string Valor { get; }
}

internal sealed class PdfCadena : PdfValor
{
    public PdfCadena(byte[] bytes) => Bytes = bytes;
    public byte[] Bytes { get; }
}

internal sealed class PdfArreglo : PdfValor
{
    public PdfArreglo(List<PdfValor> items) => Items = items;
    public List<PdfValor> Items { get; }
}

internal sealed class PdfDiccionario : PdfValor
{
    public PdfDiccionario(SortedDictionary<string, PdfValor> entradas) => Entradas = entradas;
    public SortedDictionary<string, PdfValor> Entradas { get; }

    public bool TryGet(string clave, out PdfValor valor) => Entradas.TryGetValue(clave, out valor!);
}

internal sealed class PdfReferencia : PdfValor
{
    public PdfReferencia(int numero, int generacion)
    {
        Numero = numero;
        Generacion = generacion;
    }

    public int Numero { get; }
    public int Generacion { get; }
}

internal sealed class PdfStream : PdfValor
{
    public PdfStream(PdfDiccionario diccionario, byte[] datos)
    {
        Diccionario = diccionario;
        Datos = datos;
    }

    public PdfDiccionario Diccionario { get; }
    public byte[] Datos { get; }
}

internal sealed class PdfDocumento
{
    public PdfDocumento(
        string version,
        IReadOnlyList<KeyValuePair<(int Numero, int Generacion), PdfValor>> objetos,
        PdfDiccionario trailer,
        (int Numero, int Generacion)? info)
    {
        Version = version;
        Objetos = objetos;
        Trailer = trailer;
        Info = info;
    }

    public string Version { get; }
    public IReadOnlyList<KeyValuePair<(int Numero, int Generacion), PdfValor>> Objetos { get; }
    public PdfDiccionario Trailer { get; }
    public (int Numero, int Generacion)? Info { get; }
}

/// <summary>
/// Lector secuencial minimo de la sintaxis PDF sobre Latin1 (1 char = 1 byte). No usa la
/// tabla xref para localizar objetos, pero SI la valida junto con startxref y %%EOF, y
/// rechaza cualquier byte extra despues del cierre.
/// </summary>
internal sealed class LectorPdf
{
    private static readonly string[] ClavesStreamIgnoradas =
        { "Length", "Filter", "DecodeParms", "F", "FFilter" };

    /// <summary>Unica forma de fecha que emite PdfSharp en el Info (offset, nunca sufijo Z).</summary>
    private static readonly Regex FormaFechaInfo = new(
        "^D:\\d{14}[+-]\\d{2}'\\d{2}'$", RegexOptions.Compiled);

    private readonly string _texto;
    private int _i;

    public LectorPdf(string texto) => _texto = texto;

    public PdfDocumento LeerDocumento()
    {
        string version = LeerCabecera();
        var objetos = new Dictionary<(int Numero, int Generacion), PdfValor>();
        var offsets = new Dictionary<(int Numero, int Generacion), int>();

        while (true)
        {
            SaltarEspaciosYComentarios();
            if (_i >= _texto.Length)
                throw new InvalidDataException("El PDF termina sin tabla xref ni trailer.");
            if (EsPalabraSiguiente("xref") || EsPalabraSiguiente("trailer")) break;

            int offsetObjeto = _i;
            if (!TryLeerEntero(out int numero))
                throw new InvalidDataException(
                    $"Se esperaba un objeto PDF ('N G obj') en el offset {_i} y se encontro '{Muestra(_i)}'.");
            SaltarEspaciosYComentarios();
            int generacion = LeerEntero();
            SaltarEspaciosYComentarios();
            if (LeerPalabra() != "obj")
                throw new InvalidDataException($"Se esperaba la palabra clave 'obj' en el offset {_i}.");

            PdfValor valor = LeerValor();
            SaltarEspaciosYComentarios();
            if (EsPalabraSiguiente("stream"))
            {
                LeerPalabra();
                valor = LeerStream((PdfDiccionario)valor);
                SaltarEspaciosYComentarios();
            }

            if (LeerPalabra() != "endobj")
                throw new InvalidDataException($"Se esperaba 'endobj' en el offset {_i}.");
            if (!objetos.TryAdd((numero, generacion), valor))
                throw new InvalidDataException($"Objeto PDF duplicado: {numero} {generacion} obj.");
            offsets[(numero, generacion)] = offsetObjeto;
            VerificarTipoSoportado(valor, numero, generacion);
        }

        SaltarEspaciosYComentarios();
        int inicioXref = _i;
        if (!CoincidePalabra("xref"))
            throw new InvalidDataException(
                "El PDF no contiene tabla xref clasica despues de los objetos (fail-closed).");

        SortedDictionary<int, EntradaXref> entradas = LeerEntradasXref();
        if (!CoincidePalabra("trailer"))
            throw new InvalidDataException(
                "La tabla xref debe terminar en el diccionario trailer, sin datos intermedios.");

        SaltarEspaciosYComentarios();
        if (LeerValor() is not PdfDiccionario trailer)
            throw new InvalidDataException("El trailer no es un diccionario PDF.");

        ValidarCierre(inicioXref, trailer, entradas, offsets);

        (int Numero, int Generacion)? info = ValidarMetadatosVolatiles(trailer, objetos);

        var ordenados = new List<KeyValuePair<(int Numero, int Generacion), PdfValor>>(objetos.Count);
        foreach (var par in objetos.OrderBy(kv => kv.Key.Numero).ThenBy(kv => kv.Key.Generacion))
            ordenados.Add(par);
        return new PdfDocumento(version, ordenados, trailer, info);
    }

    // ── Cierre del documento: xref, startxref y %%EOF ──────────────────────

    private SortedDictionary<int, EntradaXref> LeerEntradasXref()
    {
        var entradas = new SortedDictionary<int, EntradaXref>();
        while (true)
        {
            SaltarEspaciosYComentarios();
            if (_i >= _texto.Length)
                throw new InvalidDataException("La tabla xref no termina en 'trailer'.");
            if (EsPalabraSiguiente("trailer")) return entradas;

            if (!TryLeerEntero(out int primera) || primera < 0)
                throw new InvalidDataException($"Subseccion xref mal formada en el offset {_i}.");
            SaltarEspaciosYComentarios();
            if (!TryLeerEntero(out int cantidad) || cantidad <= 0)
                throw new InvalidDataException($"Subseccion xref sin cantidad valida en el offset {_i}.");

            for (int k = 0; k < cantidad; k++)
            {
                SaltarEspaciosYComentarios();
                if (!TryLeerDigitosFijos(10, out long offset))
                    throw new InvalidDataException($"Entrada xref sin offset de 10 digitos en el offset {_i}.");
                SaltarEspaciosYComentarios();
                if (!TryLeerDigitosFijos(5, out long generacion))
                    throw new InvalidDataException($"Entrada xref sin generacion de 5 digitos en el offset {_i}.");
                SaltarEspaciosYComentarios();
                if (_i >= _texto.Length || (_texto[_i] != 'n' && _texto[_i] != 'f'))
                    throw new InvalidDataException($"Entrada xref sin bandera 'n'/'f' en el offset {_i}.");
                bool enUso = _texto[_i] == 'n';
                _i++;

                int numero = primera + k;
                if (!entradas.TryAdd(numero, new EntradaXref(offset, (int)generacion, enUso)))
                    throw new InvalidDataException($"La tabla xref declara dos veces el objeto {numero}.");
            }
        }
    }

    private void ValidarCierre(
        int inicioXref,
        PdfDiccionario trailer,
        SortedDictionary<int, EntradaXref> entradas,
        Dictionary<(int Numero, int Generacion), int> offsets)
    {
        if (trailer.Entradas.ContainsKey("Prev") || trailer.Entradas.ContainsKey("XRefStm"))
            throw new NotSupportedException(
                "El PDF usa actualizaciones incrementales (/Prev o /XRefStm); no se admite en el comparador.");

        if (!trailer.TryGet("Size", out PdfValor sizeValor)
            || sizeValor is not PdfNumero sizeNumero
            || sizeNumero.Valor != decimal.Truncate(sizeNumero.Valor)
            || sizeNumero.Valor < 1
            || sizeNumero.Valor > int.MaxValue)
        {
            throw new InvalidDataException("El trailer no declara /Size entero valido.");
        }

        int size = (int)sizeNumero.Valor;
        if (entradas.Count != size || !entradas.ContainsKey(0) || !entradas.ContainsKey(size - 1))
            throw new InvalidDataException(
                $"La tabla xref esta incompleta: /Size={size} pero hay {entradas.Count} entradas "
                + $"({(entradas.Count > 0 ? entradas.Keys.Min() : -1)}..{(entradas.Count > 0 ? entradas.Keys.Max() : -1)}).");

        var porNumero = new Dictionary<int, (int Generacion, int Offset)>();
        foreach (var par in offsets)
        {
            if (!porNumero.TryAdd(par.Key.Numero, (par.Key.Generacion, par.Value)))
                throw new InvalidDataException($"Dos generaciones del objeto {par.Key.Numero} en el cuerpo.");
        }

        foreach (var entrada in entradas)
        {
            if (entrada.Key == 0)
            {
                if (entrada.Value.EnUso)
                    throw new InvalidDataException("La tabla xref marca el objeto 0 como en uso.");
                continue;
            }

            bool existe = porNumero.TryGetValue(entrada.Key, out var objeto);
            if (entrada.Value.EnUso)
            {
                if (!existe)
                    throw new InvalidDataException(
                        $"La tabla xref declara en uso el objeto {entrada.Key}, que no existe en el cuerpo.");
                if (objeto.Generacion != entrada.Value.Generacion || objeto.Offset != entrada.Value.Offset)
                    throw new InvalidDataException(
                        $"La entrada xref del objeto {entrada.Key} no coincide con el cuerpo "
                        + $"(xref {entrada.Value.Generacion} {entrada.Value.Offset}, "
                        + $"cuerpo {objeto.Generacion} {objeto.Offset}).");
            }
            else if (existe)
            {
                throw new InvalidDataException(
                    $"La tabla xref declara libre el objeto {entrada.Key}, pero existe en el cuerpo.");
            }
        }

        foreach (int numero in porNumero.Keys)
        {
            if (!entradas.TryGetValue(numero, out EntradaXref entrada) || !entrada.EnUso)
                throw new InvalidDataException($"El objeto {numero} del cuerpo no esta en uso en la tabla xref.");
        }

        SaltarEspaciosYComentarios();
        if (!CoincidePalabra("startxref"))
            throw new InvalidDataException("Falta 'startxref' despues del trailer.");
        SaltarEspaciosYComentarios();
        if (!TryLeerEnteroLargo(out long startXref))
            throw new InvalidDataException("'startxref' sin offset entero valido.");
        if (startXref != inicioXref)
            throw new InvalidDataException(
                $"startxref ({startXref}) no apunta a la tabla xref real ({inicioXref}).");

        SaltarBlancos();
        if (!_texto.AsSpan(_i).StartsWith("%%EOF".AsSpan(), StringComparison.Ordinal))
            throw new InvalidDataException("Falta '%%EOF' al final del PDF.");
        _i += 5;

        while (_i < _texto.Length)
        {
            if (!EsBlanco(_texto[_i]))
                throw new InvalidDataException(
                    "Hay datos despues de '%%EOF' (posible actualizacion incremental o PDF manipulado).");
            _i++;
        }
    }

    /// <summary>
    /// Valida tipo y forma de los metadatos volatiles ANTES de ignorarlos: fechas del objeto
    /// Info (string PDF con forma D:yyyyMMddHHmmss±HH'mm') y /ID del trailer (arreglo de dos
    /// cadenas de 16 bytes). Una forma desconocida falla en lugar de silenciarse.
    /// </summary>
    private static (int Numero, int Generacion)? ValidarMetadatosVolatiles(
        PdfDiccionario trailer, Dictionary<(int Numero, int Generacion), PdfValor> objetos)
    {
        if (trailer.TryGet("ID", out PdfValor id))
        {
            if (id is not PdfArreglo arreglo
                || arreglo.Items.Count != 2
                || arreglo.Items[0] is not PdfCadena primera
                || arreglo.Items[1] is not PdfCadena segunda
                || primera.Bytes.Length != 16
                || segunda.Bytes.Length != 16)
            {
                throw new InvalidDataException(
                    "El /ID del trailer debe ser un arreglo de dos cadenas de 16 bytes.");
            }
        }

        if (!trailer.TryGet("Info", out PdfValor infoValor)) return null;
        if (infoValor is not PdfReferencia infoRef)
            throw new InvalidDataException("El /Info del trailer debe ser una referencia indirecta.");
        if (!objetos.TryGetValue((infoRef.Numero, infoRef.Generacion), out PdfValor? infoObjeto)
            || infoObjeto is not PdfDiccionario info)
        {
            throw new InvalidDataException(
                $"El trailer referencia /Info {infoRef.Numero} {infoRef.Generacion} R, "
                + "pero el objeto no existe o no es un diccionario.");
        }

        foreach (string clave in new[] { "CreationDate", "ModDate" })
        {
            if (!info.TryGet(clave, out PdfValor valor)) continue;
            if (valor is not PdfCadena cadena)
                throw new InvalidDataException($"Info /{clave} debe ser una cadena PDF, no {valor.GetType().Name}.");
            string fecha = Encoding.Latin1.GetString(cadena.Bytes);
            if (!FormaFechaInfo.IsMatch(fecha))
                throw new InvalidDataException(
                    $"Info /{clave} con forma desconocida: '{fecha}' "
                    + "(se espera D:yyyyMMddHHmmss±HH'mm').");
        }

        return (infoRef.Numero, infoRef.Generacion);
    }

    // ── Cabecera y estructura ───────────────────────────────────────────────

    private string LeerCabecera()
    {
        if (_texto.Length < 8 || !_texto.StartsWith("%PDF-", StringComparison.Ordinal))
            throw new InvalidDataException("Cabecera PDF (%PDF-) ausente: no parece un PDF.");
        _i = 5;
        int inicio = _i;
        while (_i < _texto.Length && !EsBlanco(_texto[_i])) _i++;
        string version = _texto.Substring(inicio, _i - inicio);
        if (version.Length == 0)
            throw new InvalidDataException("La cabecera PDF no declara version.");
        while (_i < _texto.Length && _texto[_i] != '\r' && _texto[_i] != '\n') _i++;
        return version;
    }

    private static void VerificarTipoSoportado(PdfValor valor, int numero, int generacion)
    {
        PdfDiccionario? dicc = valor as PdfDiccionario ?? (valor as PdfStream)?.Diccionario;
        if (dicc is not null
            && dicc.TryGet("Type", out PdfValor tipo)
            && tipo is PdfNombre nombre
            && (nombre.Valor == "ObjStm" || nombre.Valor == "XRef"))
        {
            throw new NotSupportedException(
                $"El objeto {numero} {generacion} usa /Type/{nombre.Valor}; el comparador solo admite "
                + "objetos directos y tabla xref clasica (fail-closed).");
        }
    }

    private bool CoincidePalabra(string palabra)
    {
        int guardado = _i;
        string leida = LeerPalabra();
        if (leida == palabra) return true;
        _i = guardado;
        return false;
    }

    private bool EsPalabraSiguiente(string palabra)
    {
        int guardado = _i;
        bool coincide = LeerPalabra() == palabra;
        _i = guardado;
        return coincide;
    }

    private string LeerPalabra()
    {
        int inicio = _i;
        while (_i < _texto.Length)
        {
            char c = _texto[_i];
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) _i++;
            else break;
        }
        return _texto.Substring(inicio, _i - inicio);
    }

    private void SaltarEspaciosYComentarios()
    {
        while (_i < _texto.Length)
        {
            char c = _texto[_i];
            if (EsBlanco(c))
            {
                _i++;
                continue;
            }
            if (c == '%')
            {
                while (_i < _texto.Length && _texto[_i] != '\r' && _texto[_i] != '\n') _i++;
                continue;
            }
            break;
        }
    }

    private void SaltarBlancos()
    {
        while (_i < _texto.Length && EsBlanco(_texto[_i])) _i++;
    }

    // ── Valores ────────────────────────────────────────────────────────────

    private PdfValor LeerValor()
    {
        SaltarEspaciosYComentarios();
        if (_i >= _texto.Length)
            throw new InvalidDataException("Valor PDF truncado al final del archivo.");

        char c = _texto[_i];
        if (c == '<')
        {
            if (_i + 1 < _texto.Length && _texto[_i + 1] == '<') return LeerDiccionario();
            return LeerCadenaHex();
        }
        if (c == '[') return LeerArreglo();
        if (c == '(') return LeerCadenaLiteral();
        if (c == '/') return LeerNombre();
        if (c == '+' || c == '-' || c == '.' || (c >= '0' && c <= '9')) return LeerNumeroOReferencia();

        int guardado = _i;
        string palabra = LeerPalabra();
        switch (palabra)
        {
            case "true": return new PdfBooleano(true);
            case "false": return new PdfBooleano(false);
            case "null": return PdfNulo.Instancia;
            case "": break;
        }
        _i = guardado;
        throw new InvalidDataException(
            $"Forma PDF desconocida en el offset {_i}: '{Muestra(_i)}'.");
    }

    private PdfValor LeerNumeroOReferencia()
    {
        int inicio = _i;
        PdfNumero numero = LeerNumero();
        string token = _texto.Substring(inicio, _i - inicio);
        if (token.Contains('.') || token.Contains('+') || token.Contains('-')) return numero;
        if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int objeto))
            return numero;

        int guardado = _i;
        SaltarEspaciosYComentarios();
        if (TryLeerEntero(out int generacion))
        {
            SaltarEspaciosYComentarios();
            if (LeerPalabra() == "R") return new PdfReferencia(objeto, generacion);
        }
        _i = guardado;
        return numero;
    }

    private PdfNumero LeerNumero()
    {
        int inicio = _i;
        bool punto = false;
        bool digito = false;
        while (_i < _texto.Length)
        {
            char c = _texto[_i];
            if (c >= '0' && c <= '9')
            {
                digito = true;
                _i++;
            }
            else if (c == '.')
            {
                if (punto)
                    throw new InvalidDataException($"Numero PDF mal formado en el offset {inicio}: '{Muestra(inicio)}'.");
                punto = true;
                _i++;
            }
            else if ((c == '+' || c == '-') && _i == inicio)
            {
                _i++;
            }
            else break;
        }

        string token = _texto.Substring(inicio, _i - inicio);
        if (!digito || token is "+" or "-" or ".")
            throw new InvalidDataException($"Numero PDF mal formado en el offset {inicio}: '{token}'.");
        decimal valor = decimal.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
        return new PdfNumero(valor);
    }

    private PdfDiccionario LeerDiccionario()
    {
        _i += 2;
        var entradas = new SortedDictionary<string, PdfValor>(StringComparer.Ordinal);
        while (true)
        {
            SaltarEspaciosYComentarios();
            if (_i >= _texto.Length)
                throw new InvalidDataException("Diccionario PDF truncado.");
            if (_texto[_i] == '>' && _i + 1 < _texto.Length && _texto[_i + 1] == '>')
            {
                _i += 2;
                return new PdfDiccionario(entradas);
            }
            if (_texto[_i] != '/')
                throw new InvalidDataException(
                    $"Clave de diccionario PDF no es un nombre en el offset {_i}: '{Muestra(_i)}'.");
            PdfNombre clave = LeerNombre();
            SaltarEspaciosYComentarios();
            PdfValor valor = LeerValor();
            if (!entradas.TryAdd(clave.Valor, valor))
                throw new InvalidDataException($"Clave duplicada '/{clave.Valor}' en diccionario PDF.");
        }
    }

    private PdfArreglo LeerArreglo()
    {
        _i++;
        var items = new List<PdfValor>();
        while (true)
        {
            SaltarEspaciosYComentarios();
            if (_i >= _texto.Length)
                throw new InvalidDataException("Arreglo PDF truncado.");
            if (_texto[_i] == ']')
            {
                _i++;
                return new PdfArreglo(items);
            }
            items.Add(LeerValor());
        }
    }

    private PdfNombre LeerNombre()
    {
        if (_i >= _texto.Length || _texto[_i] != '/')
            throw new InvalidDataException($"Se esperaba un nombre PDF en el offset {_i}.");
        _i++;
        var sb = new StringBuilder();
        while (_i < _texto.Length)
        {
            char c = _texto[_i];
            if (EsDelimitador(c)) break;
            if (c == '#' && _i + 2 < _texto.Length && EsHex(_texto[_i + 1]) && EsHex(_texto[_i + 2]))
            {
                sb.Append((char)Convert.ToByte(_texto.Substring(_i + 1, 2), 16));
                _i += 3;
            }
            else
            {
                sb.Append(c);
                _i++;
            }
        }
        return new PdfNombre(sb.ToString());
    }

    private PdfCadena LeerCadenaLiteral()
    {
        _i++;
        var bytes = new List<byte>();
        int profundidad = 1;
        while (_i < _texto.Length)
        {
            char c = _texto[_i++];
            if (c == '\\')
            {
                if (_i >= _texto.Length)
                    throw new InvalidDataException("Cadena PDF truncada tras '\\'.");
                char escapado = _texto[_i++];
                switch (escapado)
                {
                    case 'n': bytes.Add(10); break;
                    case 'r': bytes.Add(13); break;
                    case 't': bytes.Add(9); break;
                    case 'b': bytes.Add(8); break;
                    case 'f': bytes.Add(12); break;
                    case '(': bytes.Add(40); break;
                    case ')': bytes.Add(41); break;
                    case '\\': bytes.Add(92); break;
                    case '\r':
                        if (_i < _texto.Length && _texto[_i] == '\n') _i++;
                        break;
                    case '\n':
                        break;
                    default:
                        if (escapado >= '0' && escapado <= '7')
                        {
                            int valor = escapado - '0';
                            for (int k = 0; k < 2 && _i < _texto.Length
                                && _texto[_i] >= '0' && _texto[_i] <= '7'; k++)
                            {
                                valor = valor * 8 + (_texto[_i] - '0');
                                _i++;
                            }
                            bytes.Add((byte)(valor & 0xFF));
                        }
                        else
                        {
                            bytes.Add((byte)escapado);
                        }
                        break;
                }
                continue;
            }

            if (c == '(')
            {
                profundidad++;
                bytes.Add(40);
                continue;
            }
            if (c == ')')
            {
                profundidad--;
                if (profundidad == 0) return new PdfCadena(bytes.ToArray());
                bytes.Add(41);
                continue;
            }
            bytes.Add((byte)c);
        }
        throw new InvalidDataException("Cadena literal PDF sin cierre ')'.");
    }

    private PdfCadena LeerCadenaHex()
    {
        _i++;
        var digitos = new StringBuilder();
        while (_i < _texto.Length)
        {
            char c = _texto[_i++];
            if (c == '>')
            {
                if (digitos.Length % 2 == 1) digitos.Append('0');
                var bytes = new byte[digitos.Length / 2];
                for (int k = 0; k < bytes.Length; k++)
                    bytes[k] = Convert.ToByte(digitos.ToString(k * 2, 2), 16);
                return new PdfCadena(bytes);
            }
            if (EsBlanco(c)) continue;
            if (!EsHex(c))
                throw new InvalidDataException($"Cadena hexadecimal PDF invalida: '{c}'.");
            digitos.Append(c);
        }
        throw new InvalidDataException("Cadena hexadecimal PDF sin cierre '>'.");
    }

    // ── Streams ────────────────────────────────────────────────────────────

    private PdfStream LeerStream(PdfDiccionario diccionario)
    {
        if (_i >= _texto.Length)
            throw new InvalidDataException("Stream PDF truncado.");
        if (_texto[_i] == '\r')
        {
            _i++;
            if (_i < _texto.Length && _texto[_i] == '\n') _i++;
        }
        else if (_texto[_i] == '\n')
        {
            _i++;
        }
        else
        {
            throw new InvalidDataException($"La palabra 'stream' no termina en salto de linea (offset {_i}).");
        }

        if (!diccionario.TryGet("Length", out PdfValor largo)
            || largo is not PdfNumero numero
            || numero.Valor != decimal.Truncate(numero.Valor)
            || numero.Valor < 0
            || numero.Valor > int.MaxValue)
        {
            throw new InvalidDataException("Stream PDF sin /Length entero directo: forma no soportada.");
        }

        int longitud = (int)numero.Valor;
        if (_i + longitud > _texto.Length)
            throw new InvalidDataException("El /Length del stream excede el archivo.");
        byte[] crudos = Encoding.Latin1.GetBytes(_texto.Substring(_i, longitud));
        _i += longitud;

        if (_i < _texto.Length && _texto[_i] == '\r')
        {
            _i++;
            if (_i < _texto.Length && _texto[_i] == '\n') _i++;
        }
        else if (_i < _texto.Length && _texto[_i] == '\n')
        {
            _i++;
        }

        if (LeerPalabra() != "endstream")
            throw new InvalidDataException("Stream PDF sin 'endstream' tras /Length bytes.");

        return new PdfStream(diccionario, DecodificarStream(diccionario, crudos));
    }

    private static byte[] DecodificarStream(PdfDiccionario diccionario, byte[] crudos)
    {
        if (diccionario.Entradas.ContainsKey("F") || diccionario.Entradas.ContainsKey("FFilter"))
            throw new NotSupportedException("Stream PDF externo (/F o /FFilter) no soportado por el comparador.");
        if (diccionario.TryGet("DecodeParms", out PdfValor parms) && parms is not PdfNulo)
            throw new NotSupportedException("/DecodeParms no soportado por el comparador (fail-closed).");

        var filtros = new List<string>();
        if (diccionario.TryGet("Filter", out PdfValor filtro))
        {
            if (filtro is PdfNombre nombre)
            {
                filtros.Add(nombre.Valor);
            }
            else if (filtro is PdfArreglo arreglo)
            {
                foreach (PdfValor item in arreglo.Items)
                {
                    if (item is not PdfNombre filtroNombre)
                        throw new NotSupportedException("Lista /Filter con un valor que no es nombre.");
                    filtros.Add(filtroNombre.Valor);
                }
            }
            else
            {
                throw new NotSupportedException("Forma de /Filter no soportada por el comparador.");
            }
        }

        if (filtros.Count == 0) return crudos;
        if (filtros.Count == 1 && filtros[0] == "FlateDecode") return Inflar(crudos);
        throw new NotSupportedException(
            $"Filtro de stream no soportado: {string.Join(",", filtros)} (solo FlateDecode o sin filtro).");
    }

    private static byte[] Inflar(byte[] crudos)
    {
        try
        {
            using var entrada = new MemoryStream(crudos);
            using var zlib = new ZLibStream(entrada, CompressionMode.Decompress);
            using var salida = new MemoryStream();
            zlib.CopyTo(salida);
            return salida.ToArray();
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException("Stream FlateDecode invalida.", ex);
        }
    }

    // ── Canonicalizacion ───────────────────────────────────────────────────

    internal static string Serializar(PdfDocumento documento)
    {
        var sb = new StringBuilder();
        sb.Append("pdf:").Append(documento.Version).Append('\n');
        foreach (var par in documento.Objetos)
        {
            bool esInfo = documento.Info is not null && documento.Info.Value == par.Key;
            sb.Append("obj ")
                .Append(par.Key.Numero).Append(' ').Append(par.Key.Generacion).Append(' ')
                .Append(SerializarValor(par.Value, esInfo))
                .Append('\n');
        }
        sb.Append("trailer ")
            .Append(SerializarDiccionario(
                documento.Trailer, false, Array.Empty<string>(), conservarIdComoVolatil: true))
            .Append('\n');
        return sb.ToString();
    }

    private static string SerializarValor(PdfValor valor, bool esInfo)
    {
        switch (valor)
        {
            case PdfNulo: return "null";
            case PdfBooleano booleano: return booleano.Valor ? "true" : "false";
            case PdfNumero numero: return "num:" + NormalizarNumero(numero.Valor);
            case PdfNombre nombre: return "name:" + EscaparNombre(nombre.Valor);
            case PdfCadena cadena: return "str:" + Convert.ToHexString(cadena.Bytes);
            case PdfReferencia referencia: return $"ref:{referencia.Numero}:{referencia.Generacion}";
            case PdfArreglo arreglo:
                return "[" + string.Join(",", arreglo.Items.Select(item => SerializarValor(item, false))) + "]";
            case PdfDiccionario diccionario:
                return SerializarDiccionario(diccionario, esInfo, Array.Empty<string>());
            case PdfStream stream:
                return "stream{"
                    + SerializarDiccionario(stream.Diccionario, false, ClavesStreamIgnoradas)
                    + "}#" + Convert.ToHexString(SHA256.HashData(NormalizarXmpSiMetadata(stream)));
            default:
                throw new InvalidDataException($"Valor PDF no reconocido: {valor.GetType().Name}.");
        }
    }

    private static string SerializarDiccionario(
        PdfDiccionario diccionario,
        bool esInfo,
        IReadOnlyCollection<string> clavesIgnoradas,
        bool conservarIdComoVolatil = false)
    {
        bool esDiccionarioFuente = diccionario.TryGet("Type", out PdfValor tipoDic)
            && tipoDic is PdfNombre tipoDicNombre
            && tipoDicNombre.Valor == "Font";
        bool esFontDescriptor = diccionario.TryGet("Type", out PdfValor tipoDesc)
            && tipoDesc is PdfNombre tipoDescNombre
            && tipoDescNombre.Valor == "FontDescriptor";

        var partes = new List<string>();
        foreach (var entrada in diccionario.Entradas)
        {
            if (clavesIgnoradas.Contains(entrada.Key)) continue;
            if (conservarIdComoVolatil && entrada.Key == "ID")
            {
                partes.Add(EscaparNombre(entrada.Key) + "=<volatil>");
                continue;
            }
            if (esInfo && (entrada.Key == "CreationDate" || entrada.Key == "ModDate"))
            {
                partes.Add(EscaparNombre(entrada.Key) + "=<volatil>");
                continue;
            }
            if (entrada.Key == "BaseFont" && esDiccionarioFuente
                && entrada.Value is PdfNombre baseFont)
            {
                partes.Add(EscaparNombre(entrada.Key) + "=name:"
                    + EscaparNombre(NormalizarTagFuente(baseFont.Valor)));
                continue;
            }
            if (entrada.Key == "FontName" && esFontDescriptor
                && entrada.Value is PdfNombre fontName)
            {
                partes.Add(EscaparNombre(entrada.Key) + "=name:"
                    + EscaparNombre(NormalizarTagFuente(fontName.Valor)));
                continue;
            }
            partes.Add(EscaparNombre(entrada.Key) + "=" + SerializarValor(entrada.Value, false));
        }
        return "{" + string.Join(";", partes) + "}";
    }

    private static string NormalizarNumero(decimal valor)
    {
        if (valor == 0) return "0";
        return valor.ToString("0.############################", CultureInfo.InvariantCulture);
    }

    private static string EscaparNombre(string valor)
    {
        var sb = new StringBuilder("/");
        foreach (char c in valor)
        {
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
                || c == '_' || c == '-' || c == '+' || c == '.')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append("\\x").Append(((int)(byte)c).ToString("X2", CultureInfo.InvariantCulture));
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Neutraliza el tag de subconjunto SOLO cuando el valor es un nombre de fuente real
    /// (campo /BaseFont o /FontName). No se aplica a strings ni contenido arbitrario.
    /// </summary>
    private static string NormalizarTagFuente(string valor)
    {
        if (valor.Length > 7
            && valor[0] >= 'A' && valor[0] <= 'Z'
            && valor[1] >= 'A' && valor[1] <= 'Z'
            && valor[2] >= 'A' && valor[2] <= 'Z'
            && valor[3] >= 'A' && valor[3] <= 'Z'
            && valor[4] >= 'A' && valor[4] <= 'Z'
            && valor[5] >= 'A' && valor[5] <= 'Z'
            && valor[6] == '+')
        {
            return "SOPROX+" + valor.Substring(7);
        }
        return valor;
    }

    private const string FechaXmpFija = "2000-01-01T00:00:00+00:00";
    private const string UuidFijo = "uuid:00000000-0000-0000-0000-000000000000";

    private static readonly XNamespace XmpNs = "http://ns.adobe.com/xap/1.0/";
    private static readonly XNamespace XmpMmNs = "http://ns.adobe.com/xap/1.0/mm/";
    private static readonly XNamespace StEvtNs =
        "http://ns.adobe.com/xap/1.0/sType/ResourceEvent#";
    private static readonly XNamespace RdfNs = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    /// <summary>Unica forma legacy de fecha XMP emitida por PdfSharp (año 20xx, offset, nunca Z).</summary>
    private static readonly Regex FechaXmpLegacy = new(
        "^20\\d{2}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}[+-]\\d{2}:\\d{2}$",
        RegexOptions.Compiled);

    private static readonly Regex UuidXmp = new(
        "^uuid:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        RegexOptions.Compiled);

    private static readonly Regex FechaXmpEnTexto = new(
        "20\\d{2}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}(?:Z|[+-]\\d{2}:\\d{2})",
        RegexOptions.Compiled);

    private static readonly Regex UuidEnTexto = new(
        "uuid:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        RegexOptions.Compiled);

    private sealed record ElementoXmpVolatil(
        XName Nombre, string EtiquetaCruda, string ValorFijo, Regex Forma);

    private static readonly ElementoXmpVolatil[] ElementosXmpVolatiles =
    {
        new(XmpNs + "CreateDate", "xmp:CreateDate", FechaXmpFija, FechaXmpLegacy),
        new(XmpNs + "ModifyDate", "xmp:ModifyDate", FechaXmpFija, FechaXmpLegacy),
        new(XmpNs + "MetadataDate", "xmp:MetadataDate", FechaXmpFija, FechaXmpLegacy),
        new(XmpMmNs + "DocumentID", "xmpMM:DocumentID", UuidFijo, UuidXmp),
        new(XmpMmNs + "InstanceID", "xmpMM:InstanceID", UuidFijo, UuidXmp),
        new(StEvtNs + "when", "stEvt:when", FechaXmpFija, FechaXmpLegacy),
        new(StEvtNs + "instanceID", "stEvt:instanceID", UuidFijo, UuidXmp),
    };

    private static bool EsMetadataXml(PdfDiccionario diccionario) =>
        diccionario.TryGet("Type", out PdfValor tipo)
        && tipo is PdfNombre nombreTipo
        && nombreTipo.Valor == "Metadata"
        && diccionario.TryGet("Subtype", out PdfValor subtipo)
        && subtipo is PdfNombre nombreSubtipo
        && nombreSubtipo.Valor == "XML";

    /// <summary>
    /// XDocument se usa SOLO para validar estructura. Los bytes del XMP se conservan y
    /// unicamente se sustituyen los valores volatiles validados, con la misma longitud que
    /// la forma legacy. Se rechaza fail-closed: DOCTYPE, comentarios, CDATA, instrucciones
    /// de proceso ajenas a xpacket, elementos conocidos fuera de rdf:Description, con
    /// atributos, contenido anidado, entidades/forma ambigua, etiquetas legacy no
    /// identificables sin ambiguedad, y fechas que no sean la forma exacta con offset.
    /// </summary>
    private static byte[] NormalizarXmpSiMetadata(PdfStream stream)
    {
        if (!EsMetadataXml(stream.Diccionario)) return stream.Datos;

        string texto = Encoding.Latin1.GetString(stream.Datos);
        XDocument documento;
        try
        {
            documento = XDocument.Parse(texto, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("XMP no es XML bien formado (fail-closed).", ex);
        }

        if (documento.DocumentType is not null)
            throw new InvalidDataException("XMP con DOCTYPE no admitido (fail-closed).");

        foreach (XNode nodo in documento.Nodes().Concat(documento.DescendantNodes()))
        {
            switch (nodo)
            {
                case XComment:
                    throw new InvalidDataException("XMP con comentarios no admitido (fail-closed).");
                case XCData:
                    throw new InvalidDataException("XMP con CDATA no admitido (fail-closed).");
                case XProcessingInstruction instruccion when instruccion.Target != "xpacket":
                    throw new InvalidDataException(
                        $"XMP con instruccion de proceso no admitida: '{instruccion.Target}'.");
            }
        }

        List<(int Inicio, int Fin)> rangosPi = RangosInstruccionesProceso(texto);

        // Validacion estructural y correspondencia exacta elemento XML <-> etiqueta cruda.
        foreach (ElementoXmpVolatil especificacion in ElementosXmpVolatiles)
        {
            List<XElement> elementos = documento.Descendants()
                .Where(e => e.Name == especificacion.Nombre).ToList();
            int enCrudo = ContarFueraDePi(
                texto, "<" + especificacion.EtiquetaCruda + ">", rangosPi);
            if (elementos.Count != enCrudo)
            {
                throw new InvalidDataException(
                    $"XMP con forma ambigua para <{especificacion.EtiquetaCruda}>: "
                    + $"{elementos.Count} elemento(s) XML frente a {enCrudo} etiqueta(s) legacy.");
            }

            foreach (XElement elemento in elementos)
            {
                if (elemento.Parent?.Name != RdfNs + "Description")
                    throw new InvalidDataException(
                        $"Elemento XMP <{especificacion.EtiquetaCruda}> fuera de rdf:Description (fail-closed).");
                if (elemento.Attributes().Any())
                    throw new InvalidDataException(
                        $"Elemento XMP <{especificacion.EtiquetaCruda}> con atributos no admitidos (fail-closed).");
                if (elemento.Elements().Any())
                    throw new InvalidDataException(
                        $"Elemento XMP <{especificacion.EtiquetaCruda}> con contenido anidado (fail-closed).");
            }
        }

        // Solo se reemplaza el contenido crudo validado; el resto de bytes no se toca.
        var reemplazos = new List<(int Inicio, int Longitud, string Valor)>();
        foreach (ElementoXmpVolatil especificacion in ElementosXmpVolatiles)
        {
            List<XElement> elementos = documento.Descendants()
                .Where(e => e.Name == especificacion.Nombre).ToList();
            string etiquetaApertura = "<" + especificacion.EtiquetaCruda + ">";
            string etiquetaCierre = "</" + especificacion.EtiquetaCruda + ">";
            int cursor = 0;

            foreach (XElement elemento in elementos)
            {
                int apertura = BuscarFueraDePi(texto, etiquetaApertura, cursor, rangosPi);
                if (apertura < 0)
                    throw new InvalidDataException(
                        $"No se localizo <{especificacion.EtiquetaCruda}> en el XMP crudo (fail-closed).");

                int inicioValor = apertura + etiquetaApertura.Length;
                int cierre = texto.IndexOf(etiquetaCierre, inicioValor, StringComparison.Ordinal);
                if (cierre < 0)
                    throw new InvalidDataException(
                        $"XMP sin cierre para <{especificacion.EtiquetaCruda}> (fail-closed).");
                if (texto.IndexOf('<', inicioValor) != cierre)
                    throw new InvalidDataException(
                        $"XMP con contenido anidado/ambiguo en <{especificacion.EtiquetaCruda}> (fail-closed).");

                string crudo = texto.Substring(inicioValor, cierre - inicioValor);
                if (!string.Equals(crudo, elemento.Value, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"XMP con entidades o forma ambigua en <{especificacion.EtiquetaCruda}> (fail-closed).");
                }

                if (crudo.Length > 0)
                {
                    if (crudo.Length != especificacion.ValorFijo.Length)
                        throw new InvalidDataException(
                            $"XMP con longitud no legacy en <{especificacion.EtiquetaCruda}> (fail-closed).");
                    if (!especificacion.Forma.IsMatch(crudo))
                        throw new InvalidDataException(
                            $"XMP con forma desconocida en <{especificacion.EtiquetaCruda}>.");
                    reemplazos.Add((inicioValor, crudo.Length, especificacion.ValorFijo));
                }

                cursor = cierre + etiquetaCierre.Length;
            }
        }

        foreach (var reemplazo in reemplazos.OrderByDescending(r => r.Inicio))
        {
            texto = texto.Substring(0, reemplazo.Inicio)
                + reemplazo.Valor
                + texto.Substring(reemplazo.Inicio + reemplazo.Longitud);
        }

        // Ninguna fecha/UUID ajena puede quedar (incluye formas Z o valores no normalizados).
        foreach (Match fecha in FechaXmpEnTexto.Matches(texto))
        {
            if (fecha.Value != FechaXmpFija)
                throw new InvalidDataException(
                    $"Fecha ISO fuera de los elementos XMP conocidos: '{fecha.Value}'.");
        }
        foreach (Match uuid in UuidEnTexto.Matches(texto))
        {
            if (uuid.Value != UuidFijo)
                throw new InvalidDataException(
                    $"UUID fuera de los elementos XMP conocidos: '{uuid.Value}'.");
        }

        return Encoding.Latin1.GetBytes(texto);
    }

    private static List<(int Inicio, int Fin)> RangosInstruccionesProceso(string texto)
    {
        var rangos = new List<(int Inicio, int Fin)>();
        int indice = 0;
        while (true)
        {
            int inicio = texto.IndexOf("<?", indice, StringComparison.Ordinal);
            if (inicio < 0) return rangos;
            int fin = texto.IndexOf("?>", inicio + 2, StringComparison.Ordinal);
            if (fin < 0)
                throw new InvalidDataException("XMP con instruccion de proceso sin cierre (fail-closed).");
            int despues = fin + 2;
            rangos.Add((inicio, despues));
            indice = despues;
        }
    }

    private static int BuscarFueraDePi(
        string texto, string patron, int desde, List<(int Inicio, int Fin)> rangosPi)
    {
        int indice = desde;
        while (true)
        {
            int encontrado = texto.IndexOf(patron, indice, StringComparison.Ordinal);
            if (encontrado < 0) return -1;
            bool dentroDePi = rangosPi.Any(r => encontrado >= r.Inicio && encontrado < r.Fin);
            if (!dentroDePi) return encontrado;
            indice = encontrado + patron.Length;
        }
    }

    private static int ContarFueraDePi(
        string texto, string patron, List<(int Inicio, int Fin)> rangosPi)
    {
        int total = 0;
        int desde = 0;
        while (true)
        {
            int encontrado = BuscarFueraDePi(texto, patron, desde, rangosPi);
            if (encontrado < 0) return total;
            total++;
            desde = encontrado + patron.Length;
        }
    }

    // ── Utilidades lexicas ─────────────────────────────────────────────────

    private bool TryLeerEntero(out int valor)
    {
        int inicio = _i;
        while (_i < _texto.Length && _texto[_i] >= '0' && _texto[_i] <= '9') _i++;
        valor = 0;
        bool ok = _i > inicio
            && int.TryParse(_texto.AsSpan(inicio, _i - inicio), NumberStyles.None,
                CultureInfo.InvariantCulture, out valor);
        if (!ok)
        {
            _i = inicio;
            valor = 0;
        }
        return ok;
    }

    private bool TryLeerEnteroLargo(out long valor)
    {
        int inicio = _i;
        while (_i < _texto.Length && _texto[_i] >= '0' && _texto[_i] <= '9') _i++;
        valor = 0;
        bool ok = _i > inicio
            && long.TryParse(_texto.AsSpan(inicio, _i - inicio), NumberStyles.None,
                CultureInfo.InvariantCulture, out valor);
        if (!ok)
        {
            _i = inicio;
            valor = 0;
        }
        return ok;
    }

    private bool TryLeerDigitosFijos(int cantidad, out long valor)
    {
        int inicio = _i;
        valor = 0;
        for (int k = 0; k < cantidad; k++)
        {
            if (_i >= _texto.Length || _texto[_i] < '0' || _texto[_i] > '9')
            {
                _i = inicio;
                valor = 0;
                return false;
            }
            valor = valor * 10 + (_texto[_i] - '0');
            _i++;
        }
        if (_i < _texto.Length && _texto[_i] >= '0' && _texto[_i] <= '9')
        {
            _i = inicio;
            valor = 0;
            return false;
        }
        return true;
    }

    private int LeerEntero()
    {
        if (!TryLeerEntero(out int valor))
            throw new InvalidDataException($"Se esperaba un entero PDF en el offset {_i}.");
        return valor;
    }

    private string Muestra(int posicion)
    {
        int fin = Math.Min(_texto.Length, posicion + 24);
        return _texto.Substring(posicion, fin - posicion).Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static bool EsBlanco(char c) =>
        c is ' ' or '\t' or '\r' or '\n' or '\f' or '\0';

    private static bool EsDelimitador(char c) =>
        EsBlanco(c) || c is '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '/' or '%';

    private static bool EsHex(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private readonly struct EntradaXref
    {
        public EntradaXref(long offset, int generacion, bool enUso)
        {
            Offset = offset;
            Generacion = generacion;
            EnUso = enUso;
        }

        public long Offset { get; }
        public int Generacion { get; }
        public bool EnUso { get; }
    }
}
