using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Clear.FeatureActions
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
            var allTypes = GetAllTypes(assembly);

            var featureActionTypes = GetFeatureActionTypes(allTypes);

            foreach (var actionType in featureActionTypes)
            {
                var requestInterface = actionType.GetInterfaces()
                    .FirstOrDefault(i => i == typeof(IRequest));

                if (requestInterface == null)
                {
                    AddWithResponse(services, actionType, allTypes);
                }
                else
                {
                    AddWithNoResponse(services, actionType, allTypes);
                }
            }

            return services;
        }

        private static void AddWithResponse(IServiceCollection services, Type actionType, IEnumerable<Type> allTypes)
        {
            var requestInterface = actionType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));
            if (requestInterface == null) return;

            // Handle IRequest<TResponse>
            var responseType = requestInterface.GetGenericArguments()[0];
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(actionType, responseType);
            var validatorType = typeof(IValidator<>).MakeGenericType(actionType);
            var featureExecutorType = typeof(FeatureAction<,>).MakeGenericType(actionType, responseType);
            var featureExecutorInterface = typeof(IFeatureAction<,>).MakeGenericType(actionType, responseType);

            // Register the request handler
            var handlerImplementation = SearchByType(allTypes, handlerType).FirstOrDefault();
            if (handlerImplementation != null)
            {
                services.AddScoped(handlerType, handlerImplementation);
            }

            // Register the validator if available
            var validatorImplementation = SearchByType(allTypes, validatorType).FirstOrDefault();
            if (validatorImplementation != null)
            {
                services.AddScoped(validatorType, validatorImplementation);
            }

            // Register the feature action
            services.AddScoped(featureExecutorInterface, featureExecutorType);
        }

        private static void AddWithNoResponse(IServiceCollection services, Type actionType, IEnumerable<Type> allTypes)
        {
            // Handle IRequest (without TResponse)
            var handlerType = typeof(IRequestHandler<>).MakeGenericType(actionType);
            var validatorType = typeof(IValidator<>).MakeGenericType(actionType);
            var featureExecutorType = typeof(FeatureAction<>).MakeGenericType(actionType);
            var featureExecutorInterface = typeof(IFeatureAction<>).MakeGenericType(actionType);

            // Register the request handler
            var handlerImplementation = SearchByType(allTypes, handlerType).FirstOrDefault();
            if (handlerImplementation != null)
            {
                services.AddScoped(handlerType, handlerImplementation);
            }

            // Register the validator if available
            var validatorImplementation = SearchByType(allTypes, validatorType).FirstOrDefault();
            if (validatorImplementation != null)
            {
                services.AddScoped(validatorType, validatorImplementation);
            }

            // Register the feature action
            services.AddScoped(featureExecutorInterface, featureExecutorType);
        }

        public static IServiceCollection AddNotificationPublishers(this IServiceCollection services, Assembly assembly = null)
        {
            var allTypes = GetAllTypes(assembly);

            foreach (var notificationType in GetNotificationTypes(allTypes))
            {
                var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);
                var notificationPublisherType = typeof(NotificationPublisher<>).MakeGenericType(notificationType);
                var notificationPublisherInterface = typeof(INotificationPublisher<>).MakeGenericType(notificationType);

                // Register the request handlers
                foreach (var handlerImplementation in SearchByType(allTypes, handlerType))
                {
                    services.AddScoped(handlerType, handlerImplementation);
                }

                // Register the feature action
                services.AddScoped(notificationPublisherInterface, notificationPublisherType);
            }

            return services;
        }

        private static IEnumerable<Type> GetAllTypes(Assembly assembly)
        {
            return assembly == null
                ? AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.ExportedTypes)
                : assembly.ExportedTypes;
        }

        private static IEnumerable<Type> GetFeatureActionTypes(IEnumerable<Type> exportedTypes)
        {
            return exportedTypes
                .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
                ?? new List<Type>();
        }

        private static IEnumerable<Type> GetNotificationTypes(IEnumerable<Type> exportedTypes)
        {
            return exportedTypes
                .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i == typeof(INotification)))
                ?? new List<Type>();
        }

        private static IEnumerable<Type> SearchByType(IEnumerable<Type> exportedTypes, Type type)
        {
            return exportedTypes.Where(t => type.IsAssignableFrom(t));
        }
    }
}