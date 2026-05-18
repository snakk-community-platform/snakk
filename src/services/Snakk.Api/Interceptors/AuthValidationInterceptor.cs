namespace Snakk.Api.Interceptors;

using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Http;
using Snakk.Application.Auth;
using Snakk.Application.Services;
using Snakk.Api.Services;

public class AuthValidationInterceptor(
    IRevocationCache revocationCache,
    IAuthVersionCache authVersionCache,
    ICurrentUserService currentUser,
    IHttpContextAccessor httpContextAccessor) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateAuthAsync(context);
        return await continuation(request, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateAuthAsync(context);
        return await continuation(requestStream, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateAuthAsync(context);
        await continuation(request, responseStream, context);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await ValidateAuthAsync(context);
        await continuation(requestStream, responseStream, context);
    }

    private async Task ValidateAuthAsync(ServerCallContext context)
    {
        if (!currentUser.IsAuthenticated()) return;

        var userId = currentUser.GetCurrentUserId();
        if (userId is null) return;

        if (revocationCache.IsUserRevoked(userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "USER_REVOKED"));

        var httpContext = httpContextAccessor.HttpContext;
        var verClaim = httpContext?.User.FindFirst(CustomClaimTypes.AuthVersion)?.Value;

        if (verClaim is not null && long.TryParse(verClaim, out var claimVersion))
        {
            var storedVersion = await authVersionCache.GetVersionAsync(userId, context.CancellationToken);

            if (storedVersion is null || claimVersion != storedVersion.Value)
                throw new RpcException(new Status(StatusCode.Unauthenticated, "USER_REVOKED"));
        }
        // Missing ver claim = old JWT issued before this feature was deployed — skip check (grace period)
    }
}
