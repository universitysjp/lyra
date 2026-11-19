using System.Security.Claims;
using Lyra.Contracts.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddLyraJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();

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

public partial class Program { }
