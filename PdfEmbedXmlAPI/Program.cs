using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Filespec;
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