using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;

namespace KernelMemory.Extensions.DocumentExtraction;

public class PdfStructuredDocumentDecoder : IContentDecoder
{
    private readonly ILogger<PdfStructuredDocumentDecoder> _log;
    private readonly StructuredUglyToadPdfDecoder _uglyToadStructured;

    public PdfStructuredDocumentDecoder(ILoggerFactory? loggerFactory = null)
    {
        this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<PdfStructuredDocumentDecoder>();
        _uglyToadStructured = new StructuredUglyToadPdfDecoder();
    }

    /// <inheritdoc />
    public bool SupportsMimeType(string mimeType)
    {
        return mimeType != null && mimeType.StartsWith(MimeTypes.Pdf, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(string filename, CancellationToken cancellationToken = default)
    {
        using var stream = File.OpenRead(filename);
        return this.DecodeAsync(stream, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(BinaryData data, CancellationToken cancellationToken = default)
    {
        using var stream = data.ToStream();
        return this.DecodeAsync(stream, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(Stream data, CancellationToken cancellationToken = default)
    {
        this._log.LogDebug("Extracting structured text from PDF file");
        throw new NotImplementedException();
        var result = _uglyToadStructured.DecodePdf(data);
        
        return Task.FromResult(result);
    }
}

