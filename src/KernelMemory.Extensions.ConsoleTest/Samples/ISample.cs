using System.Threading.Tasks;

namespace SemanticMemory.Samples;

internal interface ISample
{
    Task RunSample(string bookPdf);
}