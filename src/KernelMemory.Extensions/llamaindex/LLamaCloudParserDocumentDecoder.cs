using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Context;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class LLamaCloudParserDocumentDecoder : IContentDecoder
{
    private readonly ILogger<LLamaCloudParserDocumentDecoder> _log;
    private readonly LLamaCloudParserClient _client;
    private readonly IContextProvider _contextProvider;

    public LLamaCloudParserDocumentDecoder(
        LLamaCloudParserClient client,
        IContextProvider contextProvider,
        ILoggerFactory? loggerFactory = null)
    {
        _log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<LLamaCloudParserDocumentDecoder>();
        _client = client;
        _contextProvider = contextProvider;
    }

    /// <inheritdoc />
    public bool SupportsMimeType(string mimeType)
    {
        //Here we can add more mime types
        return mimeType != null
            && (
                mimeType.StartsWith(MimeTypes.Pdf, StringComparison.OrdinalIgnoreCase)
                || mimeType.StartsWith(MimeTypes.OpenDocumentText, StringComparison.OrdinalIgnoreCase)
            );
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(string filename, CancellationToken cancellationToken = default)
    {
        using var stream = File.OpenRead(filename);
        return this.DecodeAsync(stream, filename, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(BinaryData data, CancellationToken cancellationToken = default)
    {
        using var stream = data.ToStream();
        return DecodeAsync(stream, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(Stream data, CancellationToken cancellationToken = default)
    {
        return DecodeAsync(data, null, cancellationToken);
    }

    public async Task<FileContent> DecodeAsync(Stream data, string? fileName, CancellationToken cancellationToken = default)
    {
        _log.LogDebug("Extracting structured text with llamacloud from file");

        //retrieve filename and parsing instructions from context
        var context = _contextProvider.GetContext();
        string parsingInstructions = string.Empty;
        if (context.Arguments.TryGetValue(LLamaCloudParserDocumentDecoderExtensions.FileNameKey, out var fileNameContext))
        {
            fileName = fileNameContext as string ?? string.Empty;
        }
        if (context.Arguments.TryGetValue(LLamaCloudParserDocumentDecoderExtensions.ParsingInstructionsKey, out var parsingInstructionsContext))
        {
            parsingInstructions = parsingInstructionsContext as string ?? string.Empty;
        }

        // ok we need a way to find the correct instruction for the file, so we can use a different configuration
        // for each file that we are going to parse.
        var parameters = new UploadParameters();

        //file name must not be null
        if (string.IsNullOrEmpty(fileName))
        {
            throw new Exception("LLAMA Cloud error: file name is missing");
        }

        //ok we need a temporary file name that we need to use to upload the file, we need a seekable stream
        var tempFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_" + fileName);
        using (var writeFileStream = File.Create(tempFileName))
        {
            await data.CopyToAsync(writeFileStream, cancellationToken);
        }

        parameters.WithParsingInstructions(parsingInstructions);

        var response = await PerformCall(fileName, tempFileName, parameters);

        if (response != null && response.ErrorCode != null)
        {
            throw new Exception($"LLAMA Cloud error: {response.ErrorCode} - {response.ErrorMessage}");
        }

        if (response == null)
        {
            throw new Exception("LLAMA Cloud error: no response");
        }

        var jobId = response.Id.ToString();

        //now wait for the job to be completed
        var jobResponse = await _client.WaitForJobSuccessAsync(jobId, TimeSpan.FromMinutes(5));

        if (!jobResponse)
        {
            throw new Exception("LLAMA Cloud error: job not completed");
        }

        // ok now the job is completed, we can get the markdown
        var markdown = await _client.GetJobRawMarkdownAsync(jobId);

        var result = new FileContent(MimeTypes.MarkDown);
        result.Sections.Add(new FileSection(1, markdown, false));

        return result;
    }

    private async Task<UploadResponse> PerformCall(
        string fileName,
        string physicalTempFileName,
        UploadParameters parameters)
    {
        try
        {
            await using var tempFileStream = File.OpenRead(physicalTempFileName);
            var uploadResponse = await _client.UploadAsync(tempFileStream, fileName, parameters);
            return uploadResponse;
        }
        finally
        {
            File.Delete(physicalTempFileName);
        }
    }
}

public static class LLamaCloudParserDocumentDecoderExtensions
{
    internal const string FileNameKey = "llamacloud.filename";
    internal const string ParsingInstructionsKey = "llamacloud.parsing_instructions";

    public static void AddLLamaCloudParserOptions(this IContextProvider contextProvider, string filename, string parseInstructions)
    {
        contextProvider.SetContextArg(FileNameKey, filename);
        contextProvider.SetContextArg(ParsingInstructionsKey, parseInstructions);
    }
}