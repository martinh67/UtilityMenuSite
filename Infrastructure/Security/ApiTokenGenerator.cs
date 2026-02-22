using System.Security.Cryptography;

namespace UtilityMenuSite.Infrastructure.Security;

public static class ApiTokenGenerator
{
    public static string Generate()
    {
        var bytes = new byte[48];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(bytes);

        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=')
            [..64];
    }
}
