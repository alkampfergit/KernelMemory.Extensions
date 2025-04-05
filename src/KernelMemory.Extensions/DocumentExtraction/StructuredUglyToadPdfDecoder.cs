using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml.ExtendedProperties;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.Pipeline;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace KernelMemory.Extensions.DocumentExtraction;

public class StructuredUglyToadPdfDecoder
{
    public FileContent DecodePdf(Stream stream)
    {
        var result = new FileContent(MimeTypes.PlainText);
        using var document = PdfDocument.Open(stream);

        Word previous = null;
        var sb = new StringBuilder(1000);

        foreach (var page in document.GetPages())
        {
            foreach (var word in page.GetWords())
            {
                if (previous != null)
                {
                    var hasInsertedWhitespace = false;
                    var bothNonEmpty = previous.Letters.Count > 0 && word.Letters.Count > 0;
                    if (bothNonEmpty)
                    {
                        var prevLetter1 = previous.Letters[0];
                        var currentLetter1 = word.Letters[0];

                        var baselineGap = Math.Abs(prevLetter1.StartBaseLine.Y - currentLetter1.StartBaseLine.Y);

                        if (baselineGap > 3)
                        {
                            hasInsertedWhitespace = true;
                            sb.AppendLine();
                        }
                    }

                    if (!hasInsertedWhitespace)
                    {
                        sb.Append(" ");
                    }
                }

                sb.Append(word.Text);

                previous = word;
            }

            result.Sections.Add(new Chunk(sb.ToString(), page.Number));
            sb.Clear();
        }

        return result;
    }
}
