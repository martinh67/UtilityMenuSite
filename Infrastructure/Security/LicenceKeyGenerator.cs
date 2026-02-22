using System.Security.Cryptography;

namespace UtilityMenuSite.Infrastructure.Security;

public static class LicenceKeyGenerator
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        var bytes = new byte[12];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(bytes);

        var chars = new char[12];
        for (int i = 0; i < 12; i++)
            chars[i] = Chars[bytes[i] % Chars.Length];

        return $"UMENU-{new string(chars, 0, 4)}-{new string(chars, 4, 4)}-{new string(chars, 8, 4)}";
    }
}
