using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace KernelMemory.Extensions.Cohere;

/// <summary>
/// Try to implement a tokenizer for Cohere
/// </summary>
public class CohereTokenizer
{
    public Dictionary<string, Tiktoken> Tokenizers { get; set; } = new();

    public CohereTokenizer(IHttpClientFactory httpClientFactory)
    {
        var tokenizerFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "command-r-plus.tiktoken");
        var tokenizerExtraFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "command-r-plus.tiktoken.extra");

        DownloadCohereTokenizerSpecifcationFileAndConvertToTiktoken(
            httpClientFactory,
            "https://storage.googleapis.com/cohere-public/tokenizers/command-r-plus.json",
            tokenizerFile,
            tokenizerExtraFile);

        //now we need to load the tokenizer, first of all we load the extra data
        var extraData = File.ReadAllText(tokenizerExtraFile);
        var ed = JsonSerializer.Deserialize<ExtraTokenizerData>(extraData)!;

        var tiktoken = new Tiktoken(tokenizerFile, null, specialTokens: ed.GetSpecialToken());
        Tokenizers["command-r-plus"] = tiktoken;
    }

    private static void DownloadCohereTokenizerSpecifcationFileAndConvertToTiktoken(
        IHttpClientFactory httpClientFactory,
        string definitionLocation,
        string tokenizerFile,
        string tokenizerExtraFile)
    {
        if (!File.Exists(tokenizerFile))
        {
            var client = httpClientFactory.CreateClient();
            var response = client.GetAsync(definitionLocation).Result;
            var content = response.Content.ReadAsStringAsync().Result;
            using (JsonDocument document = JsonDocument.Parse(content))
            {
                //First of all we need to find the node called model
                JsonElement root = document.RootElement;
                JsonElement model = root.GetProperty("model");

                //now from the model object we got a vocab node with the vocabulary
                JsonElement vocab = model.GetProperty("vocab");

                //now open output file and start writing, fore each item in the vocabulary
                //base64 encoding of the key, followed by a space then the token value
                using StreamWriter outputFile = new(tokenizerFile);

                foreach (var item in vocab.EnumerateObject())
                {
                    var token = item.Name;
                    //Special case, if the token is "Ġ" it means space
                    token = token.Replace("Ġ", " ");
                    byte[] bytes = Encoding.UTF8.GetBytes(token);
                    string base64 = Convert.ToBase64String(bytes);
                    outputFile.WriteLine($"{base64} {item.Value}");
                }

                //ok find added_tokens node, this is an array of object with id and content
                JsonElement addedTokens = root.GetProperty("added_tokens");

                List<AddedToken> addedTokensList = new();
                //now for each item in the array, we need to add to the specialTokens dictionary
                foreach (var item in addedTokens.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetInt32();
                    var addedTokenContent = item.GetProperty("content").GetString();
                    //only id creater than 255000 are special tokens
                    if (id > 255000 && addedTokenContent != null)
                    {
                        addedTokensList.Add(new AddedToken(id, addedTokenContent));
                    }
                }

                ExtraTokenizerData extraTokenizerData = new(addedTokensList.ToArray());
                var extraTokenizerDataJson = JsonSerializer.Serialize(extraTokenizerData);
                File.WriteAllText(tokenizerExtraFile, extraTokenizerDataJson);
            }
        }
    }

    internal object CountToken(string modelName, string text)
    {
        if (!Tokenizers.TryGetValue(modelName, out var tokenizer))
        {
            throw new Exception("Tokenizer not found");
        }

        return tokenizer.CountTokens(text);
    }

    private class ExtraTokenizerData
    {
        public ExtraTokenizerData(AddedToken[] addedTokens)
        {
            AddedTokens = addedTokens;
        }

        public AddedToken[] AddedTokens { get; set; }

        internal IReadOnlyDictionary<string, int>? GetSpecialToken()
        {
            return AddedTokens.ToDictionary(a => a.Content, a => a.Id);
        }
    }

    public class AddedToken
    {
        public AddedToken(int id, string content)
        {
            Id = id;
            Content = content;
        }

        public int Id { get; set; }
        public string Content { get; set; }
    }
}
