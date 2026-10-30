namespace tm_graph_src.Exceptions;

public abstract class AppException(int statusCode, string details = "An error occurred while processing your request.") : Exception(details)
{
    public string Details = details;
    public int StatusCode = statusCode;
}

public class BadRequestException(string details = "Bad Request.") : AppException(StatusCodes.Status400BadRequest, details)
{
}

public class UnauthorizedException(string details = "Unauthorized.") : AppException(StatusCodes.Status401Unauthorized, details)
{
}

public class ForbiddenException(string details = "Forbidden.") : AppException(StatusCodes.Status403Forbidden, details)
{
}

public class NotFoundException(string details = "Not Found.") : AppException(StatusCodes.Status404NotFound, details)
{
}

public class ConflictException(string details = "Conflict.") : AppException(StatusCodes.Status409Conflict, details)
{
}

public class UnprocessableEntityException(string details = "Unprocessable Entity.") : AppException(StatusCodes.Status422UnprocessableEntity, details)
{
}

public class TooManyRequestsException(string details = "Too Many Requests.") : AppException(StatusCodes.Status429TooManyRequests, details)
{
}

public class InternalServerErrorException(string details = "Internal Server Error.") : AppException(StatusCodes.Status500InternalServerError, details)
{
}

public class NotImplementedException(string details = "Not Implemented.") : AppException(StatusCodes.Status501NotImplemented, details)
{
}

public class BadGatewayException(string details = "Bad Gateway.") : AppException(StatusCodes.Status502BadGateway, details)
{
}

public class ServiceUnavailableException(string details = "Service Unavailable.") : AppException(StatusCodes.Status503ServiceUnavailable, details)
{
}