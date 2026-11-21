using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Centinelas del comparador semantico PDF. Demuestran que el comparador detecta
/// cambios reales (texto, imagen, fuente, longitud de fuente y pagina), que solo
/// tolera las diferencias declaradas (compresion y metadatos volatiles validos) y
/// que falla cerrado ante filtros, formas desconocidas o un cierre PDF manipulado.
/// </summary>
[TestClass]
public class PdfStructuralComparadorSentinelTests
{
    [TestMethod]
    public void PdfsIdenticos_SonEquivalentes()
    {
        PdfStructuralComparador.AssertEquivalent(ConstruirPdf(), ConstruirPdf(), "identicos");
    }

    [TestMethod]
    public void DistintaCompresionConElMismoContenido_EsEquivalente()
    {
        byte[] flate = ConstruirPdf(comprimirContenido: true);
        byte[] sinComprimir = ConstruirPdf(comprimirContenido: false, filtroContenido: null);

        PdfStructuralComparador.AssertEquivalent(flate, sinComprimir, "compresion distinta");
    }

    [TestMethod]
    public void DistintaCompresionDeFuenteConElMismoContenido_EsEquivalente()
    {
        // Mismo font program decodificado, dos FlateDecode (Deflate) distintos.
        byte[] fuente = FuentePorDefecto();
        Assert.AreNotEqual(
            Convert.ToHexString(Comprimir(fuente, CompressionLevel.NoCompression)),
            Convert.ToHexString(Comprimir(fuente, CompressionLevel.Optimal)),
            "El payload de prueba debe comprimir distinto con niveles distintos.");

        PdfStructuralComparador.AssertEquivalent(
            ConstruirPdf(nivelFuente: CompressionLevel.NoCompression),
            ConstruirPdf(nivelFuente: CompressionLevel.Optimal),
            "fuente deflate distinta");
    }

    [TestMethod]
    public void DistintoTagDeSubconjuntoDeFuente_EsEquivalente()
    {
        PdfStructuralComparador.AssertEquivalent(
            ConstruirPdf(tagFuente: "ABCDEF"),
            ConstruirPdf(tagFuente: "ZWKAGD"),
            "tag de subconjunto");
    }

    [TestMethod]
    public void DistintasFechasDelInfo_SonEquivalentes()
    {
        PdfStructuralComparador.AssertEquivalent(
            ConstruirPdf(fechaInfo: "D:20260101000000-07'00'"),
            ConstruirPdf(fechaInfo: "D:20261121101021+02'00'"),
            "fechas volatiles");
    }

    [TestMethod]
    public void SoloCreationDateEnInfo_EsValido()
    {
        // Forma real del renderer PdfSharp: el PDF crudo solo trae /CreationDate.
        PdfStructuralComparador.AssertEquivalent(
            ConstruirPdf(incluirModDate: false),
            ConstruirPdf(incluirModDate: false),
            "solo CreationDate");
    }

    [TestMethod]
    public void SinIdEnElTrailer_EsValido()
    {
        PdfStructuralComparador.AssertEquivalent(
            ConstruirPdf(idContenido: null),
            ConstruirPdf(idContenido: null),
            "sin ID");
    }

    [TestMethod]
    public void IdsValidosDistintos_SonEquivalentes()
    {
        PdfStructuralComparador.AssertEquivalent(
            ConstruirPdf(idContenido: "<11111111111111111111111111111111><22222222222222222222222222222222>"),
            ConstruirPdf(idContenido: "<33333333333333333333333333333333><44444444444444444444444444444444>"),
            "IDs distintos");
    }

