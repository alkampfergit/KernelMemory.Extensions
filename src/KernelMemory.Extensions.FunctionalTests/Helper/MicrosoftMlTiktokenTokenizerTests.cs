using KernelMemory.Extensions.Helper;

namespace KernelMemory.Extensions.FunctionalTests.Helper
{
    public class MicrosoftMlTiktokenTokenizerTests
    {
        [Fact]
        public void Count_token_base()
        {
            var sut = new MicrosoftMlTiktokenTokenizer("text-embedding-3-small");
            var count = sut.CountTokens("hello world I'm a toknizer");
            Assert.Equal(8, count);
        }

        [Theory]
        [InlineData("gpt-4o", 7)]
        [InlineData("GPT-4o", 7)] //Case sensitiveness
        public void Count_token_gpt4o(string model, int expected)
        {
            var sut = new MicrosoftMlTiktokenTokenizer(model);
            var count = sut.CountTokens("hello world I'm a toknizer");
            Assert.Equal(expected, count);
        }
    }
}
