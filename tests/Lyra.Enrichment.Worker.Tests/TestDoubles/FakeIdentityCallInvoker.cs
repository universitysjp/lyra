namespace Lyra.Enrichment.Worker.Tests.TestDoubles;

using global::Grpc.Core;

internal sealed class FakeIdentityCallInvoker : CallInvoker
{
    private readonly Func<object, object> _responseFactory;

    public FakeIdentityCallInvoker(Func<object, object> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    public Metadata? Headers { get; private set; }

    public DateTime? Deadline { get; private set; }

    public object? Request { get; private set; }

    public int CallCount { get; private set; }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method,
        string? host,
        CallOptions options,
        TRequest request)
    {
        CallCount++;
        Headers = options.Headers;
        Deadline = options.Deadline;
        Request = request;

        Task<TResponse> responseTask;
        try
        {
            responseTask = Task.FromResult((TResponse)_responseFactory(request!));
        }
        catch (Exception exception)
        {
            responseTask = Task.FromException<TResponse>(exception);
        }

        return new AsyncUnaryCall<TResponse>(
            responseTask,
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method,
        string? host,
        CallOptions options,
        TRequest request) =>
        AsyncUnaryCall(method, host, options, request)
            .ResponseAsync
            .GetAwaiter()
            .GetResult();

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method,
        string? host,
        CallOptions options) =>
        throw new NotSupportedException();

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method,
        string? host,
        CallOptions options) =>
        throw new NotSupportedException();

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method,
        string? host,
        CallOptions options,
        TRequest request) =>
        throw new NotSupportedException();
}
