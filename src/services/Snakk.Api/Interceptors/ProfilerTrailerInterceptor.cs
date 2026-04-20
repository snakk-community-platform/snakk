using Grpc.Core;
using Grpc.Core.Interceptors;
using StackExchange.Profiling;

namespace Snakk.Api.Interceptors;

public sealed class ProfilerTrailerInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var response = await continuation(request, context);

        var id = MiniProfiler.Current?.Id;
        if (id.HasValue)
            context.ResponseTrailers.Add("x-miniprofiler-ids", id.Value.ToString());

        return response;
    }
}
