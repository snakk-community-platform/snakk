using Grpc.Core;

namespace Snakk.Web.Services;

public enum GrpcStatus
{
    Ok,
    NotFound,
    Unauthenticated,
    PermissionDenied,
    InvalidArgument,
    ServerError
}

public sealed class GrpcResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public GrpcStatus Status { get; }
    public string? Error { get; }

    private GrpcResult(bool ok, T? value, GrpcStatus status, string? error)
    {
        IsSuccess = ok;
        Value = value;
        Status = status;
        Error = error;
    }

    public static GrpcResult<T> Ok(T value) => new(true, value, GrpcStatus.Ok, null);
    public static GrpcResult<T> NotFound(string? error = null) => new(false, default, GrpcStatus.NotFound, error);
    public static GrpcResult<T> Unauthenticated() => new(false, default, GrpcStatus.Unauthenticated, null);
    public static GrpcResult<T> PermissionDenied(string? error = null) => new(false, default, GrpcStatus.PermissionDenied, error);
    public static GrpcResult<T> InvalidArgument(string? error = null) => new(false, default, GrpcStatus.InvalidArgument, error);
    public static GrpcResult<T> ServerError(string? error = null) => new(false, default, GrpcStatus.ServerError, error);

    public static GrpcResult<T> FromRpcException(RpcException ex) => ex.StatusCode switch
    {
        StatusCode.NotFound => NotFound(ex.Status.Detail),
        StatusCode.Unauthenticated => Unauthenticated(),
        StatusCode.PermissionDenied => PermissionDenied(ex.Status.Detail),
        StatusCode.InvalidArgument => InvalidArgument(ex.Status.Detail),
        _ => ServerError(ex.Status.Detail)
    };
}
