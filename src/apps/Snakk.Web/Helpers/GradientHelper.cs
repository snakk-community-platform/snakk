using Snakk.Shared.Avatars;

namespace Snakk.Web.Helpers;

public static class GradientHelper
{
    public static string SpaceGradient(string? publicId, int angle = 135) =>
        publicId is null ? string.Empty : AvatarGenerator.GenerateGradientCss($"space:{publicId}", angle);

    public static string HubGradient(string? publicId, int angle = 135) =>
        publicId is null ? string.Empty : AvatarGenerator.GenerateGradientCss($"hub:{publicId}", angle);

    public static string UserGradient(string? publicId, int angle = 135) =>
        publicId is null ? string.Empty : AvatarGenerator.GenerateGradientCss($"user:{publicId}", angle);

    public static string CommunityGradient(string? publicId, int angle = 135) =>
        publicId is null ? string.Empty : AvatarGenerator.GenerateGradientCss($"community:{publicId}", angle);
}
