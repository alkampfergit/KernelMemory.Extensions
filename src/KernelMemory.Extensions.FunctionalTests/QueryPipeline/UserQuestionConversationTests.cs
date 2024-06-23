using KernelMemory.Extensions.Cohere;
using KernelMemory.Extensions.QueryPipeline;

namespace KernelMemory.Extensions.FunctionalTests.QueryPipeline;

public class UserQuestionConversationTests
{
    [Fact]
    public void Can_add_conversation()
    {
        var sut = GenerateSut("");

        var conversation = new Conversation();
        conversation.AddQuestion("Color of sky", "green");
        sut.SetConversation(conversation);

        Assert.Single(sut.Conversation!.GetQuestions());

        //Verify conversation data
        var question = sut.Conversation.GetQuestions().First();
        Assert.Equal("Color of sky", question.Question);
        Assert.Equal("green", question.Answer);
        Assert.True(question.Answered);
    }

    [Fact]
    public void Can_continue_conversation()
    {
        var sut = GenerateSut("What is water");
        sut.Answer = "di-hydrogen monoxide";
        sut.Citations = new List<Microsoft.KernelMemory.Citation> ();
        sut.ExtendedCitation = new List<ExtendedCitation>();

        //This will contine conversation, it should promote old question and answer to the conversation
        sut.ContinueConversation("What is the color of the sky");

        //Verify conversation
        Assert.Single(sut.Conversation!.GetQuestions());

        var question = sut.Conversation.GetQuestions().First();
        Assert.Equal("What is water", question.Question);
        Assert.Equal("di-hydrogen monoxide", question.Answer);
        Assert.True(question.Answered);

        Assert.False(sut.Answered);
        Assert.Null(sut.Answer);
        Assert.Equal("What is the color of the sky", sut.Question);

        Assert.Null(sut.Citations);
        Assert.Null(sut.ExtendedCitation);
    }

    [Fact]
    public void Can_continue_conversation_with_unanswered_question()
    {
        var sut = GenerateSut("What is water");
        sut.Answer = null;

        //This will contine conversation, it should promote old question and answer to the conversation
        sut.ContinueConversation("What is the color of the sky");

        //Verify conversation
        Assert.Single(sut.Conversation!.GetQuestions());

        var question = sut.Conversation.GetQuestions().First();
        Assert.Equal("What is water", question.Question);
        Assert.Null(question.Answer);
        Assert.False(question.Answered);
    }

    [Fact]
    public void Can_add_saved_conversation_to_userQuestion() 
    {
        var sut = GenerateSut("What is water");
        sut.Answer = "di-hydrogen monoxide";

        var conversation = new Conversation();
        conversation.AddQuestion("Color of sky", "green");

        //We are able to set a conversation.
        sut.SetConversation(conversation);

        //Verify conversation
        Assert.Single(sut.Conversation!.GetQuestions());
    }

    private UserQuestion GenerateSut(string question)
    {
        return new UserQuestion(GenerateOptions(), question);
    }

    private UserQueryOptions GenerateOptions()
    {
        return new UserQueryOptions("index");
    }
}