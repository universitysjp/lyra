---
name: clean-webapi-architecture
description: >
  Guides structuring and refactoring of ASP.NET Core Web API services to
  separate concerns cleanly: keeping minimal API endpoints thin, encapsulating
  business logic in dedicated service classes, externalizing all literal strings
  into a single configuration file (appsettings.json), centralizing error
  handling in custom middleware, organizing DTOs into Requests and Responses,
  and minimalizing Program.cs into clear numbered sections.
  USE FOR: "clean up endpoints", "move business logic to services", "centralize
  error handling", "minimalize Program.cs", "store literal strings in config",
  separating DTOs into Requests and Responses, creating custom domain exceptions
  and ExceptionHandlingMiddleware.
  DO NOT USE FOR: EF Core query optimization (use optimizing-ef-core-queries),
  frontend/Blazor work, gRPC services.
license: MIT
---

# Clean Web API Architecture

Structure ASP.NET Core Web API projects with clean separation of concerns: thin endpoints, a dedicated service layer, separated DTOs, domain exceptions, centralized exception-handling middleware, minimal `Program.cs`, and externalized literal strings stored in a single configuration file.

## When to Use

Use this skill when:
- Refactoring fat endpoints into thin endpoints that delegate all logic to a service layer;
- Encapsulating domain and business logic into dedicated service interfaces and classes under `Services/<Feature>/`;
- Externalizing all literal strings (error messages, titles, log messages, endpoints) into a single configuration file (`appsettings.json`) via strongly typed options to avoid hardcoding strings in C# code;
- Structuring request and response models into dedicated `DTOs/Requests/` and `DTOs/Responses/` directories;
- Defining custom domain exceptions under `Exceptions/`;
- Centralizing error handling and status code mapping using custom `Middleware/ExceptionHandlingMiddleware.cs`;
- Minimalizing `Program.cs` into clear, ordered sections.

## When Not to Use

Do not use this skill for:
- EF Core data modeling or database query optimization (use `optimizing-ef-core-queries`);
- Frontend, Razor, or Blazor UI changes;
- gRPC services or real-time SignalR hubs.

## Target Directory Structure

```
YourProject/
│
├── Endpoints/
│   └── <Feature>Endpoints.cs
│
├── Services/
│   └── <Feature>/
│       ├── I<Feature>Service.cs
│       └── <Feature>Service.cs
│
├── DTOs/
│   ├── Requests/
│   │   └── <Action>Request.cs
│   └── Responses/
│       └── <Action>Response.cs
│
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs
│
├── Exceptions/
│   ├── <Domain>ValidationException.cs
│   ├── InvalidCredentialsException.cs
│   ├── InvalidTokenException.cs
│   ├── UserLockedOutException.cs
│   └── NotFoundException.cs
│
├── Models/
│   └── <DomainEntity>.cs
│
├── Configuration/
│   └── AppStringOptions.cs
│
├── appsettings.json
└── Program.cs
```

## Workflow

### Step 1: Externalize Literal Strings to a Single Config File (`appsettings.json`)
Never hardcode error messages, titles, log templates, or location paths in C# code. Define them in `appsettings.json` under an `AppStrings` section:

```json
{
  "AppStrings": {
    "Errors": {
      "InvalidCredentials": "Invalid email or password.",
      "EmailPasswordRequired": "Email and password are required.",
      "UserLockedOut": "The user account is temporarily locked out due to multiple failed login attempts.",
      "UserNotFound": "User not found."
    },
    "Titles": {
      "Unauthorized": "Unauthorized",
      "AccountLocked": "Account Locked",
      "NotFound": "Not Found",
      "InternalServerError": "An error occurred while processing your request."
    },
    "Logs": {
      "ValidationFailure": "Validation failure on {Path}",
      "AuthenticationFailed": "Authentication failed on {Path}: {Message}"
    },
    "Endpoints": {
      "MeLocation": "/api/auth/me"
    }
  }
}
```

Create a strongly typed class `Configuration/AppStringOptions.cs` and bind it:
```csharp
builder.Services.Configure<AppStringOptions>(
    builder.Configuration.GetSection(AppStringOptions.SectionName));
```
Inject `IOptions<AppStringOptions>` into services and middleware instead of using literal strings.

### Step 2: Define DTOs (Requests & Responses)
- Place incoming payloads in `DTOs/Requests/` as `sealed record` types with `init` properties.
- Place outgoing payloads in `DTOs/Responses/` as positional `sealed record` types.
- Never expose domain/database models directly in endpoint parameters or responses.

