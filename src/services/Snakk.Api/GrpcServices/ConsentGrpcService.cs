namespace Snakk.Api.GrpcServices;

using Grpc.Core;
using Snakk.Api.Services;
using Snakk.Application.Services;
using Snakk.Protos.Consent;

public class ConsentGrpcService(
    IConsentService consentService,
    ICurrentUserService currentUser) : ConsentService.ConsentServiceBase
{
    public override async Task<GetRequiredConsentsResponse> GetRequiredConsents(
        GetRequiredConsentsRequest request, ServerCallContext context)
    {
        var consents = await consentService.GetActiveRequiredConsentsAsync();

        var response = new GetRequiredConsentsResponse();
        foreach (var c in consents)
        {
            var info = new Protos.Consent.ConsentTypeInfo
            {
                Slug = c.Slug,
                Name = c.Name,
                IsRequired = c.IsRequired,
                LatestVersionId = c.LatestVersionId
            };
            if (c.ShortLabel is not null) info.ShortLabel = c.ShortLabel;
            if (c.LinkUrl is not null) info.LinkUrl = c.LinkUrl;
            response.Consents.Add(info);
        }

        return response;
    }

    public override async Task<GetPendingConsentsResponse> GetPendingConsents(
        GetPendingConsentsRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var pending = await consentService.GetPendingConsentsAsync(userId);

        var response = new GetPendingConsentsResponse();
        foreach (var p in pending)
        {
            var info = new PendingConsentInfo
            {
                Slug = p.Slug,
                Name = p.Name,
                VersionId = p.VersionId
            };
            if (p.ShortLabel is not null) info.ShortLabel = p.ShortLabel;
            if (p.LinkUrl is not null) info.LinkUrl = p.LinkUrl;
            response.Consents.Add(info);
        }

        return response;
    }

    public override async Task<AcceptConsentsResponse> AcceptConsents(
        AcceptConsentsRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        await consentService.AcceptConsentsAsync(userId, request.VersionIds, request.IpAddress);
        return new AcceptConsentsResponse { Success = true };
    }

    public override async Task<GetConsentTextResponse> GetConsentText(
        GetConsentTextRequest request, ServerCallContext context)
    {
        var version = await consentService.GetLatestVersionAsync(request.Slug);

        if (version is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Consent type not found"));

        return new GetConsentTextResponse
        {
            Text = version.Text,
            VersionNumber = version.VersionNumber
        };
    }

    private string RequireAuth()
    {
        if (!currentUser.IsAuthenticated())
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userId = currentUser.GetCurrentUserId();
        if (userId is null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        return userId;
    }
}
