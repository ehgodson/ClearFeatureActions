using FluentResults;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Clear
{
    public interface IRequest<TResponse>
    {
    }

    public interface IRequest : IRequest<bool>
    { }

    public interface IRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        Task<Result<TResponse>> Handle(TRequest command, CancellationToken cancellationToken);
    }

    public interface IFeatureAction<TRequest> : IFeatureAction<TRequest, bool>
        where TRequest : IRequest
    { }

    public interface IFeatureAction<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        Task<Result<TResponse>> Execute(TRequest command, CancellationToken cancellationToken);
    }

    public abstract class BaseFeatureAction<TRequest, TResponse> : IFeatureAction<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IRequestHandler<TRequest, TResponse> _handler;
        private readonly IValidator<TRequest> _validator;

        protected BaseFeatureAction(
            IRequestHandler<TRequest, TResponse> handler,
            IValidator<TRequest> validator = null)
        {
            _handler = handler;
            _validator = validator;
        }

        public async Task<Result<TResponse>> Execute(TRequest command, CancellationToken cancellationToken)
        {
            if (_validator != null)
            {
                var validationResult = await _validator.ValidateAsync(command, cancellationToken);
                if (!validationResult.IsValid)
                {
                    return Result.Fail(validationResult.Errors.Select(e => e.ErrorMessage).ToList());
                }
            }

            return await _handler.Handle(command, cancellationToken);
        }
    }

    internal class FeatureAction<TRequest, TResponse> : BaseFeatureAction<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public FeatureAction(
            IRequestHandler<TRequest, TResponse> handler,
            IValidator<TRequest> validator = null)
            : base(handler, validator)
        {
        }
    }

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFeatureActions(this IServiceCollection services, Assembly assembly)
        {
            List<Type> featureActionTypes = GetFeatureActionTypes(assembly);

            foreach (var actionType in featureActionTypes)
            {
                var requestInterface = actionType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));
                if (requestInterface == null) continue;

                var responseType = requestInterface.GetGenericArguments()[0];
                var handlerType = typeof(IRequestHandler<,>).MakeGenericType(actionType, responseType);
                var validatorType = typeof(IValidator<>).MakeGenericType(actionType);
                var featureExecutorType = typeof(FeatureAction<,>).MakeGenericType(actionType, responseType);
                var featureExecutorInterface = typeof(IFeatureAction<,>).MakeGenericType(actionType, responseType);

                // Register the request handler
                var handlerImplementation = assembly.ExportedTypes.FirstOrDefault(t => handlerType.IsAssignableFrom(t));
                if (handlerImplementation != null)
                {
                    services.AddScoped(handlerType, handlerImplementation);
                }

                // Register the validator if available
                var validatorImplementation = assembly.ExportedTypes.FirstOrDefault(t => validatorType.IsAssignableFrom(t));
                if (validatorImplementation != null)
                {
                    services.AddScoped(validatorType, validatorImplementation);
                }

                // Register the feature action
                services.AddScoped(featureExecutorInterface, featureExecutorType);
            }

            return services;
        }

        private static List<Type> GetFeatureActionTypes(Assembly assembly)
        {
            var types = assembly.ExportedTypes
                .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
                .ToList();

            return types ?? new List<Type>();
        }
    }
}