using Snakk.Shared.Helpers;

namespace Snakk.Shared.Tests.Helpers;

public class UlidBase62Tests
{
    // A real, valid 26-char Crockford Base32 ULID.
    private const string ValidUlid = "01KSDHT68R7RA3QKVXY4KWQWTV";

    [Test]
    public async Task Encode_ThenDecode_RoundTripsValidUlid()
    {
        var encoded = UlidBase62.Encode(ValidUlid);

        await Assert.That(encoded.Length).IsEqualTo(22);
        await Assert.That(UlidBase62.Decode(encoded)).IsEqualTo(ValidUlid);
    }

    [Test]
    public async Task TryEncode_ValidUlid_ReturnsTrueAndMatchesEncode()
    {
        var ok = UlidBase62.TryEncode(ValidUlid, out var encoded);

        await Assert.That(ok).IsTrue();
        await Assert.That(encoded).IsEqualTo(UlidBase62.Encode(ValidUlid));
    }

    // B3: ids containing Crockford-excluded chars (I, L, O, U) must NOT throw via TryEncode —
    // they previously 500'd every page that rendered the user's profile link.
    [Test]
    [Arguments("01JJQP00000000000000000BOB")]   // contains O
    [Arguments("01JJQP000000000000000ALICE")]   // contains L, I
    [Arguments("01JJQP0000000000000CHARLIE")]   // contains L, I
    public async Task TryEncode_CrockfordExcludedChar_ReturnsFalse(string badId)
    {
        var ok = UlidBase62.TryEncode(badId, out var encoded);

        await Assert.That(ok).IsFalse();
        await Assert.That(encoded).IsEqualTo(string.Empty);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("TOOSHORT")]
    [Arguments("01KSDHT68R7RA3QKVXY4KWQWTVEXTRA")] // too long
    public async Task TryEncode_NullOrWrongLength_ReturnsFalse(string? input)
    {
        var ok = UlidBase62.TryEncode(input, out var encoded);

        await Assert.That(ok).IsFalse();
        await Assert.That(encoded).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Encode_InvalidUlid_Throws()
    {
        await Assert.That(() => UlidBase62.Encode("01JJQP00000000000000000BOB"))
            .Throws<ArgumentException>();
    }

    // The sanitized seeder vanity ids must now be valid.
    [Test]
    [Arguments("01JJQP000000000000000A11CE")]
    [Arguments("01JJQP00000000000000000B0B")]
    [Arguments("01JJQP0000000000000CHAR11E")]
    public async Task TryEncode_SanitizedVanityIds_ReturnsTrue(string vanityId)
    {
        await Assert.That(UlidBase62.TryEncode(vanityId, out _)).IsTrue();
    }
}
