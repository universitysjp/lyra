namespace Lyra.AI.Providers;

using System.ClientModel;
using System.ClientModel.Primitives;
using Lyra.Contracts.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenCodeSessionHeaderPolicy = Lyra.AI.Providers.StaticHeaderPipelinePolicy;

internal sealed class OpenCodeChatClientProvider : IAiChatClientProvider
{
    private const string SessionHeaderName = "x-opencode-session";

    private static readonly Uri PayAsYouGoEndpoint =
        new("https://opencode.ai/inference/openai/v1", UriKind.Absolute);

    private static readonly Uri GoEndpoint =
        new("https://opencode.ai/inference/go/openai/v1", UriKind.Absolute);

    private readonly HttpMessageHandler? _httpMessageHandler;

    public OpenCodeChatClientProvider()
    {
    }

    internal OpenCodeChatClientProvider(HttpMessageHandler httpMessageHandler)
    {
        _httpMessageHandler = httpMessageHandler;
    }

    public AiProvider Provider => AiProvider.OpenCode;

    public IChatClient Create(AiConnectionSecret connection, string? sessionId)
    {
        ValidatePlan(connection);

        var endpoint = connection.OpenCodePlan switch
        {
            OpenCodePlan.PayAsYouGo => PayAsYouGoEndpoint,
            OpenCodePlan.Go => GoEndpoint,
            _ => throw new ArgumentOutOfRangeException(
                nameof(connection),
                connection.OpenCodePlan,
                "The requested OpenCode plan is not supported.")
        };

        var options = new OpenAIClientOptions
        {
            Endpoint = endpoint
        };

        options.AddPolicy(
            new OpenCodeSessionHeaderPolicy(SessionHeaderName, sessionId!),
            PipelinePosition.PerCall);

        if (_httpMessageHandler is not null)
        {
            options.Transport = new HttpClientPipelineTransport(
                new HttpClient(_httpMessageHandler, disposeHandler: false));
        }

        var client = new OpenAIClient(new ApiKeyCredential(connection.ApiKey), options);
        return client.GetChatClient(connection.Model).AsIChatClient();
    }

    private static void ValidatePlan(AiConnectionSecret connection)
    {
        if (connection.OpenCodePlan is null || !Enum.IsDefined(connection.OpenCodePlan.Value))
        {
            throw new ArgumentException(
                "An OpenCode plan is required for the OpenCode provider.",
                nameof(connection));
        }
    }
}
