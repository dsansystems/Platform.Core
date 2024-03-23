using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Platform.Core.FunctionApp.Attributes;
using Platform.Core.FunctionApp.Extensions;

namespace Platform.Core.FunctionApp.Middleware;

/// <summary>
/// <see cref="AuthorisationMiddleware"/>
/// </summary>
public class AuthorisationMiddleware : IFunctionsWorkerMiddleware
{

    /// <summary>
    /// azure AD scope metadata
    /// </summary>
    private const string ScopeClaimType = "http://schemas.microsoft.com/identity/claims/scope";

    /// <summary>
    /// Invoke middleware to authorise request
    /// </summary>
    /// <param name="context"></param>
    /// <param name="next"></param>
    /// <returns></returns>
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var principalFeature = context.Features.Get<JwtPrincipalFeature>();

        if (!AuthorisePrincipal(context, principalFeature!.Principal))
        {
            await context.CreateHttpResponseStatusCodeAsync(HttpStatusCode.Forbidden);
            return;
        }

        await next(context);
    }

    /// <summary>
    /// Authorise Principal
    /// </summary>
    /// <param name="context"></param>
    /// <param name="principal"></param>
    /// <returns></returns>
    private static bool AuthorisePrincipal(FunctionContext context, ClaimsPrincipal principal)
    {
        // This authorization implementation was made for Azure AD.
        if (principal.HasClaim(c => c.Type == ScopeClaimType))
        {
            // Request made with delegated permissions, check scopes and user roles
            return AuthoriseDelegatedPermissions(context, principal);
        }

        // Request made with application permissions, check app roles
        return AuthoriseApplicationPermissions(context, principal);
    }

    /// <summary>
    /// Authorise Delegated Permissions
    /// </summary>
    /// <param name="context"></param>
    /// <param name="principal"></param>
    /// <returns></returns>
    private static bool AuthoriseDelegatedPermissions(FunctionContext context, ClaimsPrincipal principal)
    {
        var targetMethod = context.GetTargetFunctionMethod();

        var (acceptedScopes, acceptedUserRoles) = GetAcceptedScopesAndUserRoles(targetMethod);

        var userRoles = principal.FindAll(ClaimTypes.Role);
        var userHasAcceptedRole = userRoles.Any(ur => acceptedUserRoles.Contains(ur.Value));

        // Scopes are stored in a single claim, space-separated
        var callerScopes = (principal.FindFirst(ScopeClaimType)?.Value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var callerHasAcceptedScope = callerScopes.Any(acceptedScopes.Contains);

        // This app requires both a scope and user role, when called with scopes, so we check both
        return userHasAcceptedRole && callerHasAcceptedScope;
    }

    /// <summary>
    /// Authorise Application Permissions
    /// </summary>
    /// <param name="context"></param>
    /// <param name="principal"></param>
    /// <returns></returns>
    private static bool AuthoriseApplicationPermissions(FunctionContext context, ClaimsPrincipal principal)
    {
        var targetMethod = context.GetTargetFunctionMethod();

        var acceptedAppRoles = GetAcceptedAppRoles(targetMethod);
        var appRoles = principal.FindAll(ClaimTypes.Role);
        var appHasAcceptedRole = appRoles.Any(ur => acceptedAppRoles.Contains(ur.Value));
        return appHasAcceptedRole;
    }

    /// <summary>
    /// Get Accepted Scopes And UserRoles
    /// </summary>
    /// <param name="targetMethod"></param>
    /// <returns></returns>
    private static (List<string> scopes, List<string> userRoles) GetAcceptedScopesAndUserRoles(MethodInfo targetMethod)
    {
        var attributes = GetCustomAttributesOnClassAndMethod<AuthoriseAttribute>(targetMethod);
        // If scopes A and B are allowed at class level, and scope A is allowed at method level, then only scope A can be allowed.
        // This finds those common scopes and user roles on the attributes.
        var scopes = attributes
            .Skip(1)
            .Select(a => a.Scopes)
            .Aggregate(attributes.FirstOrDefault()?.Scopes ?? Enumerable.Empty<string>(), (result, acceptedScopes) =>
            {
                return result.Intersect(acceptedScopes);
            })
            .ToList();

        var userRoles = attributes
            .Skip(1)
            .Select(a => a.UserRoles)
            .Aggregate(attributes.FirstOrDefault()?.UserRoles ?? Enumerable.Empty<string>(), (result, acceptedRoles) =>
            {
                return result.Intersect(acceptedRoles);
            })
            .ToList();
        return (scopes, userRoles);
    }

    /// <summary>
    /// Get Accepted AppRoles
    /// </summary>
    /// <param name="targetMethod"></param>
    /// <returns></returns>
    private static List<string> GetAcceptedAppRoles(MethodInfo targetMethod)
    {
        var attributes = GetCustomAttributesOnClassAndMethod<AuthoriseAttribute>(targetMethod);

        // Same as above for scopes and user roles, only allow app roles that are common in class and method level attributes.
        return attributes
           .Skip(1)
           .Select(a => a.AppRoles)
           .Aggregate(attributes.FirstOrDefault()?.UserRoles ?? Enumerable.Empty<string>(), (result, acceptedRoles) =>
           {
               return result.Intersect(acceptedRoles).ToList();

           }).ToList();
    }

    /// <summary>
    /// Get Custom Attributes On Class and Method
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="targetMethod"></param>
    /// <returns></returns>
    private static List<T> GetCustomAttributesOnClassAndMethod<T>(MethodInfo targetMethod) where T : Attribute
    {
        var methodAttributes = targetMethod.GetCustomAttributes<T>();
        var classAttributes = targetMethod.DeclaringType!.GetCustomAttributes<T>();
        return methodAttributes.Concat(classAttributes).ToList();
    }
}
