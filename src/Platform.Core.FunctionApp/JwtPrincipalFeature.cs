using System.Security.Claims;

namespace Platform.Core.FunctionApp;

/// <summary>
/// Holds the authenticated user principal for the request 
/// along with the access token they used.
/// </summary>
/// <remarks>
/// Jwt Principal Feature
/// </remarks>
/// <param name="principal"></param>
/// <param name="accessToken"></param>
public class JwtPrincipalFeature(ClaimsPrincipal principal, string accessToken)
{

    /// <summary>
    /// The claims principal
    /// </summary>
    public ClaimsPrincipal Principal { get; } = principal;

    /// <summary>
    /// The access token that was used for this request. Can be used to acquire further access tokens with the on-behalf-of flow.
    /// </summary>
    public string AccessToken { get; } = accessToken;
}
