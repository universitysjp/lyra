namespace Lyra.AI;

using Lyra.AI.Configuration;
using Lyra.AI.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddLyraAiProviders(
        this IServiceCollection services,
        Action<AiProviderOptions>? configure = null)
    {
        var options = services.AddOptions<AiProviderOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAiChatClientProvider, OpenAiChatClientProvider>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAiChatClientProvider, GeminiChatClientProvider>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAiChatClientProvider, OpenCodeChatClientProvider>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAiChatClientProvider, ClaudeChatClientProvider>());
        services.TryAddSingleton<IAiChatClientFactory, AiChatClientFactory>();

        return services;
    }
}
