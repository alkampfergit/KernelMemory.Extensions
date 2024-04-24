using System.Threading.Tasks;

namespace SemanticMemory.Samples;

/// <summary>
/// Stupid interface that represent a sample that can work on
/// a single pd file.
/// </summary>
internal interface ISample
{
    /// <summary>
    /// Run the sample, you can optionally pass a file for all samples that operates
    /// on a single file.
    /// </summary>
    /// <param name="bookPdf"></param>
    /// <returns></returns>
    Task RunSample(string? bookPdf);
}

internal interface ISample2
{
    /// <summary>
    /// Other type of samples that does not operates on a single file, but is more complex
    /// </summary>
    /// <returns></returns>
    Task RunSample2();
}
