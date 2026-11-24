namespace Lyra.AI.Tests;

using Lyra.Contracts.AI;
using Xunit;

public class AiConnectionSecretTests
{
    [Fact]
    public void ToString_RedactsApiKey()
    {
        var secret = new AiConnectionSecret(
            AiProvider.OpenAI,
            "gpt-test",
            "sk-never-log-this",
            null);

        var text = secret.ToString();

        Assert.DoesNotContain("sk-never-log-this", text);
        Assert.Contains("[REDACTED]", text);
        Assert.Contains("gpt-test", text);
    }
}
