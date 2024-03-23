using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Azure.Functions.Worker;

namespace Platform.Core.FunctionApp.Middleware;

/// <summary>
/// Adds header to the the response before 
/// </summary>
public class CorrelationIdHeaderMiddleware : IFunctionsWorkerMiddleware
{
    /// <summary>
    /// Http Headers Middleware add the required 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="next"></param>
    /// <returns></returns>
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var requestData = await context.GetHttpRequestDataAsync();
        string correlationId;

        if (requestData!.Headers.TryGetValues("X-CorrelationId", out var values))
        {
            correlationId = values.First();
        }
        else
        {
            correlationId = Guid.NewGuid().ToString();
        }

        await next(context);

        context.GetHttpResponseData()?.Headers.Add("X-CorrelationId", correlationId);
    }
}
