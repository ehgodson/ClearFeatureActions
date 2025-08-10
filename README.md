# ClearFeatureActions

## Description
ClearFeatureActions is a robust framework designed to encapsulate a request and its process flow within a feature. Each action serves as a subset of a specific feature, consisting of three main components: a request, a handler, and a validator. The feature action coordinates these components to ensure seamless functionality.

- The **handler** is responsible for executing the requests.
- The **validator** ensures that all validation checks are completed before any execution is attempted.

This approach guarantees that the workflow is robust and efficient. The project relies on **FluentValidation** for validation logic and **FluentResults** for managing execution results.

## Features
- Encapsulation of request and process flow
- Robust validation using FluentValidation
- Efficient result management using FluentResults
- **Notification system** with support for multiple handlers
- **Concurrent and sequential execution** modes for notification handlers
- **Publisher-subscriber pattern** implementation

## Installation
To install ClearFeatureActions, add the following package references to your project file:

```xml
<PackageReference Include="ClearFeatureActions" Version="1.2.0" />
```

## Usage

### Feature Actions
1. Define a request by implementing the `IRequest<TResponse>` interface.
2. Implement a handler for the request by implementing the `IRequestHandler<TRequest, TResponse>` interface.
3. Optionally, implement a validator for the request by inheriting from `AbstractValidator<TRequest>`.
4. Register the feature actions in your service collection using the `AddFeatureActions` extension method.

### Notifications
1. Define a notification by implementing the `INotification` interface.
2. Implement one or more handlers for the notification by implementing the `INotificationHandler<TNotification>` interface.
3. Register the notification publishers in your service collection using the `AddNotificationPublishers` extension method.
4. Inject and use `INotificationPublisher<TNotification>` to publish notifications.

### Examples

#### Feature Action Example

Here's an example of how to use the feature action framework with a sample request, handler, and execution in a dependency-injected service.  

---

### **Step 1: Define the Request**  
A request to check if a user exists by their ID, returning a `bool`.  

```csharp
public class CheckUserExistsRequest : IRequest<bool>
{
    public int UserId { get; }

    public CheckUserExistsRequest(int userId)
    {
        UserId = userId;
    }
}
```

---

### **Step 2: Implement the Request Handler**  
Handles the request using a user repository.  

```csharp
public class CheckUserExistsHandler : IRequestHandler<CheckUserExistsRequest, bool>
{
    private readonly IUserRepository _userRepository;

    public CheckUserExistsHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<bool>> Handle(CheckUserExistsRequest request, CancellationToken cancellationToken)
    {
        var exists = await _userRepository.ExistsAsync(request.UserId);
        return Result.Ok(exists);
    }
}
```

---

### **Step 3: Optional Validator**  
Ensures the `UserId` is valid.  

```csharp
public class CheckUserExistsValidator : AbstractValidator<CheckUserExistsRequest>
{
    public CheckUserExistsValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0).WithMessage("User ID must be greater than zero.");
    }
}
```

---

### **Step 4: Register Dependencies Using `AddFeatureActions`**  
Automatically register all feature actions, handlers, and validators in `Program.cs` or `Startup.cs`:  

```csharp
var assembly = Assembly.GetExecutingAssembly(); // Get the current assembly
services.AddFeatureActions(assembly); // Auto-registers everything in DI
```

---

### **Step 5: Use the Feature Action in a Controller**  
Inject `IFeatureAction<CheckUserExistsRequest, bool>` into a controller and expose an API endpoint.  

```csharp
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IFeatureAction<CheckUserExistsRequest, bool> _checkUserExistsAction;

    public UsersController(IFeatureAction<CheckUserExistsRequest, bool> checkUserExistsAction)
    {
        _checkUserExistsAction = checkUserExistsAction;
    }

    [HttpGet("{userId}/exists")]
    public async Task<IActionResult> CheckUserExists(int userId, CancellationToken cancellationToken)
    {
        var result = await _checkUserExistsAction.Execute(new CheckUserExistsRequest(userId), cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(result.Errors); // Return validation errors if any

        return Ok(new { Exists = result.Value });
    }
}
```

---

#### Notification Example

Here's an example of how to use the notification system to handle events across multiple handlers.

### **Step 1: Define the Notification**  
A notification for when a user is created.  

```csharp
public class UserCreatedNotification : INotification
{
    public int UserId { get; }
    public string Email { get; }
    public DateTime CreatedAt { get; }

    public UserCreatedNotification(int userId, string email, DateTime createdAt)
    {
        UserId = userId;
        Email = email;
        CreatedAt = createdAt;
    }
}
```

---

### **Step 2: Implement Notification Handlers**  
Create multiple handlers that respond to the notification.  

```csharp
// Handler for sending welcome email (supports concurrent execution)
public class SendWelcomeEmailHandler : INotificationHandler<UserCreatedNotification>
{
    private readonly IEmailService _emailService;

    public bool SupportsConcurrentExecution => true; // Can run concurrently

    public SendWelcomeEmailHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        await _emailService.SendWelcomeEmailAsync(notification.Email, cancellationToken);
    }
}

// Handler for logging user creation (requires sequential execution)
public class LogUserCreationHandler : INotificationHandler<UserCreatedNotification>
{
    private readonly ILogger _logger;

    public bool SupportsConcurrentExecution => false; // Must run sequentially

    public LogUserCreationHandler(ILogger logger)
    {
        _logger = logger;
    }

    public async Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"User {notification.UserId} created at {notification.CreatedAt}");
        await Task.CompletedTask;
    }
}
```

---

### **Step 3: Register Notification Publishers**  
Register the notification system in your service collection:  

```csharp
var assembly = Assembly.GetExecutingAssembly();
services.AddNotificationPublishers(assembly); // Auto-registers notification publishers and handlers
```

---

### **Step 4: Publish Notifications**  
Use the notification publisher in your service to broadcast events:  

```csharp
public class UserService
{
    private readonly INotificationPublisher<UserCreatedNotification> _notificationPublisher;
    private readonly IUserRepository _userRepository;

    public UserService(
        INotificationPublisher<UserCreatedNotification> notificationPublisher,
        IUserRepository userRepository)
    {
        _notificationPublisher = notificationPublisher;
        _userRepository = userRepository;
    }

    public async Task<User> CreateUserAsync(string email, CancellationToken cancellationToken)
    {
        var user = await _userRepository.CreateAsync(email, cancellationToken);
        
        // Publish notification to all registered handlers
        var notification = new UserCreatedNotification(user.Id, user.Email, user.CreatedAt);
        await _notificationPublisher.Publish(notification, cancellationToken);
        
        return user;
    }
}
```

---

## Key Benefits

### Feature Actions
- **Separation of Concerns**: Clear separation between request definition, handling logic, and validation
- **Dependency Injection**: Automatic registration and resolution of handlers and validators
- **Result Management**: Built-in error handling with FluentResults
- **Validation**: Integrated FluentValidation support

### Notifications
- **Decoupled Architecture**: Publishers don't need to know about specific handlers
- **Multiple Handlers**: Support for multiple handlers per notification type
- **Execution Control**: Choose between concurrent and sequential execution per handler
- **Scalability**: Easy to add new handlers without modifying existing code

## License
This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.