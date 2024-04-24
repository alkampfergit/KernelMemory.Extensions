using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KernelMemory.Extensions.QueryPipeline
{
    /// <summary>
    /// Allows to create a pipeline for user question given a name and
    /// a configuration.
    /// </summary>
    public interface IUserQuestionPipelineFactory
    {
        public const string DefaultPipelineName = "default";

        /// <summary>
        /// Create a pipeline given a configuration.
        /// </summary>
        /// <param name="pipelineName">Name of configured pipeline, it can be null. If null the "defualt" pipeline key
        /// is used.</param>
        /// <returns></returns>
        UserQuestionPipeline Create(string? pipelineName = DefaultPipelineName);
    }

    internal class UserQuestionPipelineFactory : IUserQuestionPipelineFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public UserQuestionPipelineFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public UserQuestionPipeline Create(string pipelineName = IUserQuestionPipelineFactory.DefaultPipelineName)
        {
            if (string.IsNullOrEmpty(pipelineName))
            {
                throw new ArgumentException($"'{nameof(pipelineName)}' cannot be null or empty.", nameof(pipelineName));
            }

            //we need to create the pipeline
            var pipeline = new UserQuestionPipeline();

            //we need to get the configuration
            var uqpc = _serviceProvider.GetRequiredKeyedService<UserQuestionPipelineConfiguration>(pipelineName ?? IUserQuestionPipelineFactory.DefaultPipelineName);
            if (uqpc == null)
            {
                throw new InvalidOperationException($"No configuration found for pipeline {pipelineName}");
            }
            foreach (var handlerConfig in uqpc.Handlers)
            {
                //resolve the handler and add to the pipeline
                IQueryHandler handler;
                if (handlerConfig.KeyedName == null)
                {
                    handler = (IQueryHandler)_serviceProvider.GetRequiredService(handlerConfig.HandlerType);
                }
                else
                {
                    handler = (IQueryHandler)_serviceProvider.GetRequiredKeyedService(handlerConfig.HandlerType, handlerConfig.KeyedName);
                }
                pipeline.AddHandler(handler);
            }

            if (uqpc.ReRanker != null)
            {
                pipeline.SetReRanker((IReRanker)_serviceProvider.GetRequiredService(uqpc.ReRanker));
            }

            return pipeline;
        }
    }

    public class UserQuestionPipelineConfiguration
    {
        public UserQuestionPipelineConfiguration(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }

        public List<HandlerConfig> Handlers { get; private set; } = new();

        public Type? ReRanker { get; set; }

        public UserQuestionPipelineConfiguration SetReRanker<T>() where T : IReRanker
        {
            ReRanker = typeof(T);
            return this;
        }

        public UserQuestionPipelineConfiguration AddHandler<T>() where T : IQueryHandler
        {
            return AddHandler(typeof(T));
        }

        public UserQuestionPipelineConfiguration AddHandler<T>(object keyedName) where T : IQueryHandler
        {
            return AddHandler(typeof(T), keyedName);
        }

        public UserQuestionPipelineConfiguration AddHandler(Type type)
        {
            Handlers.Add(new HandlerConfig(type, null));
            return this;
        }

        public UserQuestionPipelineConfiguration AddHandler(Type type, object keyedName)
        {
            Handlers.Add(new HandlerConfig(type, keyedName));
            return this;
        }
    }

    public record HandlerConfig(Type HandlerType, Object? KeyedName);

    public static class UserQuestionPipelineConfigurationExtension
    {
        public static IServiceCollection AddKernelMemoryUserQuestionPipeline(
           this IServiceCollection services)
        {
            return AddKernelMemoryUserQuestionPipeline(services, _ => { });
        }

        public static IServiceCollection AddKernelMemoryUserQuestionPipeline(
            this IServiceCollection services,
            Action<UserQuestionPipelineConfiguration> config)
        {
            return AddKernelMemoryUserQuestionPipeline(services, IUserQuestionPipelineFactory.DefaultPipelineName, config);
        }

        public static IServiceCollection AddKernelMemoryUserQuestionPipeline(
           this IServiceCollection services,
           string name,
           Action<UserQuestionPipelineConfiguration> config)
        {
            bool isRegistered = services.Any(serviceDescriptor => serviceDescriptor.ServiceType == typeof(IUserQuestionPipelineFactory));
            if (!isRegistered)
            {
                services.AddSingleton<IUserQuestionPipelineFactory, UserQuestionPipelineFactory>();
            }

            var uqpc = new UserQuestionPipelineConfiguration(name);
            config(uqpc);
            services.AddKeyedSingleton(name, uqpc);
            return services;
        }
    }
}
