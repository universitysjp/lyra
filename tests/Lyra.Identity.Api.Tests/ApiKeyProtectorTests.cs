namespace Lyra.Identity.Api.Tests;

using System.Security.Cryptography;
using Lyra.Identity.Api.Services.AiConnections;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

public class ApiKeyProtectorTests
{
    [Fact]
    public void Protect_ProducesCiphertextAndRoundTrips()
    {
        var protector = new ApiKeyProtector(new EphemeralDataProtectionProvider());
        const string plaintext = "sk-test-secret-value";

        var protectedValue = protector.Protect(plaintext);

        Assert.NotEqual(plaintext, protectedValue);
        Assert.DoesNotContain(plaintext, protectedValue);
        Assert.Equal(plaintext, protector.Unprotect(protectedValue));
    }

    [Fact]
    public void Unprotect_RejectsPayloadFromDifferentKeyRing()
    {
        var writer = new ApiKeyProtector(new EphemeralDataProtectionProvider());
        var reader = new ApiKeyProtector(new EphemeralDataProtectionProvider());

        var protectedValue = writer.Protect("sk-test-secret-value");

        Assert.Throws<CryptographicException>(() => reader.Unprotect(protectedValue));
    }
}
