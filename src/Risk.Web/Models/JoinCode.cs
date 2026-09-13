using System.Security.Cryptography;

namespace Risk.Web.Models;

/// <summary>
/// A 6-char networked-game join code (e.g. <c>R7K9X2</c>): uppercase
/// alphanumeric excluding ambiguous <c>I/O/1/0</c>, so codes survive being
/// read aloud or retyped on a phone. Generation uses
/// <see cref="RandomNumberGenerator"/> (never <c>Random.Shared</c>) — a
/// guessable code would let strangers join a private table.
/// </summary>
public sealed record JoinCode(string Value)
{
    /// <summary>Alphabet without ambiguous characters (32 symbols).</summary>
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public const int Length = 6;

    /// <summary>Crypto-random code; collision against existing games is the
    /// registry's job (it retries on the rare duplicate), not this type's.</summary>
    public static JoinCode Generate()
    {
        Span<char> chars = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new JoinCode(new string(chars));
    }

    /// <summary>Case-insensitive parse; accepts only exact-length codes over
    /// <see cref="Alphabet"/>. Normalizes to uppercase.</summary>
    public static bool TryParse(string? s, out JoinCode? code)
    {
        code = null;

        if (s is null || s.Length != Length)
        {
            return false;
        }

        var upper = s.ToUpperInvariant();
        foreach (var c in upper)
        {
            if (!Alphabet.Contains(c))
            {
                return false;
            }
        }

        code = new JoinCode(upper);
        return true;
    }
}
