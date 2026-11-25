namespace Lyra.AI.Tests.TestDoubles;

using System.Net;
using System.Text;

internal sealed class RecordingEmbeddingHttpMessageHandler : HttpMessageHandler
{
    private const string EmbeddingResponse = """
        {
          "object": "list",
          "data": [
            {
              "object": "embedding",
              "index": 0,
              "embedding": [0.1, 0.2, 0.3]
            }
          ],
          "model": "text-embedding-3-small",
          "usage": { "prompt_tokens": 1, "total_tokens": 1 }
        }
        """;

    public HttpRequestMessage? Request { get; private set; }

    public string? RequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Request = request;
        RequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                EmbeddingResponse,
                Encoding.UTF8,
                "application/json")
        };
    }
}
