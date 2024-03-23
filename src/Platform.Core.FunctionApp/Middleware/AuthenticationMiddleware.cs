using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using Platform.Core.FunctionApp.Configuration;
using Platform.Core.FunctionApp;
using Platform.Core.FunctionApp.Extensions;
using System.Text.Json;

namespace Platform.Core.Auth.FunctionApp.Middleware;

/// <summary>
/// <see cref="AuthenticationMiddleware"/>.
/// </summary>
public class AuthenticationMiddleware : IFunctionsWorkerMiddleware
{
    /// <summary>
    /// _tokenValidator
    /// </summary>
    private readonly JwtSecurityTokenHandler _tokenValidator;

    /// <summary>
    /// _tokenValidationParameters
    /// </summary>
    private readonly TokenValidationParameters _tokenValidationParameters;

    /// <summary>
    /// OpenId Connect Configuration
    /// </summary>
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;

    /// <summary>
    /// Auth Configuration
    /// </summary>
    private readonly AuthConfiguration _authConfiguration;

    /// <summary>
    /// <see cref="AuthenticationMiddleware"/>
    /// </summary>
    /// <param name="authConfigurationOptions"></param>
    public AuthenticationMiddleware(IOptions<AuthConfiguration> authConfigurationOptions)
    {
        _authConfiguration = authConfigurationOptions.Value;
        _tokenValidator = new JwtSecurityTokenHandler();
        _tokenValidationParameters = new TokenValidationParameters
        {
            ValidAudiences = [_authConfiguration.ClientId, $"api://{_authConfiguration.ClientId}"]
        };
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(_authConfiguration.OpenIdMetadataAddress, new OpenIdConnectConfigurationRetriever());
    }

    /// <summary>
    /// Invoke function interceptor
    /// </summary>
    /// <param name="context"></param>
    /// <param name="next"></param>
    /// <returns></returns>
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (!TryGetTokenFromHeaders(context, out var token))
        {
            await context.CreateHttpResponseStatusCodeAsync(HttpStatusCode.Unauthorized);
            return;
        }

        if (!_tokenValidator.CanReadToken(token))
        {
            await context.CreateHttpResponseStatusCodeAsync(HttpStatusCode.Unauthorized);
            return;
        }

        await ValidateToken(context, token!, next);
    }

    /// <summary>
    /// Validate Token
    /// </summary>
    /// <param name="context"></param>
    /// <param name="token"></param>
    /// <param name="functionExecutionDelegate"></param>
    /// <returns></returns>
    private async Task ValidateToken(FunctionContext context, string token, FunctionExecutionDelegate functionExecutionDelegate)
    {
        try
        {
            var validationParameters = _tokenValidationParameters.Clone();
            var openIdConfig = await _configurationManager.GetConfigurationAsync(default);
            validationParameters.ValidIssuers = [openIdConfig.Issuer, $"https://sts.windows.net/{_authConfiguration.TenantId}/"];
            validationParameters.IssuerSigningKeys = openIdConfig.SigningKeys;

            // Validate token
            var principal = _tokenValidator.ValidateToken(token, validationParameters, out _);

            // Set principal + token in Features collection.They can be accessed from here later in the call chain
            context.Features.Set(new JwtPrincipalFeature(principal, token));

            await functionExecutionDelegate(context);
        }
        catch (SecurityTokenException ex)
        {
            // Token is not valid (expired etc.)
            Debug.WriteLine($"Token Validation failed because: {ex.Message}");
            await context.CreateHttpResponseStatusCodeAsync(HttpStatusCode.Unauthorized);
            return;
        }
    }

    /// <summary>
    /// Try Get Token From Headers
    /// </summary>
    /// <param name="context"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private static bool TryGetTokenFromHeaders(FunctionContext context, out string? token)
    {
        token = null;

        // HTTP headers are in the binding context as a JSON object
        // The first checks ensure that we have the JSON string
        if (!context.BindingContext.BindingData.TryGetValue("Headers", out var headersObj))
        {
            return false;
        }

        if (headersObj is not string headersStr)
        {
            return false;
        }

        // Deserialize headers from JSON
        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersStr);
        var normalizedKeyHeaders = headers!.ToDictionary(h => h.Key.ToLowerInvariant(), h => h.Value);

        if (!normalizedKeyHeaders.TryGetValue("authorization", out var authHeaderValue))
        {
            return false; // No Authorization header present
        }

        if (!authHeaderValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false; // Scheme is not Bearer
        }

        token = authHeaderValue["Bearer ".Length..].Trim();
        return true;
    }
}
