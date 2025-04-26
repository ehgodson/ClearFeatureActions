using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clear
{
    public interface INotification
    {
    }

    public interface INotificationHandler<in T> where T : INotification
    {
        bool SupportsConcurrentExecution { get; }

        /// <summary>
        /// Handles the specified notification asynchronously.
        /// </summary>
        /// <param name="notification">The notification to process. Cannot be null.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests..</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task Handle(T notification, CancellationToken cancellationToken);
    }

    public interface INotificationPublisher<in T> where T : INotification
    {
        /// <summary>
        /// Publishes the specified notification to all registered handlers.
        /// </summary>
        /// <remarks>This method invokes the <c>Handle</c> method of each registered handler
        /// asynchronously.  Handlers are executed sequentially in the order they were registered.</remarks>
        /// <param name="notification">The notification to be published. Cannot be null.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests. The operation will terminate early if the token is canceled.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task Publish(T notification, CancellationToken cancellationToken);
    }

    public class NotificationPublisher<T> : INotificationPublisher<T> where T : INotification
    {
        private readonly IEnumerable<INotificationHandler<T>> _handlers;

        public NotificationPublisher(IEnumerable<INotificationHandler<T>> handlers)
        {
            _handlers = handlers;
        }

        /// <summary>
        /// Publishes the specified notification to all registered handlers.
        /// </summary>
        /// <remarks>This method invokes the <c>Handle</c> method of each registered handler
        /// asynchronously.  Handlers are executed sequentially in the order they were registered.</remarks>
        /// <param name="notification">The notification to be published. Cannot be null.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests. The operation will terminate early if the token is canceled.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task Publish(T notification, CancellationToken cancellationToken)
        {
            var tasks = _handlers
                .Where(x => x.SupportsConcurrentExecution)
                .Select(x => x.Handle(notification, cancellationToken))
                .ToArray();

            await Task.WhenAll(tasks);

            foreach (var handler in _handlers.Where(x => !x.SupportsConcurrentExecution))
            {
                await handler.Handle(notification, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}