using System.Security.Cryptography;

namespace MayAlbum.Services;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing. The encoded value contains the
/// algorithm, iteration count, salt and derived key so hashes can be verified
/// after the defaults are strengthened later.
/// </summary>
public static class PasswordHasher
{
    private const string Prefix = "pbkdf2-sha256";
    private const int Iterations = 120_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string? password, string? encoded)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }

        try
        {
            var parts = encoded.Split('$', StringSplitOptions.None);
            if (parts.Length != 4 ||
                !string.Equals(parts[0], Prefix, StringComparison.Ordinal) ||
                !int.TryParse(parts[1], out var iterations) ||
                iterations < 10_000)
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
