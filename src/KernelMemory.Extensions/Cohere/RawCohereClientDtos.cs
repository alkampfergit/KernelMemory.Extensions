using Microsoft.KernelMemory.MemoryStorage;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KernelMemory.Extensions.Cohere;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public record CohereReRankRequest(string Question, string[] Answers);

public class CohereReRankRequestBody
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = null!;

    [JsonPropertyName("query")]
    public string Query { get; init; } = null!;

    [JsonPropertyName("top_n")]
    public int TopN { get; init; }

    [JsonPropertyName("documents")]
    public string[] Documents { get; init; } = null!;
}

public class CohereRagRequest
{
    /// <summary>
    /// Factory method to create a request directly from the user question and memory records
    /// </summary>
    /// <param name="question">Question of the user</param>
    /// <param name="memoryRecords">MemoryRecords you want to pass to answer query.</param>
    /// <returns></returns>
    public static CohereRagRequest CreateFromMemoryRecord(string question, IEnumerable<MemoryRecord> memoryRecords)
    {
        //create a request body directly with system.text.json
        CohereRagRequest ragRequest = new CohereRagRequest()
        {
            Message = question,
            Documents = new List<RagDocument>()
        };

        foreach (var memory in memoryRecords)
        {
            ragRequest.Documents.Add(new RagDocument()
            {
                DocId = memory.Id,
                Text = memory.GetPartitionText()
            });
        }

        return ragRequest;
    }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = "command-r-plus";

    [JsonPropertyName("documents")]
    public List<RagDocument> Documents { get; set; }

    [JsonPropertyName("temperature")]
    public float Temperature { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; internal set; }

    [JsonPropertyName("preamble")]
    public string Preamble { get; set; }

    [JsonPropertyName("chat_history")]
    public List<ChatMessage> ChatHistory { get; set; }

    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; }

    [JsonPropertyName("prompt_truncation")]
    public string PromptTruncation { get; set; } = "OFF";

    [JsonPropertyName("connectors")]
    public List<Connector> Connectors { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("max_input_tokens")]
    public int? MaxInputTokens { get; set; }

    [JsonPropertyName("k")]
    public int? K { get; set; }

    [JsonPropertyName("p")]
    public float? P { get; set; } = 0.75f;

    [JsonPropertyName("seed")]
    public float? Seed { get; set; }

    [JsonPropertyName("stop_sequences")]
    public List<string> StopSequences { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public float? FrequencyPenalty { get; set; } = 0.0f;

    [JsonPropertyName("presence_penalty")]
    public float? PresencePenalty { get; set; } = 0.0f;

    [JsonPropertyName("tools")]
    public List<Tool> Tools { get; set; }

    [JsonPropertyName("tool_results")]
    public List<ToolResult> ToolResults { get; set; }
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}

public class Connector
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("user_access_token")]
    public string UserAccessToken { get; set; }

    [JsonPropertyName("continue_on_failure")]
    public bool ContinueOnFailure { get; set; } = false;

    [JsonPropertyName("options")]
    public Dictionary<string, string> Options { get; set; }

    [JsonPropertyName("search_queries_only")]
    public bool SearchQueriesOnly { get; set; } = false;
}

public class Tool
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("parameter_definitions")]
    public Dictionary<string, ToolParameter> ParameterDefinitions { get; set; }
}

public class ToolParameter
{
    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }
}

public class ToolResult
{
    [JsonPropertyName("call")]
    public ToolCall Call { get; set; }

    [JsonPropertyName("outputs")]
    public List<Dictionary<string, object>> Outputs { get; set; }
}

public class ToolCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; }
}

public class CohereRagResponse
{
    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("citations")]
    public List<Citation> Citations { get; set; }

    [JsonPropertyName("documents")]
    public List<RagDocument> Documents { get; set; }
}

public class Citation
{
    [JsonPropertyName("start")]
    public int Start { get; set; }

