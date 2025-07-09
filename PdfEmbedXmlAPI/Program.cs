using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Filespec;
using iText.Kernel.XMP;
using iText.Kernel.XMP.Options;
using iText.Pdfa;

// Mini WebAPI
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseHttpsRedirection();

// To check if WebAPI is working
app.MapGet("/", () => { return "PfdEmbedXml"; });

// To process files
app.MapPost("/PdfEmbedXml", (PdfEmbedXmlRequest request) =>
{
    return Results.Ok(PdfEmbedXml(request));
});

app.Run();

// Processing PDF using iText functionality
// (create PDF/A-3, copy pages from original PDF, embed XML into PDF)
PdfEmbedXmlResponse PdfEmbedXml(PdfEmbedXmlRequest request)
{
    PdfEmbedXmlResponse ret = new PdfEmbedXmlResponse();

    try
    {
        byte[]? pdfBytes = null;
        byte[]? xmlBytes = null;

        if (String.IsNullOrWhiteSpace(request.PdfBase64))
        {
            ret.Error = "Input PDF is empty\r\n";
            return ret;
        }
        pdfBytes = Convert.FromBase64String(request.PdfBase64);
        if ((pdfBytes == null) || (pdfBytes.LongLength == 0))
        {
            ret.Error = "Input PDF is empty\r\n";
            return ret;
        }

        // Create new PDF/A-3
        using (MemoryStream outputPdfStream = new MemoryStream())
        using (PdfWriter pdfWriter = new PdfWriter(outputPdfStream))
        using (MemoryStream iccStream = new MemoryStream(IccProfile.IccProfileBytes))
        using (PdfADocument transformedPdfADoc = new PdfADocument(pdfWriter, PdfAConformance.PDF_A_3U, new PdfOutputIntent("Custom", "", "http://www.color.org", "sRGB2014", iccStream)))
        {
            // Copy pages from original PDF
            using (MemoryStream inputPdfStream = new MemoryStream(pdfBytes))
            using (PdfReader pdfReader = new PdfReader(inputPdfStream))
            using (PdfDocument originalPdfDoc = new PdfDocument(pdfReader))
            {
                originalPdfDoc.CopyPagesTo(1, originalPdfDoc.GetNumberOfPages(), transformedPdfADoc);
            }

            // Update XMP Metadata
            XMPMeta xmp = CreateValidXmp(transformedPdfADoc);
            transformedPdfADoc.SetXmpMetadata(xmp);

            // Embed XML into PDF
            if (!String.IsNullOrWhiteSpace(request.XmlBase64))
            {
                xmlBytes = Convert.FromBase64String(request.XmlBase64);
                if ((xmlBytes != null) && (xmlBytes.LongLength > 0))
                {
                    PdfFileSpec xmlFileSpec = PdfFileSpec.CreateEmbeddedFileSpec(
                        transformedPdfADoc,
                        xmlBytes,
                        "ZUGFeRD invoice",
                        "factur-x.xml",
                        new PdfName("application/xml"),
                        new PdfDictionary(new[] { new KeyValuePair<PdfName, PdfObject>(PdfName.ModDate, new PdfDate().GetPdfObject()) }),
                        PdfName.Alternative);
                    transformedPdfADoc.AddFileAttachment("factur-x.xml", xmlFileSpec);
                    transformedPdfADoc.GetCatalog().Put(PdfName.AF, new PdfArray { xmlFileSpec.GetPdfObject().GetIndirectReference() });
                }
            }

            // Finalize PDF/A-3
            transformedPdfADoc.Close();

            // Assign the resulting PDF to the data transfer object
            ret.PdfBase64 = Convert.ToBase64String(outputPdfStream.ToArray());
        }
    }
    catch (Exception ex)
    {
        ret.Error = "Exceprion in PdfCombineXml\r\n";
        if (!String.IsNullOrWhiteSpace(ex.Message))
        {
            ret.Error += ex.Message + "\r\n";
        }
        if ((ex.InnerException != null) && (!String.IsNullOrWhiteSpace(ex.InnerException.Message)))
        {
            ret.Error += ex.InnerException.Message + "\r\n";
        }
    }

    return ret;
}

