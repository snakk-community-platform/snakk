using Grpc.Core;
using Snakk.Application.Services;
using Snakk.Protos.Markup;

namespace Snakk.Api.GrpcServices;

public class MarkupGrpcService(
    IMarkupParser markupParser) : MarkupService.MarkupServiceBase
{
    public override Task<PreviewMarkupResponse> Preview(PreviewMarkupRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Task.FromResult(new PreviewMarkupResponse
            {
                Html = "<p class=\"text-base-content/50 italic\">Nothing to preview</p>"
            });
        }

        var html = markupParser.ToHtml(request.Content);

        return Task.FromResult(new PreviewMarkupResponse
        {
            Html = $"<div class=\"prose prose-sm max-w-none\">{html}</div>"
        });
    }
}
