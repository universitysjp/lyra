namespace Lyra.PlaylistRag.Api.Tests.TestDoubles;

using Lyra.AI.Identity;
using Lyra.Contracts.AI;

internal sealed class FakeUserAiConnectionClient(
    Func<Guid, CancellationToken, Task<AiConnectionSecret>>? handler = null)
    : IUserAiConnectionClient
{
    public int Calls { get; private set; }

    public Guid? LastUserId { get; private set; }

    public Task<AiConnectionSecret> GetRequiredAsync(Guid userId, CancellationToken ct)
    {
        Calls++;
        LastUserId = userId;

        return handler is not null
            ? handler(userId, ct)
            : Task.FromResult(Connection());
    }

    public static AiConnectionSecret Connection() =>
        new(AiProvider.OpenAI, "gpt-test", "sk-test", null);
}
