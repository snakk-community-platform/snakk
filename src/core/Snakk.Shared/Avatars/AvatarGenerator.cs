using System.Security.Cryptography;
using System.Text;

namespace Snakk.Shared.Avatars;

/// <summary>
/// Avatar style types inspired by Boring Avatars.
/// </summary>
public enum AvatarType
{
    /// <summary>Flowing colors that look like marbles</summary>
    Marble = 0,
    /// <summary>Duotone smiley face with offset circles</summary>
    Beam = 1,
    /// <summary>Pixel art style grid</summary>
    Pixel = 2,
    /// <summary>Random circles, squares and lines (Bauhaus Moholy style)</summary>
    Bauhaus = 3
}

/// <summary>
/// Generates deterministic SVG avatars with multiple styles.
/// Uses complementary colors derived from the user ID.
/// </summary>
public static class AvatarGenerator
{
    private const int TypeCount = 4;

    /// <summary>
    /// Generates an SVG avatar for the given user ID.
    /// The avatar type is algorithmically selected based on the userId.
    /// </summary>
    /// <param name="userId">The user's public ID (used as seed for deterministic generation)</param>
    /// <param name="size">The size of the avatar in pixels (default 80)</param>
    /// <param name="typeOverride">Optional type override. If null, type is selected algorithmically.</param>
    /// <returns>An SVG string</returns>
    public static string Generate(string userId, int size = 80, AvatarType? typeOverride = null)
    {
        var hash = GetHash(userId);
        var type = typeOverride ?? GetAvatarType(hash);
        var colors = GenerateComplementaryColors(hash);

        return type switch
        {
            AvatarType.Marble => GenerateMarble(hash, colors, size),
            AvatarType.Beam => GenerateBeam(hash, colors, size),
            AvatarType.Pixel => GeneratePixel(hash, colors, size),
            AvatarType.Bauhaus => GenerateBauhaus(hash, colors, size),
            _ => GenerateBauhaus(hash, colors, size)
        };
    }

    /// <summary>
    /// Gets the content type for SVG images.
    /// </summary>
    public static string ContentType => "image/svg+xml";

    /// <summary>
    /// Parses a type string to AvatarType enum.
    /// </summary>
    public static AvatarType? ParseType(string? typeString)
    {
        if (string.IsNullOrWhiteSpace(typeString))
            return null;

        return typeString.ToLowerInvariant() switch
        {
            "marble" => AvatarType.Marble,
            "beam" => AvatarType.Beam,
            "pixel" => AvatarType.Pixel,
            "bauhaus" => AvatarType.Bauhaus,
            _ => null
        };
    }

    private static byte[] GetHash(string input)
    {
        return MD5.HashData(Encoding.UTF8.GetBytes(input));
    }

    private static AvatarType GetAvatarType(byte[] hash)
    {
        // Use a specific byte from hash to determine type
        return (AvatarType)(hash[0] % TypeCount);
    }

    /// <summary>
    /// Generates a set of complementary colors based on the hash.
    /// Uses HSL color space for better harmony.
    /// </summary>
    private static string[] GenerateComplementaryColors(byte[] hash)
    {
        // Base hue from hash (0-360)
        var baseHue = (hash[1] + hash[2]) % 360;

        // Saturation and lightness variation
        var saturation = 50 + (hash[3] % 30); // 50-80%
        var lightness = 45 + (hash[4] % 20);  // 45-65%

        // Generate 5 complementary colors
        var colors = new string[5];

        // Primary color
        colors[0] = HslToHex(baseHue, saturation, lightness);

        // Complementary (opposite on wheel)
        colors[1] = HslToHex((baseHue + 180) % 360, saturation, lightness);

        // Analogous (adjacent colors)
        colors[2] = HslToHex((baseHue + 30) % 360, saturation, lightness + 10);
        colors[3] = HslToHex((baseHue + 330) % 360, saturation, lightness - 10);

        // Triadic
        colors[4] = HslToHex((baseHue + 120) % 360, saturation - 10, lightness + 5);

        return colors;
    }

    private static string HslToHex(int h, int s, int l)
    {
        var hue = h / 360.0;
        var sat = s / 100.0;
        var light = l / 100.0;

        double r, g, b;

        if (Math.Abs(sat) < 0.001)
        {
            r = g = b = light;
        }
        else
        {
            var q = light < 0.5 ? light * (1 + sat) : light + sat - light * sat;
            var p = 2 * light - q;
            r = HueToRgb(p, q, hue + 1.0 / 3);
            g = HueToRgb(p, q, hue);
            b = HueToRgb(p, q, hue - 1.0 / 3);
        }

        var ri = (int)Math.Round(r * 255);
        var gi = (int)Math.Round(g * 255);
        var bi = (int)Math.Round(b * 255);

        return $"#{ri:X2}{gi:X2}{bi:X2}";
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }

