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

    public static string Encode(string ulid)
    {
        var value = UInt128.Zero;
        foreach (var c in ulid.ToUpperInvariant())
        {
            var index = CrockfordAlphabet.IndexOf(c);
            if (index < 0) throw new ArgumentException($"Invalid ULID character: {c}");
            value = value * 32 + (UInt128)index;
        }

        Span<char> result = stackalloc char[EncodedLength];
        for (var i = EncodedLength - 1; i >= 0; i--)
        {
            result[i] = Base62Alphabet[(int)(value % 62)];
            value /= 62;
        }

        return new string(result);
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
