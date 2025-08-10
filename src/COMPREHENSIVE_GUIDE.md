# ClearFeatureActions - Comprehensive Developer Guide

## Table of Contents
1. [Overview](#overview)
2. [Installation](#installation)
3. [Core Concepts](#core-concepts)
4. [Feature Actions](#feature-actions)
5. [Notification System](#notification-system)
6. [Dependency Injection](#dependency-injection)
7. [Advanced Usage Patterns](#advanced-usage-patterns)
8. [Best Practices](#best-practices)
9. [Error Handling](#error-handling)
10. [Performance Considerations](#performance-considerations)
11. [Testing Strategies](#testing-strategies)
12. [Migration Guide](#migration-guide)
13. [API Reference](#api-reference)
14. [Troubleshooting](#troubleshooting)

---

## Overview

ClearFeatureActions is a powerful .NET framework that implements the Command Query Responsibility Segregation (CQRS) pattern with built-in validation and notification capabilities. It provides a clean, maintainable architecture for handling business logic through feature actions and event notifications.

### Key Features
- **Feature Actions**: Encapsulate request handling with built-in validation
- **Notification System**: Publisher-subscriber pattern with concurrent and sequential execution
- **FluentValidation Integration**: Robust input validation before processing
- **FluentResults Integration**: Comprehensive error handling and result management
- **Dependency Injection**: Automatic registration and resolution
- **Performance Optimized**: Support for concurrent execution where appropriate

### Target Frameworks
- .NET Standard 2.0 (for maximum compatibility)
- .NET 9.0 (for latest features)

---

## Installation

### Package Manager Console
```powershell
Install-Package ClearFeatureActions -Version 1.2.0
```

### .NET CLI
```bash
dotnet add package ClearFeatureActions --version 1.2.0
```

### PackageReference
```xml
<PackageReference Include="ClearFeatureActions" Version="1.2.0" />
```

### Dependencies
ClearFeatureActions automatically includes these dependencies:
- FluentResults (3.16.0)
- FluentValidation (11.11.0)
- Microsoft.Extensions.Caching.Abstractions (9.0.3)
- Microsoft.Extensions.DependencyInjection.Abstractions (9.0.3)

---

## Core Concepts

### Architecture Overview
```
Request ? Validator ? Handler ? Response
    ?         ?         ?        ?
 IRequest  IValidator IHandler  Result<T>
```

### Key Interfaces
- **IRequest<TResponse>**: Defines a request that expects a specific response type
- **IRequest**: Defines a request that returns a boolean (shorthand for IRequest<bool>)
- **IRequestHandler<TRequest, TResponse>**: Handles business logic for requests
- **IFeatureAction<TRequest, TResponse>**: Orchestrates validation and handling
- **INotification**: Defines an event notification
- **INotificationHandler<TNotification>**: Handles notifications
- **INotificationPublisher<TNotification>**: Publishes notifications to handlers

---

## Feature Actions

### Basic Request and Handler

#### Step 1: Define a Request
```csharp
using Clear.FeatureActions;

public class GetUserByIdRequest : IRequest<User>
{
    public int UserId { get; }

    public GetUserByIdRequest(int userId)
    {
        UserId = userId;
    }
}
```

#### Step 2: Implement the Handler
```csharp
using Clear.FeatureActions;
using FluentResults;

public class GetUserByIdHandler : IRequestHandler<GetUserByIdRequest, User>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetUserByIdHandler> _logger;

    public GetUserByIdHandler(
        IUserRepository userRepository, 
        ILogger<GetUserByIdHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<Result<User>> Handle(
        GetUserByIdRequest request, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving user with ID: {UserId}", request.UserId);

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        
        if (user == null)
        {
            return Result.Fail($"User with ID {request.UserId} not found");
        }

        return Result.Ok(user);
    }
}
```

#### Step 3: Add Validation (Optional)
```csharp
using FluentValidation;

public class GetUserByIdValidator : AbstractValidator<GetUserByIdRequest>
{
    public GetUserByIdValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .WithMessage("User ID must be a positive number");
    }
}
```

### Requests Without Response Type

For operations that don't need to return data:

```csharp
public class DeleteUserRequest : IRequest
{
    public int UserId { get; }

    public DeleteUserRequest(int userId)
    {
        UserId = userId;
    }
}

public class DeleteUserHandler : IRequestHandler<DeleteUserRequest>
{
    private readonly IUserRepository _userRepository;

    public DeleteUserHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<bool>> Handle(
        DeleteUserRequest request, 
        CancellationToken cancellationToken)
    {
        var success = await _userRepository.DeleteAsync(request.UserId, cancellationToken);
        return Result.Ok(success);
    }
}
```

### Complex Request Examples

#### Multi-Parameter Request
```csharp
public class SearchUsersRequest : IRequest<IEnumerable<User>>
{
    public string SearchTerm { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public UserSortOrder SortOrder { get; }

    public SearchUsersRequest(
        string searchTerm, 
        int pageNumber = 1, 
        int pageSize = 10, 
        UserSortOrder sortOrder = UserSortOrder.LastNameAsc)
    {
        SearchTerm = searchTerm;
        PageNumber = pageNumber;
        PageSize = pageSize;
        SortOrder = sortOrder;
    }
}

public class SearchUsersValidator : AbstractValidator<SearchUsersRequest>
{
    public SearchUsersValidator()
    {
        RuleFor(x => x.SearchTerm)
            .NotEmpty()
            .MinimumLength(2)
            .WithMessage("Search term must be at least 2 characters");

        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be positive");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100");
    }
}
```

#### Request with File Upload
```csharp
public class UploadUserAvatarRequest : IRequest<string>
{
    public int UserId { get; }
    public Stream FileStream { get; }
    public string FileName { get; }
    public string ContentType { get; }

    public UploadUserAvatarRequest(
        int userId, 
        Stream fileStream, 
        string fileName, 
        string contentType)
    {
        UserId = userId;
        FileStream = fileStream;
        FileName = fileName;
        ContentType = contentType;
    }
}

public class UploadUserAvatarValidator : AbstractValidator<UploadUserAvatarRequest>
{
    private static readonly string[] AllowedContentTypes = 
    {
        "image/jpeg", "image/png", "image/gif"
    };

    public UploadUserAvatarValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0);

        RuleFor(x => x.FileStream)
            .NotNull()
            .Must(stream => stream.Length > 0 && stream.Length <= 5 * 1024 * 1024)
            .WithMessage("File must be between 1 byte and 5MB");

        RuleFor(x => x.ContentType)
            .Must(contentType => AllowedContentTypes.Contains(contentType))
            .WithMessage("Only JPEG, PNG, and GIF images are allowed");
    }
}
```

---

## Notification System

### Basic Notification

#### Step 1: Define a Notification
```csharp
using Clear.FeatureActions;

public class UserCreatedNotification : INotification
{
    public int UserId { get; }
    public string Email { get; }
    public string FirstName { get; }
    public string LastName { get; }
    public DateTime CreatedAt { get; }

    public UserCreatedNotification(
        int userId, 
        string email, 
        string firstName, 
        string lastName, 
        DateTime createdAt)
    {
        UserId = userId;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        CreatedAt = createdAt;
    }
}
```

#### Step 2: Implement Notification Handlers
```csharp
// Handler for sending welcome email (can run concurrently)
public class SendWelcomeEmailHandler : INotificationHandler<UserCreatedNotification>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<SendWelcomeEmailHandler> _logger;

    public bool SupportsConcurrentExecution => true;

    public SendWelcomeEmailHandler(
        IEmailService emailService, 
        ILogger<SendWelcomeEmailHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(
        UserCreatedNotification notification, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending welcome email to user {UserId}", 
            notification.UserId);

        await _emailService.SendWelcomeEmailAsync(
            notification.Email,
            notification.FirstName,
            cancellationToken);

        _logger.LogInformation(
            "Welcome email sent to user {UserId}", 
            notification.UserId);
    }
}

// Handler for audit logging (must run sequentially)
public class AuditUserCreationHandler : INotificationHandler<UserCreatedNotification>
{
    private readonly IAuditService _auditService;

    public bool SupportsConcurrentExecution => false;

    public AuditUserCreationHandler(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public async Task Handle(
        UserCreatedNotification notification, 
        CancellationToken cancellationToken = default)
    {
        await _auditService.LogEventAsync(
            "UserCreated",
            new { notification.UserId, notification.Email, notification.CreatedAt },
            cancellationToken);
    }
}

// Handler for updating metrics (can run concurrently)
public class UpdateUserMetricsHandler : INotificationHandler<UserCreatedNotification>
{
    private readonly IMetricsService _metricsService;

    public bool SupportsConcurrentExecution => true;

    public UpdateUserMetricsHandler(IMetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    public async Task Handle(
        UserCreatedNotification notification, 
        CancellationToken cancellationToken = default)
    {
        await _metricsService.IncrementCounterAsync("users.created", cancellationToken);
    }
}
```

### Notification with Data Payload

```csharp
public class OrderProcessedNotification : INotification<OrderData>
{
    public OrderData Data { get; }

    public OrderProcessedNotification(OrderData data)
    {
        Data = data;
    }
}

public class OrderData
{
    public int OrderId { get; set; }
    public decimal TotalAmount { get; set; }
    public int CustomerId { get; set; }
    public List<OrderItem> Items { get; set; }
    public DateTime ProcessedAt { get; set; }
}
```

### Execution Modes

#### Concurrent Execution
Handlers with `SupportsConcurrentExecution = true` run in parallel:
```csharp
public class EmailNotificationHandler : INotificationHandler<UserCreatedNotification>
{
    public bool SupportsConcurrentExecution => true; // Runs concurrently
    
    public async Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        // This can run in parallel with other concurrent handlers
        await SendEmailAsync(notification);
    }
}
```

#### Sequential Execution
Handlers with `SupportsConcurrentExecution = false` run one after another:
```csharp
public class DatabaseUpdateHandler : INotificationHandler<UserCreatedNotification>
{
    public bool SupportsConcurrentExecution => false; // Runs sequentially
    
    public async Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        // This runs after all concurrent handlers complete
        await UpdateDatabaseAsync(notification);
    }
}
```

---

## Dependency Injection

### Automatic Registration

#### Register Feature Actions
```csharp
using Clear.FeatureActions;
using System.Reflection;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register all feature actions from the current assembly
        services.AddFeatureActions(Assembly.GetExecutingAssembly());
        
        // Register all feature actions from a specific assembly
        services.AddFeatureActions(typeof(GetUserByIdRequest).Assembly);
        
        // Register from multiple assemblies
        services.AddFeatureActions(Assembly.GetExecutingAssembly());
        services.AddFeatureActions(typeof(SomeOtherRequest).Assembly);
    }
}
```

#### Register Notification Publishers
```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register notification publishers and handlers
        services.AddNotificationPublishers(Assembly.GetExecutingAssembly());
        
        // You can combine both registrations
        services.AddFeatureActions(Assembly.GetExecutingAssembly());
        services.AddNotificationPublishers(Assembly.GetExecutingAssembly());
    }
}
```

#### Manual Registration
For more control, you can register components manually:
```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Manual registration of specific components
    services.AddScoped<IRequestHandler<GetUserByIdRequest, User>, GetUserByIdHandler>();
    services.AddScoped<IValidator<GetUserByIdRequest>, GetUserByIdValidator>();
    services.AddScoped<IFeatureAction<GetUserByIdRequest, User>, FeatureAction<GetUserByIdRequest, User>>();
    
    services.AddScoped<INotificationHandler<UserCreatedNotification>, SendWelcomeEmailHandler>();
    services.AddScoped<INotificationPublisher<UserCreatedNotification>, NotificationPublisher<UserCreatedNotification>>();
}
```

### Using in Controllers

#### ASP.NET Core Controller
```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IFeatureAction<GetUserByIdRequest, User> _getUserAction;
    private readonly IFeatureAction<CreateUserRequest, User> _createUserAction;
    private readonly INotificationPublisher<UserCreatedNotification> _userCreatedPublisher;

    public UsersController(
        IFeatureAction<GetUserByIdRequest, User> getUserAction,
        IFeatureAction<CreateUserRequest, User> createUserAction,
        INotificationPublisher<UserCreatedNotification> userCreatedPublisher)
    {
        _getUserAction = getUserAction;
        _createUserAction = createUserAction;
        _userCreatedPublisher = userCreatedPublisher;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id, CancellationToken cancellationToken)
    {
        var result = await _getUserAction.Execute(
            new GetUserByIdRequest(id), 
            cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(result.Errors);

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(
        CreateUserDto dto, 
        CancellationToken cancellationToken)
    {
        var request = new CreateUserRequest(dto.Email, dto.FirstName, dto.LastName);
        var result = await _createUserAction.Execute(request, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(result.Errors);

        // Publish notification after successful creation
        var notification = new UserCreatedNotification(
            result.Value.Id,
            result.Value.Email,
            result.Value.FirstName,
            result.Value.LastName,
            result.Value.CreatedAt);

        await _userCreatedPublisher.Publish(notification, cancellationToken);

        return CreatedAtAction(
            nameof(GetUser), 
            new { id = result.Value.Id }, 
            result.Value);
    }
}
```

#### Using in Services
```csharp
public class UserService
{
    private readonly IFeatureAction<GetUserByIdRequest, User> _getUserAction;
    private readonly IFeatureAction<UpdateUserRequest, User> _updateUserAction;

    public UserService(
        IFeatureAction<GetUserByIdRequest, User> getUserAction,
        IFeatureAction<UpdateUserRequest, User> updateUserAction)
    {
        _getUserAction = getUserAction;
        _updateUserAction = updateUserAction;
    }

    public async Task<Result<User>> GetUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _getUserAction.Execute(new GetUserByIdRequest(userId), cancellationToken);
    }

    public async Task<Result<User>> UpdateUserAsync(
        int userId, 
        string email, 
        string firstName, 
        string lastName, 
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateUserRequest(userId, email, firstName, lastName);
        return await _updateUserAction.Execute(request, cancellationToken);
    }
}
```

---

## Advanced Usage Patterns

### Pipeline Behavior with Decorators

Create reusable cross-cutting concerns:

```csharp
public class LoggingFeatureActionDecorator<TRequest, TResponse> : IFeatureAction<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IFeatureAction<TRequest, TResponse> _inner;
    private readonly ILogger<LoggingFeatureActionDecorator<TRequest, TResponse>> _logger;

    public LoggingFeatureActionDecorator(
        IFeatureAction<TRequest, TResponse> inner,
        ILogger<LoggingFeatureActionDecorator<TRequest, TResponse>> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<Result<TResponse>> Execute(TRequest command, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Executing {RequestName}", requestName);

        var stopwatch = Stopwatch.StartNew();
        var result = await _inner.Execute(command, cancellationToken);
        stopwatch.Stop();

        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "Successfully executed {RequestName} in {ElapsedMs}ms", 
                requestName, 
                stopwatch.ElapsedMilliseconds);
        }
        else
        {
            _logger.LogWarning(
                "Failed to execute {RequestName} in {ElapsedMs}ms. Errors: {Errors}", 
                requestName, 
                stopwatch.ElapsedMilliseconds, 
                string.Join(", ", result.Errors.Select(e => e.Message)));
        }

        return result;
    }
}
```

### Caching with Decorators

```csharp
public class CachingFeatureActionDecorator<TRequest, TResponse> : IFeatureAction<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ICacheable
{
    private readonly IFeatureAction<TRequest, TResponse> _inner;
    private readonly IMemoryCache _cache;

    public CachingFeatureActionDecorator(
        IFeatureAction<TRequest, TResponse> inner,
        IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<Result<TResponse>> Execute(TRequest command, CancellationToken cancellationToken)
    {
        var cacheKey = command.GetCacheKey();
        
        if (_cache.TryGetValue(cacheKey, out Result<TResponse> cachedResult))
        {
            return cachedResult;
        }

        var result = await _inner.Execute(command, cancellationToken);
        
        if (result.IsSuccess)
        {
            _cache.Set(cacheKey, result, command.CacheExpiration);
        }

        return result;
    }
}

public interface ICacheable
{
    string GetCacheKey();
    TimeSpan CacheExpiration { get; }
}
```

### Conditional Execution

```csharp
public class ConditionalNotificationHandler<T> : INotificationHandler<T> where T : INotification
{
    private readonly INotificationHandler<T> _inner;
    private readonly Func<T, bool> _condition;

    public bool SupportsConcurrentExecution => _inner.SupportsConcurrentExecution;

    public ConditionalNotificationHandler(
        INotificationHandler<T> inner,
        Func<T, bool> condition)
    {
        _inner = inner;
        _condition = condition;
    }

    public async Task Handle(T notification, CancellationToken cancellationToken)
    {
        if (_condition(notification))
        {
            await _inner.Handle(notification, cancellationToken);
        }
    }
}
```

### Retry Logic

```csharp
public class RetryFeatureActionDecorator<TRequest, TResponse> : IFeatureAction<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IFeatureAction<TRequest, TResponse> _inner;
    private readonly int _maxRetries;
    private readonly TimeSpan _delay;

    public RetryFeatureActionDecorator(
        IFeatureAction<TRequest, TResponse> inner,
        int maxRetries = 3,
        TimeSpan? delay = null)
    {
        _inner = inner;
        _maxRetries = maxRetries;
        _delay = delay ?? TimeSpan.FromSeconds(1);
    }

    public async Task<Result<TResponse>> Execute(TRequest command, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var result = await _inner.Execute(command, cancellationToken);
                if (result.IsSuccess || !IsRetryableError(result))
                {
                    return result;
                }

                if (attempt < _maxRetries)
                {
                    await Task.Delay(_delay, cancellationToken);
                }
            }
            catch (Exception ex) when (IsRetryableException(ex) && attempt < _maxRetries)
            {
                await Task.Delay(_delay, cancellationToken);
            }
        }

        return await _inner.Execute(command, cancellationToken);
    }

    private bool IsRetryableError(Result<TResponse> result)
    {
        // Define which errors are retryable
        return result.Errors.Any(e => e.Message.Contains("timeout") || e.Message.Contains("network"));
    }

    private bool IsRetryableException(Exception ex)
    {
        return ex is TimeoutException || ex is HttpRequestException;
    }
}
```

---

## Best Practices

### Request Design

#### 1. Immutable Requests
```csharp
public class CreateUserRequest : IRequest<User>
{
    public string Email { get; }
    public string FirstName { get; }
    public string LastName { get; }

    public CreateUserRequest(string email, string firstName, string lastName)
    {
        Email = email;
        FirstName = firstName;
        LastName = lastName;
    }
}
```

#### 2. Single Responsibility
Each request should represent a single business operation:
```csharp
// Good: Single responsibility
public class ActivateUserRequest : IRequest
{
    public int UserId { get; }
    public ActivateUserRequest(int userId) => UserId = userId;
}

// Avoid: Multiple responsibilities
public class ManageUserRequest : IRequest
{
    public int UserId { get; set; }
    public string Action { get; set; } // "activate", "deactivate", "delete"
    public object Parameters { get; set; }
}
```

#### 3. Validation Rules
```csharp
public class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[a-zA-Z\\s]+$")
            .WithMessage("First name can only contain letters and spaces");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(50);
    }
}
```

### Handler Design

#### 1. Dependency Injection
```csharp
public class CreateUserHandler : IRequestHandler<CreateUserRequest, User>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<CreateUserHandler> _logger;

    public CreateUserHandler(
        IUserRepository userRepository,
        IEmailService emailService,
        ILogger<CreateUserHandler> logger)
    {
        _userRepository = userRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<User>> Handle(CreateUserRequest request, CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

#### 2. Error Handling
```csharp
public async Task<Result<User>> Handle(CreateUserRequest request, CancellationToken cancellationToken)
{
    try
    {
        // Check if user already exists
        var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser != null)
        {
            return Result.Fail("A user with this email already exists");
        }

        var user = new User(request.Email, request.FirstName, request.LastName);
        await _userRepository.AddAsync(user, cancellationToken);

        return Result.Ok(user);
    }
    catch (DbException ex)
    {
        _logger.LogError(ex, "Database error occurred while creating user");
        return Result.Fail("An error occurred while saving the user");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error occurred while creating user");
        return Result.Fail("An unexpected error occurred");
    }
}
```

### Notification Design

#### 1. Rich Notifications
```csharp
public class UserProfileUpdatedNotification : INotification
{
    public int UserId { get; }
    public string PreviousEmail { get; }
    public string NewEmail { get; }
    public Dictionary<string, object> Changes { get; }
    public DateTime UpdatedAt { get; }
    public string UpdatedBy { get; }

    public UserProfileUpdatedNotification(
        int userId,
        string previousEmail,
        string newEmail,
        Dictionary<string, object> changes,
        DateTime updatedAt,
        string updatedBy)
    {
        UserId = userId;
        PreviousEmail = previousEmail;
        NewEmail = newEmail;
        Changes = changes;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }
}
```

#### 2. Handler Prioritization
```csharp
// High priority - run sequentially first
public class CriticalAuditHandler : INotificationHandler<UserProfileUpdatedNotification>
{
    public bool SupportsConcurrentExecution => false;
    // Implementation
}

// Lower priority - can run concurrently
public class EmailNotificationHandler : INotificationHandler<UserProfileUpdatedNotification>
{
    public bool SupportsConcurrentExecution => true;
    // Implementation
}
```

---

## Error Handling

### FluentResults Integration

#### Success Results
```csharp
public async Task<Result<User>> Handle(GetUserByIdRequest request, CancellationToken cancellationToken)
{
    var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
    
    if (user == null)
    {
        return Result.Fail($"User with ID {request.UserId} not found");
    }

    return Result.Ok(user);
}
```

#### Multiple Errors
```csharp
public async Task<Result<User>> Handle(CreateUserRequest request, CancellationToken cancellationToken)
{
    var errors = new List<string>();

    // Check email uniqueness
    if (await _userRepository.EmailExistsAsync(request.Email))
    {
        errors.Add("Email is already in use");
    }

    // Check other business rules
    if (IsEmailBlacklisted(request.Email))
    {
        errors.Add("Email domain is not allowed");
    }

    if (errors.Any())
    {
        return Result.Fail(errors);
    }

    // Create user
    var user = new User(request.Email, request.FirstName, request.LastName);
    await _userRepository.AddAsync(user, cancellationToken);

    return Result.Ok(user);
}
```

#### Custom Error Types
```csharp
public class ValidationError : Error
{
    public string Field { get; }

    public ValidationError(string field, string message) : base(message)
    {
        Field = field;
    }
}

public class BusinessRuleError : Error
{
    public string RuleCode { get; }

    public BusinessRuleError(string ruleCode, string message) : base(message)
    {
        RuleCode = ruleCode;
    }
}

// Usage
return Result.Fail(new ValidationError("Email", "Invalid email format"));
return Result.Fail(new BusinessRuleError("USR001", "User limit exceeded"));
```

### Global Error Handling

#### ASP.NET Core Middleware
```csharp
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation error occurred");
            await HandleValidationExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred");
            await HandleGenericExceptionAsync(context, ex);
        }
    }

    private async Task HandleValidationExceptionAsync(HttpContext context, ValidationException ex)
    {
        context.Response.StatusCode = 400;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = "Validation failed",
            details = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private async Task HandleGenericExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var response = new { error = "An internal server error occurred" };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
```

---

## Performance Considerations

### Concurrent vs Sequential Execution

#### Notification Handler Performance
```csharp
// Fast, independent operations - use concurrent execution
public class MetricsUpdateHandler : INotificationHandler<UserCreatedNotification>
{
    public bool SupportsConcurrentExecution => true; // ? Can run in parallel

    public async Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        await _metricsService.IncrementAsync("users.created");
    }
}

// Database operations that might conflict - use sequential execution
public class UserSequenceUpdateHandler : INotificationHandler<UserCreatedNotification>
{
    public bool SupportsConcurrentExecution => false; // ? Must run sequentially

    public async Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        await _database.UpdateUserSequenceAsync();
    }
}
```

### Memory Management

#### Disposing Resources
```csharp
public class FileProcessingHandler : IRequestHandler<ProcessFileRequest, ProcessResult>
{
    public async Task<Result<ProcessResult>> Handle(ProcessFileRequest request, CancellationToken cancellationToken)
    {
        using var fileStream = new FileStream(request.FilePath, FileMode.Open);
        using var reader = new StreamReader(fileStream);
        
        var content = await reader.ReadToEndAsync();
        var result = ProcessContent(content);
        
        return Result.Ok(result);
    }
}
```

#### Async Best Practices
```csharp
public class AsyncBestPracticesHandler : IRequestHandler<SomeRequest, SomeResponse>
{
    public async Task<Result<SomeResponse>> Handle(SomeRequest request, CancellationToken cancellationToken)
    {
        // ? Good: Use ConfigureAwait(false) in libraries
        var data = await _repository.GetDataAsync(cancellationToken).ConfigureAwait(false);
        
        // ? Good: Parallel execution when possible
        var tasks = request.Items.Select(item => ProcessItemAsync(item, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        
        // ? Good: Use cancellation tokens
        cancellationToken.ThrowIfCancellationRequested();
        
        return Result.Ok(new SomeResponse(results));
    }
}
```

### Caching Strategies

#### Request-Level Caching
```csharp
public class CachedUserLookupRequest : IRequest<User>, ICacheable
{
    public int UserId { get; }

    public CachedUserLookupRequest(int userId)
    {
        UserId = userId;
    }

    public string GetCacheKey() => $"user:{UserId}";
    public TimeSpan CacheExpiration => TimeSpan.FromMinutes(15);
}
```

---

## Testing Strategies

### Unit Testing Handlers

#### Testing Request Handlers
```csharp
public class GetUserByIdHandlerTests
{
    private readonly Mock<IUserRepository> _mockRepository;
    private readonly Mock<ILogger<GetUserByIdHandler>> _mockLogger;
    private readonly GetUserByIdHandler _handler;

    public GetUserByIdHandlerTests()
    {
        _mockRepository = new Mock<IUserRepository>();
        _mockLogger = new Mock<ILogger<GetUserByIdHandler>>();
        _handler = new GetUserByIdHandler(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_UserExists_ReturnsUser()
    {
        // Arrange
        var userId = 1;
        var expectedUser = new User { Id = userId, Email = "test@example.com" };
        var request = new GetUserByIdRequest(userId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUser);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedUser, result.Value);
        _mockRepository.Verify(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = 999;
        var request = new GetUserByIdRequest(userId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains($"User with ID {userId} not found", result.Errors.Select(e => e.Message));
    }
}
```

#### Testing Feature Actions
```csharp
public class FeatureActionTests
{
    private readonly Mock<IRequestHandler<CreateUserRequest, User>> _mockHandler;
    private readonly Mock<IValidator<CreateUserRequest>> _mockValidator;
    private readonly FeatureAction<CreateUserRequest, User> _featureAction;

    public FeatureActionTests()
    {
        _mockHandler = new Mock<IRequestHandler<CreateUserRequest, User>>();
        _mockValidator = new Mock<IValidator<CreateUserRequest>>();
        _featureAction = new FeatureAction<CreateUserRequest, User>(
            _mockHandler.Object, 
            _mockValidator.Object);
    }

    [Fact]
    public async Task Execute_ValidRequest_CallsHandler()
    {
        // Arrange
        var request = new CreateUserRequest("test@example.com", "John", "Doe");
        var expectedUser = new User { Id = 1, Email = request.Email };

        _mockValidator
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _mockHandler
            .Setup(h => h.Handle(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(expectedUser));

        // Act
        var result = await _featureAction.Execute(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedUser, result.Value);
    }

    [Fact]
    public async Task Execute_InvalidRequest_ReturnsValidationErrors()
    {
        // Arrange
        var request = new CreateUserRequest("", "", "");
        var validationErrors = new List<ValidationFailure>
        {
            new ValidationFailure("Email", "Email is required"),
            new ValidationFailure("FirstName", "First name is required")
        };

        _mockValidator
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(validationErrors));

        // Act
        var result = await _featureAction.Execute(request, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.Errors.Count);
        _mockHandler.Verify(h => h.Handle(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

### Integration Testing

#### Testing with Dependency Injection
```csharp
public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateUser_ValidRequest_ReturnsCreatedUser()
    {
        // Arrange
        var request = new
        {
            Email = "integration@test.com",
            FirstName = "Integration",
            LastName = "Test"
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/users", content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var user = JsonSerializer.Deserialize<User>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(user);
        Assert.Equal(request.Email, user.Email);
        Assert.Equal(request.FirstName, user.FirstName);
        Assert.Equal(request.LastName, user.LastName);
    }
}
```

### Testing Notifications

#### Testing Notification Handlers
```csharp
public class SendWelcomeEmailHandlerTests
{
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<ILogger<SendWelcomeEmailHandler>> _mockLogger;
    private readonly SendWelcomeEmailHandler _handler;

    public SendWelcomeEmailHandlerTests()
    {
        _mockEmailService = new Mock<IEmailService>();
        _mockLogger = new Mock<ILogger<SendWelcomeEmailHandler>>();
        _handler = new SendWelcomeEmailHandler(_mockEmailService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidNotification_SendsEmail()
    {
        // Arrange
        var notification = new UserCreatedNotification(
            1, 
            "test@example.com", 
            "John", 
            "Doe", 
            DateTime.UtcNow);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _mockEmailService.Verify(
            e => e.SendWelcomeEmailAsync(
                notification.Email, 
                notification.FirstName, 
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void SupportsConcurrentExecution_ReturnsTrue()
    {
        // Assert
        Assert.True(_handler.SupportsConcurrentExecution);
    }
}
```

#### Testing Notification Publishers
```csharp
public class NotificationPublisherTests
{
    private readonly Mock<INotificationHandler<TestNotification>> _mockConcurrentHandler;
    private readonly Mock<INotificationHandler<TestNotification>> _mockSequentialHandler;
    private readonly NotificationPublisher<TestNotification> _publisher;

    public NotificationPublisherTests()
    {
        _mockConcurrentHandler = new Mock<INotificationHandler<TestNotification>>();
        _mockSequentialHandler = new Mock<INotificationHandler<TestNotification>>();
        
        _mockConcurrentHandler.Setup(h => h.SupportsConcurrentExecution).Returns(true);
        _mockSequentialHandler.Setup(h => h.SupportsConcurrentExecution).Returns(false);

        var handlers = new[]
        {
            _mockConcurrentHandler.Object,
            _mockSequentialHandler.Object
        };

        _publisher = new NotificationPublisher<TestNotification>(handlers);
    }

    [Fact]
    public async Task Publish_CallsAllHandlers()
    {
        // Arrange
        var notification = new TestNotification { Message = "Test" };

        // Act
        await _publisher.Publish(notification, CancellationToken.None);

        // Assert
        _mockConcurrentHandler.Verify(
            h => h.Handle(notification, It.IsAny<CancellationToken>()), 
            Times.Once);
        _mockSequentialHandler.Verify(
            h => h.Handle(notification, It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}
```

---

## Migration Guide

### From Version 1.1.0 to 1.2.0

#### Breaking Changes
None. Version 1.2.0 is fully backward compatible.

#### New Features
- Enhanced notification publisher registration
- Improved concurrent execution handling
- Better performance for notification publishing

#### Migration Steps
1. Update package reference:
```xml
<PackageReference Include="ClearFeatureActions" Version="1.2.0" />
```

2. No code changes required - existing implementations continue to work.

### From MediaTR to ClearFeatureActions

#### Key Differences
| Feature | MediaTR | ClearFeatureActions |
|---------|---------|-------------------|
| Validation | Separate pipeline behavior | Built-in with FluentValidation |
| Results | Throws exceptions | Uses FluentResults |
| Notifications | Basic publish | Concurrent/Sequential execution |
| Registration | Manual configuration | Automatic assembly scanning |

#### Migration Example

**Before (MediaTR):**
```csharp
public class GetUserQuery : IRequest<User>
{
    public int Id { get; set; }
}

public class GetUserHandler : IRequestHandler<GetUserQuery, User>
{
    public async Task<User> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        // Implementation that throws exceptions
        var user = await _repository.GetAsync(request.Id);
        if (user == null)
            throw new NotFoundException($"User {request.Id} not found");
        return user;
    }
}
```

**After (ClearFeatureActions):**
```csharp
public class GetUserRequest : IRequest<User>
{
    public int Id { get; }
    public GetUserRequest(int id) => Id = id;
}

public class GetUserHandler : IRequestHandler<GetUserRequest, User>
{
    public async Task<Result<User>> Handle(GetUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _repository.GetAsync(request.Id);
        if (user == null)
            return Result.Fail($"User {request.Id} not found");
        return Result.Ok(user);
    }
}
```

---

## API Reference

### Core Interfaces

#### IRequest<TResponse>
Defines a request that expects a specific response type.
```csharp
public interface IRequest<TResponse> { }
```

#### IRequest
Shorthand for `IRequest<bool>`.
```csharp
public interface IRequest : IRequest<bool> { }
```

#### IRequestHandler<TRequest, TResponse>
Handles business logic for requests.
```csharp
public interface IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<Result<TResponse>> Handle(TRequest command, CancellationToken cancellationToken);
}
```

#### IFeatureAction<TRequest, TResponse>
Orchestrates validation and handling.
```csharp
public interface IFeatureAction<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<Result<TResponse>> Execute(TRequest command, CancellationToken cancellationToken);
}
```

#### INotification
Marker interface for notifications.
```csharp
public interface INotification { }
```

#### INotificationHandler<T>
Handles notifications.
```csharp
public interface INotificationHandler<in T> where T : INotification
{
    bool SupportsConcurrentExecution { get; }
    Task Handle(T notification, CancellationToken cancellationToken);
}
```

#### INotificationPublisher<T>
Publishes notifications to handlers.
```csharp
public interface INotificationPublisher<in T> where T : INotification
{
    Task Publish(T notification, CancellationToken cancellationToken);
}
```

### Extension Methods

#### AddFeatureActions
Registers all feature actions, handlers, and validators from an assembly.
```csharp
public static IServiceCollection AddFeatureActions(
    this IServiceCollection services, 
    Assembly assembly = null)
```

#### AddNotificationPublishers
Registers all notification publishers and handlers from an assembly.
```csharp
public static IServiceCollection AddNotificationPublishers(
    this IServiceCollection services, 
    Assembly assembly = null)
```

---

## Troubleshooting

### Common Issues

#### 1. Handler Not Found
**Error:** `Unable to resolve service for type 'IRequestHandler<MyRequest, MyResponse>'`

**Solution:**
- Ensure the handler implements the correct interface
- Verify the assembly is registered with `AddFeatureActions`
- Check that the handler class is public and not abstract

```csharp
// ? Correct implementation
public class MyRequestHandler : IRequestHandler<MyRequest, MyResponse>
{
    public async Task<Result<MyResponse>> Handle(MyRequest request, CancellationToken cancellationToken)
    {
        // Implementation
    }
}

// ? Incorrect - missing interface implementation
public class MyRequestHandler
{
    public async Task<Result<MyResponse>> Handle(MyRequest request, CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

#### 2. Validator Not Found
**Error:** Feature action executes without validation even though validator exists.

**Solution:**
- Ensure validator inherits from `AbstractValidator<TRequest>`
- Verify the validator is in the same assembly being scanned
- Check validator class is public

```csharp
// ? Correct validator
public class MyRequestValidator : AbstractValidator<MyRequest>
{
    public MyRequestValidator()
    {
        RuleFor(x => x.Property).NotEmpty();
    }
}

// ? Incorrect - doesn't inherit from AbstractValidator
public class MyRequestValidator : IValidator<MyRequest>
{
    // Manual implementation
}
```

#### 3. Notification Handlers Not Executing
**Error:** Notification published but handlers don't execute.

**Solution:**
- Verify handlers implement `INotificationHandler<TNotification>`
- Ensure `AddNotificationPublishers` is called during startup
- Check that the notification type matches exactly

```csharp
// ? Correct notification handler
public class MyNotificationHandler : INotificationHandler<MyNotification>
{
    public bool SupportsConcurrentExecution => true;
    
    public async Task Handle(MyNotification notification, CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

#### 4. Concurrent Execution Issues
**Problem:** Handlers that should run sequentially are running concurrently.

**Solution:**
- Set `SupportsConcurrentExecution = false` for handlers that must run sequentially
- Understand that concurrent handlers run first, then sequential handlers

```csharp
public class AuditHandler : INotificationHandler<MyNotification>
{
    // This ensures the handler runs sequentially after concurrent handlers
    public bool SupportsConcurrentExecution => false;
    
    public async Task Handle(MyNotification notification, CancellationToken cancellationToken)
    {
        // Critical operations that must not run concurrently
    }
}
```

### Performance Issues

#### 1. Too Many Sequential Handlers
**Problem:** Slow notification publishing due to many sequential handlers.

**Solution:**
- Only use sequential execution for handlers that truly need it
- Consider combining multiple sequential operations into a single handler
- Use concurrent execution for independent operations

#### 2. Memory Leaks in Long-Running Handlers
**Problem:** Memory usage increases over time.

**Solution:**
- Properly dispose of resources using `using` statements
- Avoid keeping references to large objects
- Use `ConfigureAwait(false)` to avoid capturing synchronization context

### Debugging Tips

#### 1. Enable Detailed Logging
```csharp
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});
```

#### 2. Inspect Dependency Injection Container
```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddFeatureActions(Assembly.GetExecutingAssembly());
    
    // Debug: Print all registered services
    var serviceProvider = services.BuildServiceProvider();
    foreach (var service in services)
    {
        Console.WriteLine($"{service.ServiceType.Name} -> {service.ImplementationType?.Name}");
    }
}
```

#### 3. Validate Registration at Startup
```csharp
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    // Validate critical services are registered
    using var scope = app.ApplicationServices.CreateScope();
    
    try
    {
        var featureAction = scope.ServiceProvider.GetRequiredService<IFeatureAction<MyRequest, MyResponse>>();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher<MyNotification>>();
        
        Console.WriteLine("All required services are registered successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Service registration validation failed: {ex.Message}");
        throw;
    }
}
```

---

## Conclusion

ClearFeatureActions provides a robust, scalable foundation for implementing CQRS patterns in .NET applications. By following the patterns and practices outlined in this guide, you can build maintainable, testable, and performant applications.

### Key Takeaways
1. **Use Feature Actions** for command/query operations with built-in validation
2. **Leverage Notifications** for decoupled event handling and side effects
3. **Follow the single responsibility principle** for requests and handlers
4. **Implement proper error handling** using FluentResults
5. **Use concurrent execution** for independent notification handlers
6. **Test thoroughly** with unit and integration tests
7. **Monitor performance** and optimize based on usage patterns

### Getting Help
- GitHub Repository: [https://github.com/ehgodson/ClearFeatureActions](https://github.com/ehgodson/ClearFeatureActions)
- Issues: [https://github.com/ehgodson/ClearFeatureActions/issues](https://github.com/ehgodson/ClearFeatureActions/issues)
- License: MIT License

---

*This guide covers ClearFeatureActions version 1.2.0. For the latest updates and changes, please refer to the CHANGELOG.md file.*