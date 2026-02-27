using Snakk.Shared.Avatars;

namespace Snakk.Shared.Tests.Avatars;

public class AvatarGeneratorTests
{
    [Test]
    public async Task Generate_ReturnsValidSvgString()
    {
        // Arrange
        var userId = "test-user-id-12345";

        // Act
        var svg = AvatarGenerator.Generate(userId);

        // Assert
        await Assert.That(svg).StartsWith("<svg");
        await Assert.That(svg).EndsWith("</svg>");
    }

    [Test]
    public async Task Generate_IsDeterministic_SameUserIdSameOutput()
    {
        // Arrange
        var userId = "deterministic-test-user";

        // Act
        var svg1 = AvatarGenerator.Generate(userId);
        var svg2 = AvatarGenerator.Generate(userId);

        // Assert
        await Assert.That(svg1).IsEqualTo(svg2);
    }

    [Test]
    public async Task Generate_DifferentUserIds_ProduceDifferentAvatars()
    {
        // Arrange & Act
        var svg1 = AvatarGenerator.Generate("user-alpha");
        var svg2 = AvatarGenerator.Generate("user-beta");

        // Assert
        await Assert.That(svg1).IsNotEqualTo(svg2);
    }

    [Test]
    public async Task Generate_WithMarbleOverride_ProducesValidSvg()
    {
        // Arrange & Act
        var svg = AvatarGenerator.Generate("test-user", typeOverride: AvatarType.Marble);

        // Assert
        await Assert.That(svg).StartsWith("<svg");
        await Assert.That(svg).EndsWith("</svg>");
    }

    [Test]
    public async Task Generate_WithBeamOverride_ProducesValidSvg()
    {
        // Arrange & Act
        var svg = AvatarGenerator.Generate("test-user", typeOverride: AvatarType.Beam);

        // Assert
        await Assert.That(svg).StartsWith("<svg");
        await Assert.That(svg).EndsWith("</svg>");
    }

    [Test]
    public async Task Generate_WithPixelOverride_ProducesValidSvg()
    {
        // Arrange & Act
        var svg = AvatarGenerator.Generate("test-user", typeOverride: AvatarType.Pixel);

        // Assert
        await Assert.That(svg).StartsWith("<svg");
        await Assert.That(svg).EndsWith("</svg>");
    }

    [Test]
    public async Task Generate_WithBauhausOverride_ProducesValidSvg()
    {
        // Arrange & Act
        var svg = AvatarGenerator.Generate("test-user", typeOverride: AvatarType.Bauhaus);

        // Assert
        await Assert.That(svg).StartsWith("<svg");
        await Assert.That(svg).EndsWith("</svg>");
    }

    [Test]
    [Arguments("marble")]
    [Arguments("beam")]
    [Arguments("pixel")]
    [Arguments("bauhaus")]
    public async Task ParseType_ParsesValidTypeStrings(string typeString)
    {
        // Act
        var result = AvatarGenerator.ParseType(typeString);

        // Assert
        await Assert.That(result).IsNotNull();
    }

    [Test]
    [Arguments("MARBLE")]
    [Arguments("Beam")]
    [Arguments("PIXEL")]
    public async Task ParseType_IsCaseInsensitive(string typeString)
    {
        // Act
        var result = AvatarGenerator.ParseType(typeString);

        // Assert
        await Assert.That(result).IsNotNull();
    }

    [Test]
    [Arguments("invalid")]
    [Arguments("unknown")]
    [Arguments("circle")]
    public async Task ParseType_ReturnsNullForInvalidStrings(string typeString)
    {
        // Act
        var result = AvatarGenerator.ParseType(typeString);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ParseType_ReturnsNullForNull()
    {
        // Act
        var result = AvatarGenerator.ParseType(null);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ParseType_ReturnsNullForEmptyString()
    {
        // Act
        var result = AvatarGenerator.ParseType("");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ParseType_ReturnsNullForWhitespace()
    {
        // Act
        var result = AvatarGenerator.ParseType("   ");

        // Assert
        await Assert.That(result).IsNull();
    }
}
