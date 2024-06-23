using Fasterflect;
using KernelMemory.Extensions.QueryPipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory.MemoryStorage;
using Moq;

namespace KernelMemory.Extensions.FunctionalTests.QueryPipeline;

public class UserQuestionPipelineFactoryTests
{
    [Fact]
    public void Can_resolve_with_basic_registration()
    {
        // Arrange
        ServiceCollection serviceCollection = new ServiceCollection();
        serviceCollection.AddKernelMemoryUserQuestionPipeline();

        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Act
        var factory = serviceProvider.GetService<IUserQuestionPipelineFactory>();

        // Assert
        Assert.NotNull(factory);
    }

    [Fact]
    public void Can_configure_and_resolve_pipeline()
    {
        // Arrange
        ServiceCollection serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<StandardVectorSearchQueryHandler>();

        var mdb = new Mock<IMemoryDb>();
        serviceCollection.AddSingleton(mdb.Object);

        serviceCollection.AddKernelMemoryUserQuestionPipeline(config =>
        {
            config.AddHandler(typeof(StandardVectorSearchQueryHandler));
        });

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var factory = serviceProvider.GetService<IUserQuestionPipelineFactory>()!;

        // Act
        var pipeline = factory.Create();

        // Assert
        Assert.NotNull(pipeline);
        //white box testing, getting private field with fasterflect
        var handlers = pipeline.GetFieldValue("_queryHandlers") as List<IQueryHandler>;
        Assert.NotNull(handlers);
        Assert.Single(handlers);
        Assert.IsType<StandardVectorSearchQueryHandler>(handlers.First());
    }

    [Fact]
    public void Can_configure_and_resolve_pipeline_with_re_ranker()
    {
        // Arrange
        ServiceCollection serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<StandardVectorSearchQueryHandler>();
        serviceCollection.AddSingleton<TestReRanker>();

        var mdb = new Mock<IMemoryDb>();
        serviceCollection.AddSingleton(mdb.Object);

        serviceCollection.AddKernelMemoryUserQuestionPipeline(config =>
        {
            config.AddHandler(typeof(StandardVectorSearchQueryHandler));
            config.ReRanker = typeof(TestReRanker);
        });

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var factory = serviceProvider.GetService<IUserQuestionPipelineFactory>()!;

        // Act
        var pipeline = factory.Create();

        // Assert
        Assert.NotNull(pipeline);
        //white box testing, getting private field with fasterflect
        Assert.IsType<TestReRanker>(pipeline.GetFieldValue("_reRanker"));
    }

    [Fact]
    public void Can_configure_and_resolve_pipeline_with_generics()
    {
        // Arrange
        ServiceCollection serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<StandardVectorSearchQueryHandler>();
        serviceCollection.AddSingleton<TestReRanker>();
        serviceCollection.AddSingleton<TestQueryRewriter>();

        var mdb = new Mock<IMemoryDb>();
        serviceCollection.AddSingleton(mdb.Object);

        serviceCollection.AddKernelMemoryUserQuestionPipeline(config =>
        {
            config
                .AddHandler<StandardVectorSearchQueryHandler>()
                .SetReRanker<TestReRanker>()
                .SetQueryRewriter<TestQueryRewriter>();
        });

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var factory = serviceProvider.GetService<IUserQuestionPipelineFactory>()!;

        // Act
        var pipeline = factory.Create();

        // Assert
        Assert.NotNull(pipeline);
        //white box testing, getting private field with fasterflect
        Assert.IsType<TestReRanker>(pipeline.GetFieldValue("_reRanker"));

        //white box testing, getting private field with fasterflect
        Assert.IsType<TestQueryRewriter>(pipeline.GetFieldValue("_conversationQueryRewriter"));
    }

    [Fact]
    public void Supports_keyed_service()
    {
        // Arrange
        ServiceCollection serviceCollection = new ServiceCollection();

        var mdb1 = new Mock<IMemoryDb>();
        serviceCollection.AddKeyedSingleton("1", mdb1.Object);
        var mdb2 = new Mock<IMemoryDb>();
        serviceCollection.AddKeyedSingleton("2", mdb2.Object);

        //I register handler with key "2" and it depends on IMemoryDb with key "2"
        serviceCollection.AddKeyedSingleton("2", (serviceProvider, _) =>
        {
            var memoryDb = serviceProvider.GetRequiredKeyedService<IMemoryDb>("2");
            return new StandardVectorSearchQueryHandler(memoryDb);
        });

        serviceCollection.AddKeyedSingleton("1", (serviceProvider, _) =>
        {
            var memoryDb = serviceProvider.GetRequiredKeyedService<IMemoryDb>("1");
            return new StandardVectorSearchQueryHandler(memoryDb);
        });

        //Now I want to configure pipeline with handler with key "2"
        ResolveAndAssert(serviceCollection, "1", mdb1.Object);
        ResolveAndAssert(serviceCollection, "2", mdb2.Object);
    }

    private class TestReRanker : IReRanker
    {
        public Task<IReadOnlyCollection<MemoryRecord>> ReRankAsync(string question, IReadOnlyDictionary<string, IReadOnlyCollection<MemoryRecord>> candidates)
        {
           return Task.FromResult<IReadOnlyCollection<MemoryRecord>>(candidates.SelectMany(c => c.Value).ToArray());
        }
    }

    private class TestQueryRewriter : IConversationQueryRewriter
    {
        public Task<string> RewriteAsync(Conversation conversation, string question)
        {
            return Task.FromResult(question);
        }
    }

    private class TextLenghtReRanker : IReRanker
    {
        public Task<IReadOnlyCollection<MemoryRecord>> ReRankAsync(string question, IReadOnlyDictionary<string, IReadOnlyCollection<MemoryRecord>> candidates)
        {
            return Task.FromResult<IReadOnlyCollection<MemoryRecord>>(candidates.SelectMany(c => c.Value).OrderBy(c => c.GetPartitionText().Length).ToArray());
        }
    }

    private static void ResolveAndAssert(ServiceCollection serviceCollection, string key, IMemoryDb expected)
    {
        serviceCollection.AddKernelMemoryUserQuestionPipeline(config =>
        {
            config.AddHandler(typeof(StandardVectorSearchQueryHandler), key);
        });

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var factory = serviceProvider.GetService<IUserQuestionPipelineFactory>()!;

        // Act
        var pipeline = factory.Create();

        // Assert
        Assert.NotNull(pipeline);
        //white box testing, getting private field with fasterflect
        var handlers = pipeline.GetFieldValue("_queryHandlers") as List<IQueryHandler>;
        Assert.NotNull(handlers);
        Assert.Single(handlers);
        Assert.IsType<StandardVectorSearchQueryHandler>(handlers.First());

        //now assert internal memory db of the handler
        var handler = handlers.First() as StandardVectorSearchQueryHandler;
        Assert.NotNull(handler);
        Assert.Same(expected, handler.GetFieldValue("_memoryDb"));
    }
}
