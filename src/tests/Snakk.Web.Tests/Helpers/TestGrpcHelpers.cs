using Grpc.Core;

namespace Snakk.Web.Tests.Helpers;

/// <summary>
/// Helpers for creating gRPC AsyncUnaryCall instances used when mocking gRPC clients.
/// </summary>
public static class TestGrpcHelpers
{
    /// <summary>
    /// Creates a successful AsyncUnaryCall that resolves to the given response.
    /// </summary>
    public static AsyncUnaryCall<T> CreateAsyncUnaryCall<T>(T response)
        => new(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

    /// <summary>
    /// Creates a failed AsyncUnaryCall that throws an RpcException with the given status code.
    /// </summary>
    public static AsyncUnaryCall<T> CreateFailedAsyncUnaryCall<T>(StatusCode statusCode, string detail = "")
        => new(
            Task.FromException<T>(new RpcException(new Status(statusCode, detail))),
            Task.FromResult(new Metadata()),
            () => new Status(statusCode, detail),
            () => new Metadata(),
            () => { });
}
