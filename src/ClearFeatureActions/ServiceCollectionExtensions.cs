using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Clear
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers feature action types, their corresponding request handlers, and validators into the service
        /// collection.
        /// </summary>
        /// <remarks>This method scans the specified assembly for types implementing the <see
        /// cref="IRequest{TResponse}"/> interface  and registers the corresponding request handlers, validators, and
        /// feature action executors into the service collection. If a request handler or validator is not found for a
        /// feature action, it will not be registered.</remarks>
        /// <param name="services">The <see cref="IServiceCollection"/> to which the feature actions, handlers, and validators will be added.</param>
        /// <param name="assembly">The assembly to scan for feature action types, request handlers, and validators.  If <see langword="null"/>,
        /// the entire assemblies in the current domain are scanned.</param>
        /// <returns>The updated <see cref="IServiceCollection"/> with the registered feature actions, handlers, and validators.</returns>
        public static IServiceCollection AddFeatureActions(this IServiceCollection services, Assembly assembly = null)
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
            var exportedTypes = assembly == null
                ? AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.ExportedTypes)
                : assembly.ExportedTypes;

            return exportedTypes
                .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
                .ToList() ?? new List<Type>();
        }
    }
}