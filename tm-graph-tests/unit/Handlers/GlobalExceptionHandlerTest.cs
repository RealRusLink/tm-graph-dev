using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using tm_graph_src;
using tm_graph_src.Exceptions;

namespace tm_graph_tests;

public class GlobalExceptionHandlerTest
{
    public static TheoryData<AppException, int> AppExceptionCases => new()
    {
        { new BadRequestException(), StatusCodes.Status400BadRequest },
        { new UnauthorizedException(), StatusCodes.Status401Unauthorized },
        { new ForbiddenException(), StatusCodes.Status403Forbidden },
        { new NotFoundException(), StatusCodes.Status404NotFound },
        { new ConflictException(), StatusCodes.Status409Conflict },
        { new UnprocessableEntityException(), StatusCodes.Status422UnprocessableEntity },
        { new TooManyRequestsException(), StatusCodes.Status429TooManyRequests },
        { new InternalServerErrorException(), StatusCodes.Status500InternalServerError },
        { new tm_graph_src.Exceptions.NotImplementedException(), StatusCodes.Status501NotImplemented },
        { new BadGatewayException(), StatusCodes.Status502BadGateway },
        { new ServiceUnavailableException(), StatusCodes.Status503ServiceUnavailable },
    };

    private static async Task<(HttpContext Context, ProblemDetails? Problem, bool Handled)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddOptions().BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();

        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return (context, problem, handled);
    }

    [Theory]
    [MemberData(nameof(AppExceptionCases))]
    public async Task AppException_ShouldMapToItsStatusCode(AppException exception, int expectedStatus)
    {
        var (context, problem, handled) = await HandleAsync(exception);

        Assert.True(handled);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedStatus, problem!.Status);
        Assert.Equal("Error", problem.Title);
        Assert.Equal(exception.Details, problem.Detail);
    }

    [Fact]
    public async Task AppException_ShouldPropagateCustomDetails()
    {
        var exception = new ConflictException("User with this email already exists");

        var (_, problem, _) = await HandleAsync(exception);

        Assert.Equal("User with this email already exists", problem!.Detail);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
    }

    [Fact]
    public async Task UnknownException_ShouldBecomeInternalServerError()
    {
        var exception = new InvalidOperationException("secret connection string: Server=db;Password=...");

        var (context, problem, handled) = await HandleAsync(exception);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem!.Status);
        Assert.Equal("Internal Server Error.", problem.Detail);
        Assert.DoesNotContain("secret", problem.Detail);
    }

    [Fact]
    public async Task Response_ShouldBeJson()
    {
        var (context, _, _) = await HandleAsync(new BadRequestException());

        Assert.StartsWith("application/json", context.Response.ContentType);
    }
}
