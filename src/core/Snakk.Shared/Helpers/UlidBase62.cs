namespace Snakk.Shared.Helpers;

/// <summary>
/// Encodes/decodes ULID strings (26-char Crockford Base32) to/from 22-char Base62
/// for shorter, URL-safe discussion identifiers.
/// </summary>
public static class UlidBase62
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int EncodedLength = 22;
    private const int UlidLength = 26;

    /// <summary>
    /// Encodes a 26-char Crockford Base32 ULID to 22-char Base62.
    /// Throws <see cref="ArgumentException"/> when the input is not a valid ULID.
    /// </summary>
    public static string Encode(string ulid)
        => TryEncode(ulid, out var encoded)
            ? encoded
            : throw new ArgumentException($"Invalid ULID: '{ulid}'", nameof(ulid));

    /// <summary>
    /// Attempts to encode a 26-char Crockford Base32 ULID to 22-char Base62, returning
    /// <c>false</c> instead of throwing when <paramref name="ulid"/> is null, the wrong
    /// length, or contains a non-Crockford character. Use this on URL-building paths so a
    /// single malformed id (e.g. a bad seed/import) can't throw and 500 an entire page.
    /// </summary>
    public static bool TryEncode(string? ulid, out string encoded)
    {
        encoded = string.Empty;
        if (ulid is null || ulid.Length != UlidLength)
            return false;

        var value = UInt128.Zero;
        foreach (var c in ulid.ToUpperInvariant())
        {
            var index = CrockfordAlphabet.IndexOf(c);
            if (index < 0) return false;
            value = value * 32 + (UInt128)index;
        }

        Span<char> result = stackalloc char[EncodedLength];
        for (var i = EncodedLength - 1; i >= 0; i--)
        {
            result[i] = Base62Alphabet[(int)(value % 62)];
            value /= 62;
        }

        encoded = new string(result);
        return true;
    }

    public static string Decode(string encoded)
    {
        var value = UInt128.Zero;
        foreach (var c in encoded)
        {
            var index = Base62Alphabet.IndexOf(c);
            if (index < 0) throw new ArgumentException($"Invalid Base62 character: {c}");
            value = value * 62 + (UInt128)index;
        }

        Span<char> result = stackalloc char[UlidLength];
        for (var i = UlidLength - 1; i >= 0; i--)
        {
            result[i] = CrockfordAlphabet[(int)(value % 32)];
            value /= 32;
        }

        return new string(result);
    }
}
