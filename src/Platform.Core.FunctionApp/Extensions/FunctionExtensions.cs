using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;

namespace Platform.Core.FunctionApp.Extensions;
public static class FunctionExtensions
{
    /// <summary>
    /// DefaultSerialisationOptions
    /// </summary>
    public static readonly JsonSerializerOptions DefaultSerialisationOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Gets the query json string.
    /// </summary>
    /// <param name="httpRequestData">The request.</param>
    /// <returns></returns>
    public static string? GetQueryStringFromRequest(this HttpRequestData httpRequestData)
    {
        return httpRequestData.FunctionContext.BindingContext.BindingData["Query"] as string;
    }

    /// <summary>
    /// Converts to object.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="httpRequestData">The HTTP request data.</param>
    /// <returns></returns>
    public static T? DeserialiseQueryStringToObject<T>(this HttpRequestData httpRequestData)
    {
        var jsonData = httpRequestData.GetQueryStringFromRequest();

        if (!string.IsNullOrWhiteSpace(jsonData))
        {
            return JsonSerializer.Deserialize<T>(jsonData, DefaultSerialisationOptions);
        }

        return default;
    }

    /// <summary>
    /// Sets the response message status code
    /// </summary>
    /// <param name="context"></param>
    /// <param name="statusCode"></param>
    /// <returns></returns>
    public static async Task CreateHttpResponseStatusCodeAsync(this FunctionContext context, HttpStatusCode statusCode)
    {
        var reponseData = await context.GetHttpRequestDataAsync();
        var response = reponseData!.CreateResponse(statusCode);
        context.GetInvocationResult().Value = response;
    }

    /// <summary>
    /// Is Swagger Endpoint
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static bool IsSwaggerEndpoint(this FunctionContext context)
    {
        return context.FunctionDefinition.Name.Contains("Swagger") || context.FunctionDefinition.Name.Contains("RenderOAuth2Redirect");
    }

    /// <summary>
    /// Is Durable Client
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static bool IsDurableClient(this FunctionContext context)
    {
        return context.FunctionDefinition.InputBindings.Any(x => x.Value.Type == "durableClient");
    }

    /// <summary>
    /// Is Healthcheck Endpoint
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static bool IsHealthcheckEndpoint(this FunctionContext context)
    {
        return context.FunctionDefinition.Name.Contains("Healthcheck", StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Is Activity Triggger
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static bool IsActivityTriggger(this FunctionContext context)
    {
        return context.FunctionDefinition.InputBindings.Any(x => x.Value.Type == "activityTrigger");
    }

    /// <summary>
    /// Is Orchestration Triggger
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static bool IsOrchestrationTriggger(this FunctionContext context)
    {
        return context.FunctionDefinition.InputBindings.Any(x => x.Value.Type == "orchestrationTrigger"); ;
    }

    /// <summary>
    /// Create Json Response Message
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="request"></param>
    /// <param name="statusCode"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    public static async Task<HttpResponseData> CreateResponseMessageAsync<T>(this HttpRequestData request, T data, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(data);
        return response;
    }

    /// <summary>
    /// Create File Response Message Async
    /// </summary>
    /// <param name="request"></param>
    /// <param name="fileStreamData"></param>
    /// <param name="headers"></param>
    /// <param name="statusCode"></param>
    /// <returns></returns>
    public static async Task<HttpResponseData> CreateFileResponseMessageAsync(this HttpRequestData request, byte[] fileStreamData, HttpHeadersCollection headers, HttpStatusCode statusCode = HttpStatusCode.OK, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream(fileStreamData, 0, fileStreamData.Length);
        await ms.WriteAsync(fileStreamData, cancellationToken);
        var response = request.CreateResponse(statusCode);
        await response.WriteBytesAsync(ms.ToArray(), cancellationToken: cancellationToken);

        foreach (var header in headers)
        {
            response.Headers.Add(header.Key, header.Value);
        }
        return response;
    }

    /// <summary>
    /// Run Async
    /// </summary>
    /// <param name="host"></param>
    /// <param name="action"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static async Task RunAsync(this IHost host, Action<IServiceProvider> action, CancellationToken token = default)
    {
        try
        {
            action.Invoke(host.Services);
            await host.StartAsync(token).ConfigureAwait(false);
            await host.WaitForShutdownAsync(token).ConfigureAwait(false);
        }
        finally
        {
            if (host is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                host.Dispose();
            }
        }
    }

    /// <summary>
    /// Get Environment
    /// </summary>
    /// <returns></returns>
    public static string GetFunctionAppEnvironmentName()
    {
        return Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")!;
    }

    /// <summary>
    /// IsDevelopment
    /// </summary>
    /// <param name="environmentName"></param>
    /// <returns></returns>
    public static bool IsFunctionAppEnvironmentDevelopment(this string environmentName)
    {
        return environmentName.Equals("Development", StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// TODO: Would be nice if this was available out of the box on FunctionContext.
    /// This contains the fully qualified name of the method, e.g. FunctionNamespace.FunctionClass.ScopesAndAppRoles
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    internal static MethodInfo GetTargetFunctionMethod(this FunctionContext context)
    {
        var entryPoint = context.FunctionDefinition.EntryPoint;
        var assemblyPath = context.FunctionDefinition.PathToAssembly;
        var assembly = Assembly.LoadFrom(assemblyPath);
        var typeName = entryPoint[..entryPoint.LastIndexOf('.')];
        var type = assembly.GetType(typeName);
        var methodName = entryPoint[(entryPoint.LastIndexOf('.') + 1)..];
        var method = type!.GetMethod(methodName);
        return method!;
    }
}
