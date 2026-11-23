using DotNetEnv;
using Lyra.Contracts.Authentication;
using Lyra.Identity.Api.Configuration;
using Lyra.Identity.Api.Data;
using Lyra.Identity.Api.Endpoints;
using Lyra.Identity.Api.Grpc;
using Lyra.Identity.Api.Middleware;
using Lyra.Identity.Api.Models;
using Lyra.Identity.Api.Services.AiConnections;
using Lyra.Identity.Api.Services.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Load local secrets (connection strings, credentials) from the repository-root .env file.
Env.Load(options: new LoadOptions().TraversePath());

var builder = WebApplication.CreateBuilder(args);

// 1. Application configuration
builder.Services.Configure<AppStringOptions>(
    builder.Configuration.GetSection(AppStringOptions.SectionName));
builder.Services.Configure<IdentityOptions>(
    builder.Configuration.GetSection("Identity"));
builder.Services.Configure<AiConnectionOptions>(
    builder.Configuration.GetSection(AiConnectionOptions.SectionName));
builder.Services.Configure<InternalServicesOptions>(
    builder.Configuration.GetSection(InternalServicesOptions.SectionName));

var connectionString =
    builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException(
        builder.Configuration[$"{AppStringOptions.SectionName}:Errors:IdentityDbMissing"]
        ?? "IdentityDb connection string is missing.");

if (!builder.Environment.IsDevelopment()
    && !builder.Environment.IsEnvironment("Testing"))
{
    var grpcUrl = builder.Configuration["Kestrel:Endpoints:Grpc:Url"];
    if (!Uri.TryCreate(grpcUrl, UriKind.Absolute, out var grpcUri)
        || grpcUri.Scheme != Uri.UriSchemeHttps)
    {
        throw new InvalidOperationException(
            "The internal gRPC endpoint must use HTTPS outside Development. " +
            "Configure 'Kestrel:Endpoints:Grpc:Url' with an https:// URL.");
    }
}

// 2. Infrastructure and database
builder.Services.AddDbContext<IdentityDbContext>(
    options => options.UseNpgsql(connectionString));
builder.Services
    .AddDataProtection()
    .SetApplicationName("Lyra.Identity.Api")
    .PersistKeysToDbContext<IdentityDbContext>();

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
builder.Services.AddScoped<IApiKeyProtector, ApiKeyProtector>();
builder.Services.AddScoped<IUserAiConnectionService, UserAiConnectionService>();
builder.Services.AddSingleton<IInternalServiceAuthenticator, InternalServiceAuthenticator>();
builder.Services.AddScoped<InternalServiceAuthenticationInterceptor>();
builder.Services.AddGrpc(options =>
    options.Interceptors.Add<InternalServiceAuthenticationInterceptor>());

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
app.MapAiConnectionEndpoints();
app.MapGrpcService<UserAiConnectionGrpcService>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();

public partial class Program { }
