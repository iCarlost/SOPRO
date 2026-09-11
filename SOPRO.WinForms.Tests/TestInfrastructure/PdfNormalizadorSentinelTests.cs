using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Tests.TestInfrastructure;

namespace SOPRO.WinForms.Tests.TestInfrastructure
{
    /// <summary>
    /// Test centinela del normalizador de PDF (N7-18a): los patrones que el normalizador debe
    /// sustituir (tags [A-Z]{6}+, fechas ISO y UUIDs) se reemplazan SOLO dentro de sus campos
    /// concretos (/BaseFont y /FontName; elementos XMP de fecha/UUID). Cualquier texto con el
    /// mismo aspecto fuera de esos campos debe permanecer intacto, y cualquier forma no
    /// reconocida dentro de los campos debe provocar fallo rápido (fail-fast).
    /// </summary>
    [TestClass]
    public class PdfNormalizadorSentinelTests
    {
        [TestMethod]
        public void SustituyeSoloCamposConocidos_DejaIntactoElMismoPatronFueraDeEllos()
        {
            var input =
                "BT/FontName/ABCDEF+Segoe#20UI 12 Tf/F1 9 Tf" + "\n"
                + "<</Type/Font/BaseFont/ABCDEF+Segoe#20UI/Encoding/WinAnsiEncoding>>" + "\n"
                + "/FontName/UVWXZY+Segoe#20UI,Bold/Flags 32" + "\n"
                + "Tj (Nota 1: contenido con ABCDEF+ dentro de un string)" + "\n"
                + "Tj (Nota 2: fecha 2026-09-11T08:30:51-07:00 en texto)" + "\n"
                + "Tj (Nota 3: ref uuid:45968bee-2f89-44c5-8d09-610e2240e6f6 en texto)" + "\n"
                + "stream\n<?xpacket begin=\"\" id=\"W5M\"?>\n"
                + "<x:xmpmeta><rdf:RDF>"
                + "<xmp:CreateDate>2026-09-11T08:30:51-07:00</xmp:CreateDate>"
                + "<xmp:ModifyDate>2026-09-11T08:30:51-07:00</xmp:ModifyDate>"
                + "<xmpMM:DocumentID>uuid:45968bee-2f89-44c5-8d09-610e2240e6f6</xmpMM:DocumentID>"
                + "<xmpMM:InstanceID>uuid:afe93a63-f7ee-4670-a2b8-97d5eac892df</xmpMM:InstanceID>"
                + "<dc:title>CATALOGO</dc:title>"
                + "</rdf:RDF></x:xmpmeta>\n<?xpacket end=\"w\"?>" + "\nendstream";

            var patched = Encoding.Latin1.GetString(
                PdfNormalizador.NeutralizarNoDeterminismoResidual(Encoding.Latin1.GetBytes(input)));

            StringAssert.Contains(patched, "/FontName/SOPROX+Segoe#20UI 12 Tf");
            StringAssert.Contains(patched, "/BaseFont/SOPROX+Segoe#20UI/Encoding");
            StringAssert.Contains(patched, "/FontName/SOPROX+Segoe#20UI,Bold/Flags 32");
            StringAssert.Contains(patched, "Tj (Nota 1: contenido con ABCDEF+ dentro de un string)");
            StringAssert.Contains(patched, "Tj (Nota 2: fecha 2026-09-11T08:30:51-07:00 en texto)");
            StringAssert.Contains(patched, "Tj (Nota 3: ref uuid:45968bee-2f89-44c5-8d09-610e2240e6f6 en texto)");
            StringAssert.Contains(patched, "<xmp:CreateDate>2000-01-01T00:00:00+00:00</xmp:CreateDate>");
            StringAssert.Contains(patched, "<xmpMM:DocumentID>uuid:00000000-0000-0000-0000-000000000000</xmpMM:DocumentID>");
            StringAssert.Contains(patched, "<dc:title>CATALOGO</dc:title>");
        }

        [TestMethod]
        public void RechazaFechaConSufijoZ_CuyaLongitudNoSeConservaria()
        {
            var input = "<stream>\n<?xpacket begin=\"\"?>"
                + "<x:xmpmeta><rdf:RDF><xmp:CreateDate>2026-09-11T08:30:51Z</xmp:CreateDate>"
                + "</rdf:RDF></x:xmpmeta>\n<?xpacket end=\"w\"?>" + "\nendstream";

            Assert.ThrowsException<InvalidDataException>(() =>
                PdfNormalizador.NeutralizarNoDeterminismoResidual(Encoding.Latin1.GetBytes(input)));
        }

        [TestMethod]
        public void RechazaFechaFueraDeLosElementosXmpConocidos()
        {
            var input = "<?xpacket begin=\"\"?>"
                + "<xmp:CreateDate>2026-09-11T08:30:51-07:00</xmp:CreateDate>"
                + "<pdf:Keywords>revision 2026-09-11T08:30:51-07:00</pdf:Keywords>"
                + "<?xpacket end=\"w\"?>";

            Assert.ThrowsException<InvalidDataException>(() =>
                PdfNormalizador.NeutralizarNoDeterminismoResidual(Encoding.Latin1.GetBytes(input)));
        }

        [TestMethod]
        public void RechazaUuidFueraDeLosElementosXmpConocidos()
        {
            var input = "<?xpacket begin=\"\"?>"
                + "<dc:description>uuid:45968bee-2f89-44c5-8d09-610e2240e6f6</dc:description>"
                + "<xmpMM:DocumentID>uuid:45968bee-2f89-44c5-8d09-610e2240e6f6</xmpMM:DocumentID>"
                + "<?xpacket end=\"w\"?>";

            Assert.ThrowsException<InvalidDataException>(() =>
                PdfNormalizador.NeutralizarNoDeterminismoResidual(Encoding.Latin1.GetBytes(input)));
        }
    }
}