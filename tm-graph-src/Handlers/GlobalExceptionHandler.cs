using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using tm_graph_src.Exceptions;

namespace tm_graph_src;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AppException) logger.LogError(exception, string.Empty);
        var e = exception is AppException ae ? ae : new InternalServerErrorException(); 
        var problem = new ProblemDetails
        {
            Status = e.StatusCode,
            Title = "Error",
            Detail = e.Details
        };
        httpContext.Response.StatusCode =  e.StatusCode;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}