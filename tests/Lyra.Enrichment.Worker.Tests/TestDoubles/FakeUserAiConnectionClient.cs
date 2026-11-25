namespace Lyra.Enrichment.Worker.Tests.TestDoubles;

using Lyra.Contracts.AI;
using Lyra.AI.Identity;

internal sealed class FakeUserAiConnectionClient : IUserAiConnectionClient
{
    private readonly Func<Guid, CancellationToken, Task<AiConnectionSecret>> _handler;

    public FakeUserAiConnectionClient(
        Func<Guid, CancellationToken, Task<AiConnectionSecret>> handler)
    {
        _handler = handler;
    }

    public int Calls { get; private set; }

    public Task<AiConnectionSecret> GetRequiredAsync(Guid userId, CancellationToken ct)
    {
        Calls++;
        return _handler(userId, ct);
    }
}
