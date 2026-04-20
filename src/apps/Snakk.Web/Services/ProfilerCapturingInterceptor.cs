using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Snakk.Web.Services;

public sealed class ProfilerCapturingInterceptor(IHttpContextAccessor httpContextAccessor) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var call = continuation(request, context);
        var services = httpContextAccessor.HttpContext?.RequestServices;
        if (services is null) return call;

        return new AsyncUnaryCall<TResponse>(
            CaptureAsync(call, services),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    private static async Task<TResponse> CaptureAsync<TResponse>(
        AsyncUnaryCall<TResponse> call, IServiceProvider services)
    {
        var response = await call.ResponseAsync;
        try
        {
            // Read trailers (sent after response body — reliable for gRPC)
            var ids = call.GetTrailers().GetValue("x-miniprofiler-ids");
            if (!string.IsNullOrEmpty(ids))
                services.GetService<ProfilerIdBag>()?.Add(ids);
        }
        catch
        {
            // Never break the response due to profiling
        }
        return response;
    }
}