    [JsonPropertyName("end")]
    public int End { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("document_ids")]
    public List<string> DocumentIds { get; set; }
}

public class RagDocument
{
    [JsonPropertyName("docid")]
    public string DocId { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }
}

public class ReRankDocument
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = null!;
}

public class Result
{
    [JsonPropertyName("document")]
    public ReRankDocument? Document { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("relevance_score")]
    public double RelevanceScore { get; set; }
}

public class ApiVersion
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = null!;

    [JsonPropertyName("is_deprecated")]
    public bool IsDeprecated { get; set; }

    [JsonPropertyName("is_experimental")]
    public bool IsExperimental { get; set; }
}

public class BilledUnits
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("search_units")]
    public int SearchUnits { get; set; }

    [JsonPropertyName("classifications")]
    public int Classifications { get; set; }
}

public class Tokens
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

public class Meta
{
    [JsonPropertyName("api_version")]
    public ApiVersion ApiVersion { get; set; } = null!;

    [JsonPropertyName("billed_units")]
    public BilledUnits BilledUnits { get; set; } = null!;

    [JsonPropertyName("tokens")]
    public Tokens Tokens { get; set; } = null!;

    [JsonPropertyName("warnings")]
    public string[]? Warnings { get; set; }
}

public class ReRankResult
{
    public static ReRankResult Empty { get; } = new ReRankResult()
    {
        Results = (new List<Result>()).AsReadOnly(),
    };

    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("results")]
    public IReadOnlyList<Result> Results { get; set; } = null!;

    [JsonPropertyName("meta")]
    public Meta Meta { get; set; } = null!;
}

public class ChatStreamEvent
{
    [JsonPropertyName("is_finished")]
    public bool IsFinished { get; set; }

    [JsonPropertyName("event_type")]
    public string EventType { get; set; }

    [JsonPropertyName("generation_id")]
    public string GenerationId { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("citations")]
    public List<CohereRagCitation> Citations { get; set; }
}

public class CohereRagCitation
{
    [JsonPropertyName("start")]
    public int Start { get; set; }

    [JsonPropertyName("end")]
    public int End { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("document_ids")]
    public List<string> DocumentIds { get; set; }
}

public class CohereRagStreamingResponse
{
    public CohereRagResponseType ResponseType { get; set; }

    public string Text { get; set; }

    public List<CohereRagCitation> Citations { get; set; }
}

public enum CohereRagResponseType
{
    Unknown = 0,
    Text = 1,
    Citations = 2,
}

public static class CohereModels
{
    public const string EmbedEnglishV3 = "embed-english-v3.0";
    public const string EmbedMultilingualV3 = "embed-multilingual-v3.0";
    public const string EmbedEnglishLightV3 = "embed-english-light-v3.0";
    public const string EmbedMultilingualLightV3 = "embed-multilingual-light-v3.0";
    public const string EmbedEnglishV2 = "embed-english-v2.0";
    public const string EmbedEnglishLightV2 = "embed-english-light-v2.0";
    public const string EmbedMultilingualV2 = "embed-multilingual-v2.0";
}

public class CohereEmbedRequest
{
    public IEnumerable<string> Texts { get; set; }
    public string Model { get; set; }
    public string InputType { get; set; }
    public IEnumerable<string> EmbeddingTypes { get; set; }
    public string Truncate { get; set; }
}

public static class CohereInputTypes
{
    public const string SearchDocument = "search_document";
    public const string SearchQuery = "search_query";
    public const string Classification = "classification";
    public const string Clustering = "clustering";
}
public class EmbedResult
{
    [JsonPropertyName("responseType")]
    public string ResponseType { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("embeddings")]
    public Embeddings Embeddings { get; set; }

    [JsonPropertyName("texts")]
    public List<string> Texts { get; set; }

    [JsonPropertyName("meta")]
    public Meta Meta { get; set; }
}

public class Embeddings
{
    [JsonPropertyName("float")]
    public List<float[]> Values { get; set; }
}


#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.