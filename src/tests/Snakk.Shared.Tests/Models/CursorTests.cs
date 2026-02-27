using Snakk.Shared.Models;

namespace Snakk.Shared.Tests.Models;

public class CursorTests
{
    [Test]
    public async Task Encode_WithDateTimeAndInt_ProducesBase64String()
    {
        // Arrange
        var dateTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var id = 42;

        // Act
        var encoded = Cursor.Encode(dateTime, id);

        // Assert
        await Assert.That(encoded).IsNotNull();
        await Assert.That(encoded.Length).IsGreaterThan(0);
        // Verify it's valid base64 by attempting decode
        var bytes = Convert.FromBase64String(encoded);
        await Assert.That(bytes.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Decode_ReversesEncode_RoundTrip()
    {
        // Arrange
        var originalDateTime = new DateTime(2024, 6, 15, 12, 30, 45, DateTimeKind.Utc);
        var originalId = 99;

        // Act
        var encoded = Cursor.Encode(originalDateTime, originalId);
        var decoded = Cursor.Decode(encoded);

        // Assert
        await Assert.That(decoded).IsNotNull();
        await Assert.That(decoded!.Value.DateTime).IsEqualTo(originalDateTime);
        await Assert.That(decoded.Value.Id).IsEqualTo(originalId);
    }

    [Test]
    public async Task Decode_WithNull_ReturnsNull()
    {
        // Act
        var result = Cursor.Decode(null);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Decode_WithEmptyString_ReturnsNull()
    {
        // Act
        var result = Cursor.Decode("");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Decode_WithInvalidBase64_ReturnsNull()
    {
        // Act
        var result = Cursor.Decode("not-valid-base64!!!");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Decode_WithMalformedContent_ReturnsNull()
    {
        // Arrange - valid base64 but wrong internal format (no ~ separator)
        var malformed = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("no-separator-here"));

        // Act
        var result = Cursor.Decode(malformed);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Encode_WithNullDateTime_UsesTicksZero()
    {
        // Act
        var encoded = Cursor.Encode(null, 5);
        var decoded = Cursor.Decode(encoded);

        // Assert
        await Assert.That(decoded).IsNotNull();
        await Assert.That(decoded!.Value.DateTime).IsNull();
        await Assert.That(decoded.Value.Id).IsEqualTo(5);
    }

    [Test]
    public async Task RoundTrip_PreservesDateTimePrecisionAndId()
    {
        // Arrange
        var dateTime = new DateTime(2025, 1, 15, 10, 30, 45, 123, DateTimeKind.Utc);
        var id = 12345;

        // Act
        var encoded = Cursor.Encode(dateTime, id);
        var decoded = Cursor.Decode(encoded);

        // Assert
        await Assert.That(decoded).IsNotNull();
        await Assert.That(decoded!.Value.DateTime!.Value.Ticks).IsEqualTo(dateTime.Ticks);
        await Assert.That(decoded.Value.Id).IsEqualTo(id);
    }
}
