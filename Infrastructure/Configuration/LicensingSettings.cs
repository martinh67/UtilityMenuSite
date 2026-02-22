namespace UtilityMenuSite.Infrastructure.Configuration;

public class LicensingSettings
{
    public string HmacSigningKey { get; set; } = string.Empty;
    public int StalenessWindowDays { get; set; } = 7;
    public int GracePeriodDays { get; set; } = 7;
}
