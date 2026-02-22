namespace UtilityMenuSite.Infrastructure.Configuration;

public class EmailSettings
{
    public string Provider { get; set; } = "Console";
    public string ApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "noreply@utilitymenu.com";
    public string FromName { get; set; } = "UtilityMenu";
}