### Step 3: Define Domain Exceptions
- Create custom exceptions inheriting from `Exception` in `Exceptions/` for predictable domain failure states (`IdentityValidationException`, `InvalidCredentialsException`, `UserLockedOutException`, `InvalidTokenException`, `NotFoundException`).
- **Do not hardcode default error strings or parameter defaults in exception constructors** (e.g., use `public class InvalidTokenException(string message) : Exception(message)`, never `message = "..."`). All error strings must be supplied by the caller from the configuration file.
- Carry structured details (e.g. error lists, error codes) in properties on the exception.

### Step 4: Implement Centralized Exception Handling Middleware
- Create `Middleware/ExceptionHandlingMiddleware.cs` using the standard ASP.NET Core middleware pattern (`RequestDelegate next`, `ILogger logger`, `IOptions<AppStringOptions> stringOptions`).
- Wrap `await next(context)` in a `try/catch`.
- Map domain exceptions to appropriate HTTP status codes (400 Bad Request, 401 Unauthorized, 404 Not Found, 423 Locked, 500 Internal Server Error) and return RFC 7807 `ProblemDetails` or error payloads.

### Step 5: Extract Business Logic to Services
- Define interfaces under `Services/<Feature>/` (e.g., `IAuthService`).
- Implement the interface in a sealed class using primary constructors.
- Keep all orchestration, verification, hashing, token minting, and persistence calls inside the service.
- Throw domain exceptions when business rules or validations fail instead of returning raw HTTP result types from the service.

### Step 6: Keep Endpoints Ultra-Thin
- Handlers in `Endpoints/<Feature>Endpoints.cs` only accept the request DTO, inject the service interface, invoke the service method, and return the HTTP response (`TypedResults.Ok(...)`, `TypedResults.Created(...)`, `TypedResults.NoContent()`).
- No validation rules, database queries, or identity checks should live directly inside endpoint delegates.

### Step 7: Minimalize Program.cs
Structure `Program.cs` cleanly into numbered, dedicated stages without cluttering:

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Application configuration
builder.Services.Configure<AppStringOptions>(
    builder.Configuration.GetSection(AppStringOptions.SectionName));
builder.Services.Configure<IdentityOptions>(
    builder.Configuration.GetSection("Identity"));

var connectionString =
    builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException(
        builder.Configuration[$"{AppStringOptions.SectionName}:Errors:IdentityDbMissing"]
        ?? "IdentityDb connection string is missing.");

// 2. Infrastructure and database
builder.Services.AddDbContext<IdentityDbContext>(
    options => options.UseNpgsql(connectionString));

// 3. Identity configuration
builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<IdentityDbContext>()
    .AddDefaultTokenProviders();

// 4. Application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

// 5. Security
builder.Services.AddSingleton<IRsaKeyProvider, RsaKeyProvider>();
builder.Services.AddLyraJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// 6. API configuration
builder.Services.AddOpenApi();

var app = builder.Build();

// 7. Middleware pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// 8. Endpoint registration
app.MapJwksEndpoints();
app.MapAuthEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();

public partial class Program { }
```

## Validation Checklist

- [ ] `Program.cs` is minimal and structured in ordered logical sections.
- [ ] No literal strings (error messages, titles, log templates, route locations) are hardcoded in code; all are stored in `appsettings.json` and read via `AppStringOptions`.
- [ ] Endpoints contain zero business logic and only delegate to services.
- [ ] DTOs are separated into `DTOs/Requests/` and `DTOs/Responses/`.
- [ ] Domain exceptions are defined under `Exceptions/`.
- [ ] Centralized `ExceptionHandlingMiddleware` handles exception-to-HTTP mapping.
- [ ] Domain models/entities are in `Models/`.
- [ ] No hardcoded connection strings in code; never use an `IDesignTimeDbContextFactory` with hardcoded credentials (EF Core tools automatically discover DbContexts from `Program.cs`).
- [ ] All unit and integration tests compile and pass.

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Hardcoding connection strings in `IDesignTimeDbContextFactory` | Delete the factory class. EF Core tools automatically discover `AddDbContext` from `Program.cs` and read `appsettings.json`. |
| Default error strings in exception constructors | Require the `message` parameter in constructors and pass it from `IOptions<AppStringOptions>`. |
| Business logic inside minimal API endpoint delegates | Move all business logic to service classes under `Services/<Feature>/`. |
| Mixing request and response DTOs in a single file or folder | Separate into `DTOs/Requests/` and `DTOs/Responses/` as immutable `sealed record`s. |
