//using Microsoft.ML.Tokenizers;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Net.Http;

//namespace KernelMemory.Extensions.Cohere
//{
//    /// <summary>
//    /// Try to implement a tokenizer for Cohere
//    /// </summary>
//    public class CohereTokenizer
//    {
//        public Dictionary<string, TiktokenSharp.TikToken> Tokenizers { get; set; } = new();

//        public CohereTokenizer(IHttpClientFactory httpClientFactory)
//        {
//            var tokenizerFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "command-r-plus.json");

//            if (!File.Exists(tokenizerFile))
//            {
//                //Download from https://storage.googleapis.com/cohere-public/tokenizers/command-r-plus.json
//                var client = httpClientFactory.CreateClient();
//                var response = client.GetAsync("https://storage.googleapis.com/cohere-public/tokenizers/command-r-plus.json").Result;
//                var content = response.Content.ReadAsStringAsync().Result;
//                File.WriteAllText(tokenizerFile, content);
//                var bpe = new Bpe(tokenizerFile, null);
//            }
//        }
//    }
//}
