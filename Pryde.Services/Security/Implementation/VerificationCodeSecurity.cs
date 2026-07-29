using System.Security.Cryptography;
using System.Text;
using Pryde.Domain.Enums;

namespace Pryde.Services.Security.Implementation;

internal static class VerificationCodeSecurity
{
    public static string GenerateSixDigitCode()
    {
        return RandomNumberGenerator
            .GetInt32(100000, 1000000)
            .ToString();
    }

    public static string Hash(
        Guid userId,
        VerificationCodePurpose purpose,
        string code)
    {
        var value = $"{userId:N}:{purpose}:{code}";
        var valueBytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToHexString(SHA256.HashData(valueBytes));
    }

    public static bool Matches(
        Guid userId,
        VerificationCodePurpose purpose,
        string suppliedCode,
        string storedHash)
    {
        var suppliedHash = Convert.FromHexString(
            Hash(userId, purpose, suppliedCode));
        byte[] storedHashBytes;

        try
        {
            storedHashBytes = Convert.FromHexString(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            suppliedHash,
            storedHashBytes);
    }
}
