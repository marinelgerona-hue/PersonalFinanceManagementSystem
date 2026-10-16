using System.Security.Cryptography;
using System.Text;

namespace PersonalFinanceManagementSystem.Services;

/// <summary>Simple salted SHA-256 password hashing for the in-memory demo.</summary>
public static class SecurityHelper
{
    public static string NewSalt() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    public static string Hash(string password, string salt)
    {
        var bytes = Encoding.UTF8.GetBytes($"{salt}:{password}");
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public static bool Verify(string password, string salt, string expectedHash)
        => Hash(password, salt).Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
}
