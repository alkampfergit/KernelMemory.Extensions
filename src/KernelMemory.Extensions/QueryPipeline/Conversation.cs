using System.Collections.Generic;

namespace KernelMemory.Extensions.QueryPipeline;

/// <summary>
/// A conversation is a series of question/answers that were made before another question.
/// </summary>
public class Conversation
{
    private readonly List<ConversationPart> _questions = [];

    public void AddQuestion(string question, string answer)
    {
        var answeredQuestion = new ConversationPart(question, answer, true);
        _questions.Add(answeredQuestion);
    }

    public void AddUnansweredQuestion(string question)
    {
        var unansweredQuestion = new ConversationPart(question, null, false);
        _questions.Add(unansweredQuestion);
    }

    public IEnumerable<ConversationPart> GetQuestions()
    {
        return _questions;
    }

    internal void AddQuestions(IEnumerable<ConversationPart> enumerable)
    {
        _questions.AddRange(enumerable);
    }
}

public record ConversationPart(string Question, string? Answer, bool Answered);
