using System.Text.Encodings.Web;
using System.Text.Json;

namespace KernelMemory.Extensions.Helper;

internal static class HttpClientPayloadSerializerHelper
{
    private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static string Serialize(object obj)
    {
        return JsonSerializer.Serialize(obj, _serializerOptions);
    }
}
