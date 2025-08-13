namespace ChatPDF.Web.Configuration;

public class IdentityServerConfiguration
{
    public const string SectionName = "IdentityServer";
    
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public bool RequireHttpsMetadata { get; set; } = true;
    public string[] Scopes { get; set; } = ["openid", "profile"];
}