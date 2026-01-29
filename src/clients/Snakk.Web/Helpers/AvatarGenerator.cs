using System.Security.Cryptography;
using System.Text;

namespace Snakk.Web.Helpers;

/// <summary>
/// Generates deterministic SVG avatars inspired by Boring Avatars (Bauhaus style).
/// Uses geometric shapes with colors derived from the user ID.
/// </summary>
public static class AvatarGenerator
{
    // Carefully curated color palettes that work well together
    private static readonly string[][] Palettes =
    [
        ["#92A1C6", "#146A7C", "#F0AB3D", "#C271B4", "#C20D90"], // Original
        ["#A3A948", "#EDB92E", "#F85931", "#CE1836", "#009989"], // Warm
        ["#5D5D5D", "#7C7C7C", "#A5A5A5", "#CDCDCD", "#2D2D2D"], // Grayscale
        ["#264653", "#2A9D8F", "#E9C46A", "#F4A261", "#E76F51"], // Earth
        ["#606C38", "#283618", "#FEFAE0", "#DDA15E", "#BC6C25"], // Forest
        ["#003049", "#D62828", "#F77F00", "#FCBF49", "#EAE2B7"], // Retro
        ["#0D1B2A", "#1B263B", "#415A77", "#778DA9", "#E0E1DD"], // Navy
        ["#2B2D42", "#8D99AE", "#EDF2F4", "#EF233C", "#D90429"], // Modern
    ];

    /// <summary>
    /// Generates an SVG avatar for the given user ID.
    /// </summary>
    /// <param name="userId">The user's public ID (used as seed for deterministic generation)</param>
    /// <param name="displayName">Optional display name for generating initials as fallback data</param>
    /// <param name="size">The size of the avatar in pixels (default 40)</param>
    /// <returns>An SVG string</returns>
    public static string Generate(string userId, string? displayName = null, int size = 40)
    {
        var hash = GetHash(userId);
        var palette = GetPalette(hash);

        // Generate deterministic values from hash
        var values = new int[20];
        for (int i = 0; i < values.Length && i < hash.Length; i++)
        {
            values[i] = hash[i];
        }

        var svg = new StringBuilder();
        svg.Append($@"<svg viewBox=""0 0 {size} {size}"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"" width=""{size}"" height=""{size}"">");

        // Background
        svg.Append($@"<rect width=""{size}"" height=""{size}"" fill=""{palette[0]}""/>");

        // Generate shapes based on hash values
        var shapeCount = 3 + (values[0] % 3); // 3-5 shapes

        for (int i = 0; i < shapeCount; i++)
        {
            var shapeType = values[i + 1] % 3;
            var colorIndex = (values[i + 2] % (palette.Length - 1)) + 1;
            var color = palette[colorIndex];

            // Position and size derived from hash
            var x = (values[i + 3] % 100) / 100.0 * size;
            var y = (values[i + 4] % 100) / 100.0 * size;
            var shapeSize = (size / 4.0) + ((values[i + 5] % 100) / 100.0 * size / 2.0);
            var rotation = values[i + 6] % 360;

            switch (shapeType)
            {
                case 0: // Circle
                    svg.Append($@"<circle cx=""{x:F1}"" cy=""{y:F1}"" r=""{shapeSize / 2:F1}"" fill=""{color}""/>");
                    break;
                case 1: // Rectangle
                    var rectW = shapeSize;
                    var rectH = shapeSize * (0.5 + (values[i + 7] % 50) / 100.0);
                    svg.Append($@"<rect x=""{x - rectW / 2:F1}"" y=""{y - rectH / 2:F1}"" width=""{rectW:F1}"" height=""{rectH:F1}"" fill=""{color}"" transform=""rotate({rotation} {x:F1} {y:F1})""/>");
                    break;
                case 2: // Triangle (as polygon)
                    var triSize = shapeSize * 0.8;
                    var p1 = $"{x:F1},{y - triSize / 2:F1}";
                    var p2 = $"{x - triSize / 2:F1},{y + triSize / 2:F1}";
                    var p3 = $"{x + triSize / 2:F1},{y + triSize / 2:F1}";
                    svg.Append($@"<polygon points=""{p1} {p2} {p3}"" fill=""{color}"" transform=""rotate({rotation} {x:F1} {y:F1})""/>");
                    break;
            }
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    /// <summary>
    /// Generates an SVG avatar as a data URI for use in img src attributes.
    /// </summary>
    public static string GenerateDataUri(string userId, string? displayName = null, int size = 40)
    {
        var svg = Generate(userId, displayName, size);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        return $"data:image/svg+xml;base64,{base64}";
    }

    /// <summary>
    /// Gets the URL for a user's avatar (uploaded or generated).
    /// </summary>
    /// <param name="userId">The user's public ID</param>
    /// <param name="avatarFileName">The uploaded avatar filename, if any</param>
    /// <param name="apiBaseUrl">The API base URL for serving avatars</param>
    public static string GetAvatarUrl(string userId, string? avatarFileName, string apiBaseUrl)
    {
        // If user has uploaded avatar, use the API endpoint
        if (!string.IsNullOrEmpty(avatarFileName))
        {
            return $"{apiBaseUrl}/avatars/{userId}";
        }

        // Otherwise return generated avatar endpoint
        return $"{apiBaseUrl}/avatars/{userId}/generated";
    }

    private static byte[] GetHash(string input)
    {
        return MD5.HashData(Encoding.UTF8.GetBytes(input));
    }

    private static string[] GetPalette(byte[] hash)
    {
        var paletteIndex = hash[0] % Palettes.Length;
        return Palettes[paletteIndex];
    }
}
