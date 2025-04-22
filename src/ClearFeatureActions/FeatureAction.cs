using FluentResults;
using FluentValidation;
using System.Linq;
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
        /// <summary>
        /// Handles the specified command and returns the result of the operation.
        /// </summary>
        /// <param name="command">The command to be processed. Cannot be <see langword="null"/>.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests. Passing a canceled token will result in the operation being
        /// canceled.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a <see
        /// cref="Result{TResponse}"/> representing the outcome of the operation.</returns>
        Task<Result<TResponse>> Handle(TRequest command, CancellationToken cancellationToken);
    }

    public interface IFeatureAction<TRequest> : IFeatureAction<TRequest, bool>
        where TRequest : IRequest
    { }

    public interface IFeatureAction<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Executes the specified command by validating it and then passing it to the handler for processing.
        /// </summary>
        /// <remarks>If a validator is provided, the command is validated before being passed to the
        /// handler.  If validation fails, the method returns a failure result containing the validation errors.
        /// Otherwise, the command is processed by the handler, and the result of the handler's execution is
        /// returned.</remarks>
        /// <param name="command">The command to be executed. Must conform to the expected structure and requirements of the handler.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests, which can be used to cancel the operation.</param>
        /// <returns>A <see cref="Result{TResponse}"/> containing the result of the command execution.  If validation fails, the
        /// result will contain the validation error messages.</returns>
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

        /// <summary>
        /// Executes the specified command by validating it and then passing it to the handler for processing.
        /// </summary>
        /// <remarks>If a validator is provided, the command is validated before being passed to the
        /// handler.  If validation fails, the method returns a failure result containing the validation errors.
        /// Otherwise, the command is processed by the handler, and the result of the handler's execution is
        /// returned.</remarks>
        /// <param name="command">The command to be executed. Must conform to the expected structure and requirements of the handler.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests, which can be used to cancel the operation.</param>
        /// <returns>A <see cref="Result{TResponse}"/> containing the result of the command execution.  If validation fails, the
        /// result will contain the validation error messages.</returns>
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
}