    [TestMethod]
    public void ReferenciaConIdVsCandidatoSinId_Falla()
    {
        // La presencia de /ID es estructural: solo sus valores son volatiles.
        Assert.ThrowsExactly<PdfStructuralMismatchException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(idContenido: null),
                "presencia de ID"));
    }

    [TestMethod]
    public void DistintoTexto_Falla()
    {
        Assert.ThrowsExactly<PdfStructuralMismatchException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(contenido: "BT (Hola) Tj ET"),
                ConstruirPdf(contenido: "BT (Adios) Tj ET"),
                "texto"));
    }

    [TestMethod]
    public void DistintaImagen_Falla()
    {
        Assert.ThrowsExactly<PdfStructuralMismatchException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(imagen: Encoding.ASCII.GetBytes("imagen-uno")),
                ConstruirPdf(imagen: Encoding.ASCII.GetBytes("imagen-dos")),
                "imagen"));
    }

    [TestMethod]
    public void DistintaFuenteDecodificada_Falla()
    {
        Assert.ThrowsExactly<PdfStructuralMismatchException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(fuente: Encoding.ASCII.GetBytes("font-program-uno")),
                ConstruirPdf(fuente: Encoding.ASCII.GetBytes("font-program-dos")),
                "fuente"));
    }

    [TestMethod]
    public void DistintaLongitudDeFuenteDeclarada_Falla()
    {
        // /Length1 describe la longitud descomprimida: mutarlo sin tocar la stream
        // debe fallar (no es una longitud de contenedor ignorable).
        Assert.ThrowsExactly<PdfStructuralMismatchException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(longitud1Override: 12345),
                "longitud de fuente"));
    }

    [TestMethod]
    public void DistintaPagina_Falla()
    {
        Assert.ThrowsExactly<PdfStructuralMismatchException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(mediaBox: "0 0 612 792"),
                ConstruirPdf(mediaBox: "0 0 612 800"),
                "pagina"));
    }

    // ── Cierre PDF: xref/startxref/%%EOF y datos extra ─────────────────────

    [TestMethod]
    public void PdfSinTablaXref_Falla()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(incluirXref: false),
                "sin xref"));
    }

    [TestMethod]
    public void StartxrefIncorrecto_Falla()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(startxrefOverride: 0),
                "startxref incorrecto"));
    }

    [TestMethod]
    public void EntradaXrefDesalineada_Falla()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(desplazamientoXref: 1),
                "xref desalineada"));
    }

    [TestMethod]
    public void BytesDespuesDelEof_Falla()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(sufijoExtra: "\n%datos-extra"),
                "datos despues de EOF"));
    }

    [TestMethod]
    public void ObjetoAgregadoDespuesDelEof_Falla()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(sufijoExtra: "\n10 0 obj\n<</Type/Catalog>>\nendobj\n"),
                "objeto despues de EOF"));
    }

    [TestMethod]
    public void TrailerAgregadoDespuesDelEof_Falla()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(sufijoExtra: "\ntrailer\n<<>>\nstartxref\n0\n%%EOF"),
                "trailer despues de EOF"));
    }

    [TestMethod]
    public void ActualizacionIncrementalConPrev_Falla()
    {
        Assert.ThrowsExactly<NotSupportedException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(prevEnTrailer: true),
                "actualizacion incremental"));
    }

    // ── Metadatos volatiles: tipo/forma ────────────────────────────────────

    [TestMethod]
    public void FechaDeInfoConFormaDesconocida_Falla()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(creationDate: "(D:2026-11-21)"),
                "fecha mal formada"));
    }

    [TestMethod]
    public void FechaDeInfoQueNoEsCadena_Falla()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(creationDate: "/NoEsFecha"),
                "fecha que no es cadena"));
    }

    [TestMethod]
    public void IdDeTrailerQueNoEsArreglo_Falla()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(idContenido: "(no-es-arreglo)"),
                "ID que no es arreglo"));
    }

    [TestMethod]
    public void IdDeTrailerConUnaSolaCadena_Falla()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(idContenido: "<00112233445566778899AABBCCDDEEFF>"),
                "ID con una sola cadena"));
    }

    [TestMethod]
    public void IdDeTrailerConLongitudInvalida_Falla()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(idContenido: "<00112233><FFEEDDCC>"),
                "ID de 4 bytes"));
    }

    // ── Fail-closed de formas/filtros no soportados ────────────────────────

    [TestMethod]
    public void FiltroDeStreamDesconocido_FallaCerrado()
    {
        Assert.ThrowsExactly<NotSupportedException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(filtroContenido: "LZWDecode"),
                "filtro desconocido"));
    }

    [TestMethod]
    public void DecodeParms_FallaCerrado()
    {
        Assert.ThrowsExactly<NotSupportedException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(decodeParms: "/Predictor 12"),
                "DecodeParms"));
    }

    [TestMethod]
    public void ObjectStream_FallaCerrado()
    {
        Assert.ThrowsExactly<NotSupportedException>(() =>
            PdfStructuralComparador.AssertEquivalent(
                ConstruirPdf(),
                ConstruirPdf(objectStream: true),
                "object stream"));
    }

    /// <summary>
    /// PDF minimo con tabla xref, startxref y %%EOF reales. La variante por
    /// parametro permite mutar cada parte semantica o el cierre por separado.
    /// </summary>
    private static byte[] ConstruirPdf(
        string contenido = "BT (Hola) Tj ET",
        bool comprimirContenido = true,
        string? filtroContenido = "FlateDecode",
        string? decodeParms = null,
        byte[]? imagen = null,
        byte[]? fuente = null,
        string mediaBox = "0 0 612 792",
        string fechaInfo = "D:20260101000000-07'00'",
        bool incluirModDate = true,
        string? creationDate = null,
        string tagFuente = "ABCDEF",
        bool objectStream = false,
        CompressionLevel nivelFuente = CompressionLevel.Optimal,
        int? longitud1Override = null,
        string? idContenido =
            "<00112233445566778899AABBCCDDEEFF><FFEEDDCCBBAA99887766554433221100>",
        int? startxrefOverride = null,
        int desplazamientoXref = 0,
        bool incluirXref = true,
        bool prevEnTrailer = false,
        string sufijoExtra = "")
    {
        imagen ??= Encoding.ASCII.GetBytes("imagen");
        fuente ??= FuentePorDefecto();
        if (!comprimirContenido && filtroContenido == "FlateDecode") filtroContenido = null;

        byte[] datosContenido = Encoding.Latin1.GetBytes(contenido);
        if (comprimirContenido) datosContenido = Comprimir(datosContenido);

        using var salida = new MemoryStream();
        var offsets = new SortedDictionary<int, int>();

        void Objeto(int numero, string diccionario, byte[]? datos)
        {
            offsets[numero] = (int)salida.Length;
            Escribir(salida, numero + " 0 obj\n");
            if (datos is null)
            {
                Escribir(salida, "<<" + diccionario + ">>\nendobj\n");
                return;
            }
            Escribir(salida, "<<" + diccionario + "/Length " + datos.Length + ">>\nstream\n");
            salida.Write(datos, 0, datos.Length);
            Escribir(salida, "\nendstream\nendobj\n");
        }

        Escribir(salida, "%PDF-1.7\n");

        string valorCreation = creationDate ?? "(" + fechaInfo + ")";
        string info = "/CreationDate" + valorCreation
            + (incluirModDate ? "/ModDate(" + fechaInfo + ")" : "")
            + "/Producer(PDFsharp 6.2.4-gdi)";
        Objeto(1, info, null);
        Objeto(2, "/Type/Catalog/Pages 3 0 R", null);
        Objeto(3, "/Type/Pages/Count 1/Kids[4 0 R]", null);
        Objeto(4,
            "/Type/Page/MediaBox[" + mediaBox + "]/Parent 3 0 R/Contents 5 0 R"
            + "/Resources<</Font<</F1 6 0 R>>/XObject<</Im1 8 0 R>>>>",
            null);

        string dictContenido = filtroContenido is null ? "" : "/Filter/" + filtroContenido;
        if (decodeParms is not null) dictContenido += "/DecodeParms<<" + decodeParms + ">>";
        Objeto(5, dictContenido, datosContenido);

        Objeto(6,
            "/Type/Font/Subtype/TrueType/BaseFont/" + tagFuente
            + "+Segoe#20UI/Encoding/WinAnsiEncoding/FontDescriptor 7 0 R",
            null);
        Objeto(7,
            "/Type/FontDescriptor/FontName/" + tagFuente
            + "+Segoe#20UI/Flags 32/FontFile2 9 0 R",
            null);
        Objeto(8,
            "/Type/XObject/Subtype/Image/Width 1/Height 1/ColorSpace/DeviceRGB"
            + "/BitsPerComponent 8/Filter/FlateDecode",
            Comprimir(imagen));
        Objeto(9,
            "/Length1 " + (longitud1Override ?? fuente.Length) + "/Filter/FlateDecode",
            Comprimir(fuente, nivelFuente));

        if (objectStream)
            Objeto(10, "/Type/ObjStm/N 0/First 0", Array.Empty<byte>());

        int ultimoObjeto = objectStream ? 10 : 9;
        int inicioXref = (int)salida.Length;
        if (incluirXref)
        {
            Escribir(salida, "xref\n0 " + (ultimoObjeto + 1) + "\n");
            Escribir(salida, "0000000000 65535 f \n");
            for (int numero = 1; numero <= ultimoObjeto; numero++)
            {
                long offset = offsets[numero] + desplazamientoXref;
                Escribir(salida, offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
            }
        }

        string id = idContenido is null ? "" : "/ID[" + idContenido + "]";
        Escribir(salida,
            "trailer\n<</Size " + (ultimoObjeto + 1) + "/Root 2 0 R/Info 1 0 R" + id
            + (prevEnTrailer ? "/Prev 0" : "") + ">>\n");

        int valorStartXref = startxrefOverride ?? (incluirXref ? inicioXref : 0);
        Escribir(salida,
            "startxref\n" + valorStartXref.ToString(CultureInfo.InvariantCulture)
            + "\n%%EOF" + sufijoExtra);

        return salida.ToArray();
    }

    private static byte[] FuentePorDefecto() =>
        Encoding.Latin1.GetBytes(string.Concat(Enumerable.Repeat("fuente-ttf-ABCDEFGH;", 28)));

    private static void Escribir(Stream salida, string texto)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(texto);
        salida.Write(bytes, 0, bytes.Length);
    }

    private static byte[] Comprimir(byte[] datos, CompressionLevel nivel = CompressionLevel.Optimal)
    {
        using var salida = new MemoryStream();
        using (var zlib = new ZLibStream(salida, nivel, leaveOpen: true))
            zlib.Write(datos, 0, datos.Length);
        return salida.ToArray();
    }
}
