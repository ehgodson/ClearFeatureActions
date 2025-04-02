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

## Installation
To install ClearFeatureActions, add the following package references to your project file:

## Usage
1. Define a request by implementing the `IRequest<TResponse>` interface.
2. Implement a handler for the request by implementing the `IRequestHandler<TRequest, TResponse>` interface.
3. Optionally, implement a validator for the request by inheriting from `AbstractValidator<TRequest>`.
4. Register the feature actions in your service collection using the `AddFeatureActions` extension method.

### Example

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

## License
This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.