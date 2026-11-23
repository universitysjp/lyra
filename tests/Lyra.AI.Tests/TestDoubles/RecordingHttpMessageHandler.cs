namespace Lyra.AI.Tests.TestDoubles;

using System.Net;
using System.Text;

internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private const string ChatCompletionResponse = """
        {
          "id": "chatcmpl-test",
          "object": "chat.completion",
          "created": 1710000000,
          "model": "test-model",
          "choices": [
            {
              "index": 0,
              "message": { "role": "assistant", "content": "ok" },
              "finish_reason": "stop"
            }
          ],
          "usage": {
            "prompt_tokens": 1,
            "completion_tokens": 1,
            "total_tokens": 2
          }
        }
        """;

    public HttpRequestMessage? Request { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Request = request;
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                ChatCompletionResponse,
                Encoding.UTF8,
                "application/json")
        };

        return Task.FromResult(response);
    }
}
