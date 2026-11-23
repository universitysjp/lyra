namespace Lyra.Identity.Api.Models;

using Lyra.Contracts.AI;

public class UserAiConnection
{
    public Guid UserId { get; set; }

    public AiProvider Provider { get; set; }

    public string Model { get; set; } = string.Empty;

    public string ProtectedApiKey { get; set; } = string.Empty;

    public OpenCodePlan? OpenCodePlan { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
