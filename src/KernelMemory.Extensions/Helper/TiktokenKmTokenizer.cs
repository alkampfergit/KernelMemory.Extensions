using Microsoft.KernelMemory.AI;
using Microsoft.ML.Tokenizers;
using System;

namespace KernelMemory.Extensions.Helper
{
    /// <summary>
    /// This text tokenizer uses latest and optimized Tiktoken model directly
    /// from Microsoft packages that offer optimized countTokens method.
    /// </summary>
#pragma warning disable KMEXP00 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public class TiktokenKmTokenizer : ITextTokenizer
#pragma warning restore KMEXP00 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    {
        private Tokenizer _tikToken;

        public TiktokenKmTokenizer(string baseModelName)
        {
            _tikToken = Tiktoken.CreateTiktokenForModel(baseModelName);
        }

        public int CountTokens(string text)
        {
            return _tikToken.CountTokens(text);
        }
    }
}