    /// <summary>
    /// Calculates the relative luminance of a hex color.
    /// Uses the formula from WCAG 2.0.
    /// </summary>
    private static double GetLuminance(string hexColor)
    {
        // Parse hex color (format: #RRGGBB)
        var hex = hexColor.TrimStart('#');
        var r = int.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber) / 255.0;
        var g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255.0;
        var b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255.0;

        // Apply sRGB gamma correction
        r = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        g = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        b = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

        // Calculate luminance
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    /// <summary>
    /// Returns black or white, whichever provides better contrast against the given background color.
    /// </summary>
    private static string GetContrastColor(string backgroundColor)
    {
        var luminance = GetLuminance(backgroundColor);
        // If luminance is greater than 0.5, the color is "light", so use black
        // Otherwise use white for better contrast
        return luminance > 0.5 ? "#000000" : "#FFFFFF";
    }

    #region Marble Style

    /// <summary>
    /// Generates a marble-style avatar with flowing gradient colors.
    /// </summary>
    private static string GenerateMarble(byte[] hash, string[] colors, int size)
    {
        var svg = new StringBuilder();
        var maskId = $"mask_{hash[0]:X2}{hash[1]:X2}";

        svg.Append($@"<svg viewBox=""0 0 {size} {size}"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"" width=""{size}"" height=""{size}"">");

        // Define mask for circular clipping
        svg.Append($@"<mask id=""{maskId}"" maskUnits=""userSpaceOnUse"" x=""0"" y=""0"" width=""{size}"" height=""{size}"">");
        svg.Append($@"<circle cx=""{size / 2}"" cy=""{size / 2}"" r=""{size / 2}"" fill=""white""/>");
        svg.Append("</mask>");

        svg.Append($@"<g mask=""url(#{maskId})"">");

        // Background
        svg.Append($@"<rect width=""{size}"" height=""{size}"" fill=""{colors[0]}""/>");

        // Generate flowing blob shapes
        for (int i = 0; i < 3; i++)
        {
            var cx = (hash[(5 + i) % hash.Length] % 100) / 100.0 * size;
            var cy = (hash[(8 + i) % hash.Length] % 100) / 100.0 * size;
            var rx = size * 0.4 + (hash[(11 + i) % hash.Length] % 50) / 100.0 * size * 0.3;
            var ry = size * 0.3 + (hash[(14 + i) % hash.Length] % 50) / 100.0 * size * 0.4;
            var rotation = hash[(i + 2) % hash.Length] % 180;
            var colorIdx = (i + 1) % colors.Length;

            // Create organic blob using ellipse with filter
            svg.Append($@"<ellipse cx=""{cx:F1}"" cy=""{cy:F1}"" rx=""{rx:F1}"" ry=""{ry:F1}"" fill=""{colors[colorIdx]}"" transform=""rotate({rotation} {cx:F1} {cy:F1})"" opacity=""0.8""/>");
        }

        // Add some smaller accent blobs
        for (int i = 0; i < 2; i++)
        {
            var cx = (hash[3 + i * 2] % 100) / 100.0 * size;
            var cy = (hash[4 + i * 2] % 100) / 100.0 * size;
            var r = size * 0.15 + (hash[6 + i] % 30) / 100.0 * size * 0.15;
            var colorIdx = (i + 3) % colors.Length;

            svg.Append($@"<circle cx=""{cx:F1}"" cy=""{cy:F1}"" r=""{r:F1}"" fill=""{colors[colorIdx]}"" opacity=""0.7""/>");
        }

        svg.Append("</g>");
        svg.Append("</svg>");

        return svg.ToString();
    }

    #endregion

    #region Beam Style (Duotone Smiley)

    /// <summary>
    /// Generates a beam-style avatar with a duotone smiley face.
    /// Two colored circles offset each other with facial features.
    /// Eyes and mouth use black or white for maximum contrast.
    /// </summary>
    private static string GenerateBeam(byte[] hash, string[] colors, int size)
    {
        var svg = new StringBuilder();
        var wrapperTranslateX = (hash[0] % 10) - 5;
        var wrapperTranslateY = (hash[1] % 10) - 5;
        var faceRotate = (hash[2] % 30) - 15;
        var isHappy = hash[3] % 2 == 0;

        // Calculate positions
        var faceX = size / 2.0;
        var faceY = size / 2.0;
        var faceRadius = size * 0.4;

        // Eye positions
        var eyeSpread = 4 + (hash[4] % 4);
        var eyeY = faceY - faceRadius * 0.15;
        var leftEyeX = faceX - eyeSpread;
        var rightEyeX = faceX + eyeSpread;

        // Mouth
        var mouthY = faceY + faceRadius * 0.25;

        // Determine contrast color for facial features (black or white based on face color)
        var faceColor = colors[2];
        var featureColor = GetContrastColor(faceColor);

        svg.Append($@"<svg viewBox=""0 0 {size} {size}"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"" width=""{size}"" height=""{size}"">");

        // Background
        svg.Append($@"<rect width=""{size}"" height=""{size}"" fill=""{colors[0]}""/>");

        // Main face group with transform
        svg.Append($@"<g transform=""translate({wrapperTranslateX} {wrapperTranslateY})"">");

        // Back circle (offset)
        var backOffsetX = (hash[5] % 10) - 5;
        var backOffsetY = (hash[6] % 10) - 5;
        svg.Append($@"<circle cx=""{faceX + backOffsetX:F1}"" cy=""{faceY + backOffsetY:F1}"" r=""{faceRadius:F1}"" fill=""{colors[1]}""/>");

        // Front face circle
        svg.Append($@"<g transform=""rotate({faceRotate} {faceX:F1} {faceY:F1})"">");
        svg.Append($@"<circle cx=""{faceX:F1}"" cy=""{faceY:F1}"" r=""{faceRadius:F1}"" fill=""{faceColor}""/>");

        svg.Append("</g>"); // rotate group
        svg.Append("</g>"); // translate group

        // Facial features drawn LAST (on top of everything) with contrast color
        // Apply same transforms to position correctly
        svg.Append($@"<g transform=""translate({wrapperTranslateX} {wrapperTranslateY})"">");
        svg.Append($@"<g transform=""rotate({faceRotate} {faceX:F1} {faceY:F1})"">");

        // Eyes
        var eyeSize = 3 + (hash[7] % 3);
        svg.Append($@"<circle cx=""{leftEyeX:F1}"" cy=""{eyeY:F1}"" r=""{eyeSize}"" fill=""{featureColor}""/>");
        svg.Append($@"<circle cx=""{rightEyeX:F1}"" cy=""{eyeY:F1}"" r=""{eyeSize}"" fill=""{featureColor}""/>");

        // Mouth
        var mouthWidth = 10 + (hash[8] % 6);
        if (isHappy)
        {
            // Happy smile arc
            svg.Append($@"<path d=""M{faceX - mouthWidth / 2:F1} {mouthY:F1} Q {faceX:F1} {mouthY + 8:F1} {faceX + mouthWidth / 2:F1} {mouthY:F1}"" stroke=""{featureColor}"" stroke-width=""2"" fill=""none"" stroke-linecap=""round""/>");
        }
        else
        {
            // Neutral or slight frown
            svg.Append($@"<line x1=""{faceX - mouthWidth / 2:F1}"" y1=""{mouthY:F1}"" x2=""{faceX + mouthWidth / 2:F1}"" y2=""{mouthY + (hash[9] % 4) - 2:F1}"" stroke=""{featureColor}"" stroke-width=""2"" stroke-linecap=""round""/>");
        }

        svg.Append("</g>"); // rotate group
        svg.Append("</g>"); // translate group
        svg.Append("</svg>");

        return svg.ToString();
    }

    #endregion

    #region Pixel Style

    /// <summary>
    /// Generates a pixel art style avatar with a grid of colored squares.
    /// </summary>
    private static string GeneratePixel(byte[] hash, string[] colors, int size)
    {
        var svg = new StringBuilder();
        var gridSize = 8; // 8x8 pixel grid
        var pixelSize = (double)size / gridSize;

        svg.Append($@"<svg viewBox=""0 0 {size} {size}"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"" width=""{size}"" height=""{size}"">");

        // Background
        svg.Append($@"<rect width=""{size}"" height=""{size}"" fill=""{colors[0]}""/>");

        // Generate symmetric pixel pattern (mirror horizontally for face-like appearance)
        var halfGrid = gridSize / 2;

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < halfGrid; x++)
            {
                // Use hash to determine if pixel is filled
                var hashIndex = (y * halfGrid + x) % hash.Length;
                var isActive = hash[hashIndex] % 3 != 0; // ~66% chance of pixel being active

                if (isActive)
                {
                    var colorIndex = (hash[(hashIndex + 1) % hash.Length] % (colors.Length - 1)) + 1;
                    var color = colors[colorIndex];

                    // Left side pixel
                    var leftX = x * pixelSize;
                    var pixelY = y * pixelSize;
                    svg.Append($@"<rect x=""{leftX:F1}"" y=""{pixelY:F1}"" width=""{pixelSize:F1}"" height=""{pixelSize:F1}"" fill=""{color}""/>");

                    // Mirrored right side pixel
                    var rightX = (gridSize - 1 - x) * pixelSize;
                    svg.Append($@"<rect x=""{rightX:F1}"" y=""{pixelY:F1}"" width=""{pixelSize:F1}"" height=""{pixelSize:F1}"" fill=""{color}""/>");
                }
            }
        }

        svg.Append("</svg>");

        return svg.ToString();
    }

    #endregion

    #region Bauhaus Style

    /// <summary>
    /// Generates a Bauhaus Moholy style avatar with geometric shapes.
    /// </summary>
    private static string GenerateBauhaus(byte[] hash, string[] colors, int size)
    {
        var svg = new StringBuilder();

        svg.Append($@"<svg viewBox=""0 0 {size} {size}"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"" width=""{size}"" height=""{size}"">");

        // Background
        svg.Append($@"<rect width=""{size}"" height=""{size}"" fill=""{colors[0]}""/>");

        // Generate shapes based on hash values
        var shapeCount = 3 + (hash[0] % 3); // 3-5 shapes

        for (int i = 0; i < shapeCount; i++)
        {
            var shapeType = hash[i + 1] % 4; // 0=circle, 1=rect, 2=triangle, 3=line
            var colorIndex = (hash[i + 2] % (colors.Length - 1)) + 1;
            var color = colors[colorIndex];

            // Position and size derived from hash
            var x = (hash[(i * 3 + 3) % hash.Length] % 100) / 100.0 * size;
            var y = (hash[(i * 3 + 4) % hash.Length] % 100) / 100.0 * size;
            var shapeSize = (size / 4.0) + (hash[(i * 3 + 5) % hash.Length] % 100) / 100.0 * size / 2.0;
            var rotation = hash[(i * 3 + 6) % hash.Length] % 360;

            switch (shapeType)
            {
                case 0: // Circle
                    svg.Append($@"<circle cx=""{x:F1}"" cy=""{y:F1}"" r=""{shapeSize / 2:F1}"" fill=""{color}""/>");
                    break;
                case 1: // Rectangle
                    var rectW = shapeSize;
                    var rectH = shapeSize * (0.5 + (hash[(i * 3 + 7) % hash.Length] % 50) / 100.0);
                    svg.Append($@"<rect x=""{x - rectW / 2:F1}"" y=""{y - rectH / 2:F1}"" width=""{rectW:F1}"" height=""{rectH:F1}"" fill=""{color}"" transform=""rotate({rotation} {x:F1} {y:F1})""/>");
                    break;
                case 2: // Triangle (as polygon)
                    var triSize = shapeSize * 0.8;
                    var p1 = $"{x:F1},{y - triSize / 2:F1}";
                    var p2 = $"{x - triSize / 2:F1},{y + triSize / 2:F1}";
                    var p3 = $"{x + triSize / 2:F1},{y + triSize / 2:F1}";
                    svg.Append($@"<polygon points=""{p1} {p2} {p3}"" fill=""{color}"" transform=""rotate({rotation} {x:F1} {y:F1})""/>");
                    break;
                case 3: // Line
                    var lineLength = shapeSize;
                    var strokeWidth = 3 + (hash[(i + 8) % hash.Length] % 6);
                    var x2 = x + Math.Cos(rotation * Math.PI / 180) * lineLength;
                    var y2 = y + Math.Sin(rotation * Math.PI / 180) * lineLength;
                    svg.Append($@"<line x1=""{x:F1}"" y1=""{y:F1}"" x2=""{x2:F1}"" y2=""{y2:F1}"" stroke=""{color}"" stroke-width=""{strokeWidth}"" stroke-linecap=""round""/>");
                    break;
            }
        }

        svg.Append("</svg>");

        return svg.ToString();
    }

    #endregion
}
