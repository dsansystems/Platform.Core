using System.Globalization;

namespace Platform.Core.Auth.FunctionApp.Configuration;

/// <summary>
/// Auth Configuration
/// </summary>
public class AuthConfiguration
{
    /// <summary>
    /// OpenId Configuration Path
    /// </summary>
    private const string OpenIdConfigurationPath = ".well-known/openid-configuration";

    /// <summary>
    /// the Azure AD instance eg.https://login.microsoftonline.com/
    /// </summary>
    public string Instance { get; set; } = "https://login.microsoftonline.com/{0}/v2.0";

    /// <summary>
    /// The appication Id in the app registration
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    /// The Client Secret
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The TenantId where the client app resides
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// Token Audiences
    /// </summary>
    public IEnumerable<string> TokenAudiences { get; set; } = Enumerable.Empty<string>();


    /// <summary>
    /// Authority
    /// </summary>
    public string Authority
    {
        get
        {
            return string.Format(CultureInfo.InvariantCulture, Instance, TenantId);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public string OpenIdMetadataAddress
    {
        get
        {
            return $"{Authority}/{OpenIdConfigurationPath}";
        }
    }
}
