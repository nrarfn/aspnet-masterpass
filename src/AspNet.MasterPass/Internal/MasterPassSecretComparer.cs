using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AspNet.MasterPass.Internal;

/// <summary>
/// Compares the supplied password with the configured secret, accepting either plaintext
/// or an ASP.NET Core Identity password hash.
/// </summary>
internal static class MasterPassSecretComparer
{
    private static readonly PasswordHasher<object> HashVerifier =
        new(Options.Create(new PasswordHasherOptions()));

    public static bool Matches(string providedPassword, string secret)
    {
        if (LooksLikeIdentityHash(secret))
        {
            var result = HashVerifier.VerifyHashedPassword(new object(), secret, providedPassword);
            return result != PasswordVerificationResult.Failed;
        }

        return FixedTimeEquals(providedPassword, secret);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftHash = SHA256.HashData(Encoding.UTF8.GetBytes(left));
        var rightHash = SHA256.HashData(Encoding.UTF8.GetBytes(right));
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }

    internal static bool LooksLikeIdentityHash(string value)
    {
        if (value.Length < 48)
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written < 33)
        {
            return false;
        }

        return buffer[0] == 0x01;
    }
}