XMPMeta CreateValidXmp(PdfADocument pdfDoc)
{
    XMPMeta xmp = pdfDoc.GetXmpMetadata(true);
    string zugferdNamespace = "urn:ferd:pdfa:CrossIndustryDocument:invoice:1p0#";
    string zugferdPrefix = "fx";
    XMPMetaFactory.GetSchemaRegistry().RegisterNamespace(zugferdNamespace, zugferdPrefix);

    xmp.SetProperty(zugferdNamespace, "DocumentType", "INVOICE");
    xmp.SetProperty(zugferdNamespace, "Version", "1.0");
    xmp.SetProperty(zugferdNamespace, "ConformanceLevel", "EXTENDED");
    xmp.SetProperty(zugferdNamespace, "DocumentFileName", "factur-x.xml");

    PropertyOptions bagOptions = new PropertyOptions(PropertyOptions.ARRAY);
    xmp.SetProperty(XMPConst.NS_PDFA_EXTENSION, "schemas", null, bagOptions);

    string bagPath = "pdfaExtension:schemas";

    int newItemIndex = xmp.CountArrayItems(XMPConst.NS_PDFA_EXTENSION, bagPath) + 1;
    string newItemPath = bagPath + "[" + newItemIndex + "]";

    PropertyOptions structOptions = new PropertyOptions(PropertyOptions.STRUCT);
    xmp.SetProperty(XMPConst.NS_PDFA_EXTENSION, newItemPath, null, structOptions);

    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, newItemPath, XMPConst.NS_PDFA_SCHEMA, "schema", "Factur-X PDFA Extension Schema");
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, newItemPath, XMPConst.NS_PDFA_SCHEMA, "namespaceURI", zugferdNamespace);
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, newItemPath, XMPConst.NS_PDFA_SCHEMA, "prefix", "fx");

    string seqPath = newItemPath + "/pdfaSchema:property";
    PropertyOptions seqOptions = new PropertyOptions(PropertyOptions.ARRAY_ORDERED);
    xmp.SetProperty(XMPConst.NS_PDFA_EXTENSION, seqPath, null, seqOptions);

    string firstSeqItemPath = seqPath + "[1]";
    string secondSeqItemPath = seqPath + "[2]";
    string thirdSeqItemPath = seqPath + "[3]";
    string fourthSeqItemPath = seqPath + "[4]";

    xmp.SetProperty(XMPConst.NS_PDFA_EXTENSION, firstSeqItemPath, null, structOptions);
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, firstSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "name", "DocumentFileName");
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, firstSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "valueType", "Text");
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, firstSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "category", "external");
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, firstSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "description", "The name of the embedded XML document");

    xmp.SetProperty(XMPConst.NS_PDFA_EXTENSION, secondSeqItemPath, null, structOptions);
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, secondSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "name", "DocumentType");
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, secondSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "valueType", "Text");
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, secondSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "category", "external");
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, secondSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "description", "The type of the hybrid document in capital letters, e.g. INVOICE or ORDER");

    xmp.SetProperty(XMPConst.NS_PDFA_EXTENSION, thirdSeqItemPath, null, structOptions);
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, thirdSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "name", "Version");
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, thirdSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "valueType", "Text");
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, thirdSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "category", "external");
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, thirdSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "description", "The actual version of the standard applying to the embedded XML document");

    xmp.SetProperty(XMPConst.NS_PDFA_EXTENSION, fourthSeqItemPath, null, structOptions);
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, fourthSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "name", "ConformanceLevel");
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, fourthSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "valueType", "Text");
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, fourthSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "category", "external");
    xmp.SetStructField(XMPConst.NS_PDFA_EXTENSION, fourthSeqItemPath, XMPConst.NS_PDFA_PROPERTY, "description", "The conformance level of the embedded XML document");

    return xmp;
}

// Data transfer object for the request
internal class PdfEmbedXmlRequest
{
    public string? PdfBase64 { get; set; } = null;
    public string? XmlBase64 { get; set; } = null;
}

// Data transfer object for the response
internal class PdfEmbedXmlResponse
{
    public string? PdfBase64 { get; set; } = null;
    public string? Error { get; set; } = null;
}

// Icc-Profile caching
internal static class IccProfile
{
    private static byte[]? _iccBytes = null;
    public static byte[] IccProfileBytes {
        get {
            if (_iccBytes == null)
            {
                _iccBytes = File.ReadAllBytes("sRGB2014.icc");
            }
            return _iccBytes;
        }
    } 
}