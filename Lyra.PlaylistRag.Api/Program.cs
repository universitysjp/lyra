using System.Security.Claims;
using DotNetEnv;
using Lyra.Contracts.Authentication;
using Lyra.PlaylistRag.Api.Data;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

// Load local secrets (connection strings, broker credentials) from the repository-root .env file.
Env.Load(options: new LoadOptions().TraversePath());

var builder = WebApplication.CreateBuilder(args);

var vectorConnectionString =
    builder.Configuration.GetConnectionString("VectorDb")
    ?? throw new InvalidOperationException(
        "The vector database connection string is missing. Set 'ConnectionStrings__VectorDb' in the repository-root .env file.");

builder.Services.AddDbContext<VectorDbContext>(options =>
    options.UseNpgsql(vectorConnectionString, npgsql => npgsql.UseVector()));

builder.Services.AddOpenApi();
builder.Services.AddLyraJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<VectorDbContext>();
    await database.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/playlists/ping", (ClaimsPrincipal user) =>
{
    var userId = user.GetUserId();
    return TypedResults.Ok(new { message = "pong", userId });
})
.RequireAuthorization()
.WithName("PlaylistsPing")
.WithSummary("Echo authenticated user ID to verify token propagation");

app.Run();

public partial class Program
{
}
